using NetworkLibrary.DistributedP2P.Channels.Components;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Components;
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
        private readonly ILogger logger;

        public UdpChannel(Socket udpSocket, IPEndPoint receiveEp, ChannelInfo info, ILogger logger)
        {

            Info = info;
            this.logger = logger;
            innerchannel = new UdpChannelBase(udpSocket, receiveEp, info, logger);

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
                    HandleKeepAliveMessage(buffer, offset, count, flag);
                    break;

                case MessageFlags.Kill:
                    HandleDisconnect();
                    break;

                case MessageFlags.InternalReliableMessage:
                    HandleIncomingInternalRudpSegment(buffer, offset, count);
                    break;

                case MessageFlags.Ping:
                case MessageFlags.Pong:
                    HandlePingMessage(buffer, offset, count, flag);
                    break;

            }


        }


        private void HandleInternalReliableMessage(byte[] buffer, int offset, int count)
        {
            var flag = (MessageFlags)buffer[offset++];
            count--;

            HandleReivedMessage(buffer, offset, count, flag);
        }

        private void HandleMessage(byte[] buffer, int offset, int count)
        {
            try
            {
                OnBytesReceived?.Invoke(buffer, offset, count);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
                throw;
            }
        }

        private void HandleJumboSegment(byte[] buffer, int offset, int count)
        {
            try
            {
                JumboUdp.HandleReceivedSegment(buffer, offset, count);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
            }
        }

        private void SendJumboSegment(byte[] arg1, int arg2, int arg3)
        {
            try
            {
                SendWithFlag(MessageFlags.JumboMessage, arg1, arg2, arg3);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
            }

        }

        private void HandleRudpSegment(byte[] buffer, int offset, int count)
        {
            try
            {
                ReliableUdp.HandleBytes(buffer, offset, count);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
            }

        }
        private void HandleKeepAliveMessage(byte[] buffer, int offset, int count, MessageFlags flag)
        {
            try
            {
                keepAlive.HandleMessage(flag, buffer, offset, count);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
            }
        }

        private void HandleIncomingInternalRudpSegment(byte[] buffer, int offset, int count)
        {
            try
            {
                internalReliableModule.HandleBytes(buffer, offset, count);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
            }
        }

        private void HandlePingMessage(byte[] buffer, int offset, int count, MessageFlags flag)
        {
            try
            {
                pinger.HandleMessage(flag, buffer, offset, count);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
            }

        }

        private void SendRudpSegment(ReliableModule module, byte[] buffer, int offset, int count)
        {
            try
            {
                SendWithFlag(MessageFlags.ReliableMessage, buffer, offset, count);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
            }
        }


        private void SendInternalRudpSegment(ReliableModule module, byte[] buffer, int offset, int count)
        {
            try
            {
                SendWithFlag(MessageFlags.InternalReliableMessage, buffer, offset, count);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
            }

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
                Log(e);
                CloseChannel();
                throw;
            }
        }


        public void CloseChannel()
        {
            try
            {
                SendWithFlag(MessageFlags.Kill, new byte[1], 0, 1);

            }
            catch { }

            try
            {
                innerchannel.CloseChannel();
            }
            catch { }

            HandleDisconnect();

        }

        protected void HandleDisconnect()
        {
            if (Interlocked.CompareExchange(ref isClosed, 1, 0) == 0)
            {
                Log(LogType.Debug,"Udp Channel disconnected");
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

        protected virtual void Log(LogType logType, string v)
        {
            logger?.Log(logType, v);
        }

        protected virtual void Log(Exception e)
        {
            logger?.Log(e);
        }
    }
}
