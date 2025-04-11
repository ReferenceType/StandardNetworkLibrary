using NetworkLibrary.DistributedP2P.Channels.Components;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.UDP.Jumbo;
using NetworkLibrary.UDP.Reliable.Components;
using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class UdpChannel : IChannel
    {
        public ChannelInfo Info { get; private set; }

        private UdpChannelBase innerchannel;

        public event Action<byte[], int, int> OnBytesReceived;
        public event Action OnDisconnected;

        protected JumboModule JumboUdp = new JumboModule(0);
        internal ReliableModule ReliableUdp;
        private ReliableModule internalReliableModule;
        protected KeepAlive keepAlive;
        protected Pinger pinger;

        private int isClosed = 0;
        private int isDisposed = 0;


        public UdpChannel(Socket udpSocket, IPEndPoint receiveEp, ChannelInfo info)
        {

            Info = info;
            innerchannel = new UdpChannelBase(udpSocket, receiveEp, info);
            JumboUdp.SendToSocket = SendJumboSegment;
            JumboUdp.MessageReceived = HandleMessage;

            SenderModule sender = new SenderModule();

            sender.MaxSegmentSize = 1280;
            sender.MinWindowSize = 1280 * 2;

            ReliableUdp = new ReliableModule(receiveEp, sender);

            ReliableUdp.OnReceived += (e, b, o, c) => HandleMessage(b, o, c);
            ReliableUdp.OnSend += SendRudpSegment;

            SenderModule sender2 = new SenderModule();

            sender.MaxSegmentSize = 1280;
            sender.MinWindowSize = 1280 * 2;
            internalReliableModule = new ReliableModule(receiveEp, sender2);
            internalReliableModule.OnReceived += (e, b, o, c) => HandleInternalReliableMessage(b, o, c);
            internalReliableModule.OnSend += SendInternalRudpSegment;

            keepAlive = new KeepAlive();
            keepAlive.SendData += SendInternalReliable;
            keepAlive.NotAlive += HandleDisconnect;

            pinger = new Pinger();
            pinger.SendData += SendInternalReliable;


        }



        public void Start()
        {
            innerchannel.OnBytesReceived += BytesReceived;
            innerchannel.OnDisconnected += HandleDisconnect;
            innerchannel.Start();
        }

        protected virtual void BytesReceived(byte[] buffer, int offset, int count)
        {
            // filter flags
            var flag = (MessageFlags)buffer[offset++];
            count--;
            HandleReivedMessage(buffer, offset, count, flag);
        }

        protected virtual void HandleReivedMessage(byte[] buffer, int offset, int count, MessageFlags flag)
        {
            switch (flag)
            {
                case MessageFlags.StandardMessage:
                    HandleMessage(buffer, offset, count);
                    break;

                case MessageFlags.JumboMessage:
                    HandleJumboSegment(buffer, offset, count);
                    break;

                case MessageFlags.ReliableMessage:
                    HandleRudpSegment(buffer, offset, count);
                    break;

                case MessageFlags.KeepAliveMessage:
                    keepAlive.HandleMessage(flag, buffer, offset, count);
                    break;

                case MessageFlags.Kill:
                    HandleDisconnect();
                    break;

                case MessageFlags.InternalReliableMessage:
                    HandleIncomingInternalRudpSegment(buffer, offset, count);
                    break;

                case MessageFlags.Ping:
                case MessageFlags.Pong:
                    pinger.HandleMessage(flag, buffer, offset, count);
                    break;

            }


        }


        protected virtual void HandleInternalReliableMessage(byte[] buffer, int offset, int count)
        {
            var flag = (MessageFlags)buffer[offset++];
            count--;

            HandleReivedMessage(buffer, offset, count, flag);
        }

        protected virtual void HandleMessage(byte[] buffer, int offset, int count)
        {
            OnBytesReceived?.Invoke(buffer, offset, count);
        }

        protected void HandleJumboSegment(byte[] buffer, int offset, int count)
        {
            JumboUdp.HandleReceivedSegment(buffer, offset, count);
        }

        protected virtual void SendJumboSegment(byte[] arg1, int arg2, int arg3)
        {
            SendWithFlag(MessageFlags.JumboMessage, arg1, arg2, arg3);
        }

        protected void HandleRudpSegment(byte[] buffer, int offset, int count)
        {
            ReliableUdp.HandleBytes(buffer, offset, count);
        }

        protected void HandleIncomingInternalRudpSegment(byte[] buffer, int offset, int count)
        {
            internalReliableModule.HandleBytes(buffer, offset, count);
        }
        internal virtual void SendRudpSegment(ReliableModule module, byte[] buffer, int offset, int count)
        {
            SendWithFlag(MessageFlags.ReliableMessage, buffer, offset, count);
        }

        internal virtual void SendInternalRudpSegment(ReliableModule module, byte[] buffer, int offset, int count)
        {
            SendWithFlag(MessageFlags.InternalReliableMessage, buffer, offset, count);
        }

        public Task<double> Ping()
        {
            return pinger.Ping();
        }

        public virtual void Send(byte[] buffer, int offset, int count)
        {

            if (count > 64000)
            {
                JumboUdp.Send(buffer, offset, count);
            }
            else
            {
                SendWithFlag(MessageFlags.StandardMessage, buffer, offset, count);
            }

        }

        public void SendReliable(byte[] buffer, int offset, int count)
        {
            ReliableUdp.Send(buffer, offset, count);
        }


        protected virtual void SendWithFlag(MessageFlags flag, byte[] arg1, int arg2, int arg3)
        {
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.WriteByte((byte)flag);
            stream.Write(arg1, arg2, arg3);
            SendInternal(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

        protected void SendInternalReliable(MessageFlags flag, byte[] buffer, int offset, int count)
        {
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.WriteByte((byte)flag);
            stream.Write(buffer, offset, count);

            internalReliableModule.Send(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

        protected void SendInternal(byte[] bytes, int offset, int count)
        {
            try
            {
                innerchannel.Send(bytes, offset, count);
            }
            catch (Exception e)
            {
            }

        }


        public void CloseChannel()
        {
            try
            {
                SendWithFlag(MessageFlags.Kill, new byte[1], 0, 1);
            }
            catch { }

            HandleDisconnect();

        }

        protected void HandleDisconnect()
        {
            if (Interlocked.CompareExchange(ref isClosed, 1, 0) == 0)
            {
                OnDisconnected?.Invoke();
                Dispose();
            }

        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref isDisposed, 1, 0) == 0)
                ReleseResources();
        }

        protected virtual void ReleseResources()
        {
            ReliableUdp.Close();
            internalReliableModule.Close();
            keepAlive.Close();
            JumboUdp.Release();

            innerchannel.CloseChannel();

            OnDisconnected = null;
            OnBytesReceived = null;


        }


    }
}
