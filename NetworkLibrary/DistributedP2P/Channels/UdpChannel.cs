using NetworkLibrary.Components.Crypto.Algorithms;
using NetworkLibrary.DistributedP2P.Channels.Components;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.UDP.Jumbo;
using NetworkLibrary.UDP.Reliable.Components;
using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Sockets;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class UdpChannel : IChannel
    {
        public ChannelInfo Info { get; private set; }

        private UdpChannelBase innerchannel;

        public event Action<byte[], int, int> OnMessageReceived;

        protected JumboModule JumboUdp = new JumboModule(0);
        internal ReliableModule ReliableUdp;
        ReliableModule internalReliableModule;


        public UdpChannel(Socket udpSocket, IPEndPoint receiveEp, ChannelInfo info)
        {
           
            Info = info;
            innerchannel = new UdpChannelBase(udpSocket, receiveEp, info);
            JumboUdp.SendToSocket = SendJumboSegment;
            JumboUdp.MessageReceived = HandleMessage;

            SenderModule sender = new SenderModule();
           
            sender.MaxSegmentSize = 1280;
            sender.MinWindowSize = 1280*2;

            ReliableUdp = new ReliableModule(receiveEp,sender);

            ReliableUdp.OnReceived += (e, b, o, c) => HandleMessage(b, o, c);
            ReliableUdp.OnSend += SendRudpSegment;

            SenderModule sender2 = new SenderModule();

            sender.MaxSegmentSize = 1280;
            sender.MinWindowSize = 1280 * 2;
            internalReliableModule = new ReliableModule(receiveEp, sender2);
            internalReliableModule.OnReceived += (e, b, o, c) => HandleInternalReliableMessage(b, o, c);
            internalReliableModule.OnSend += SendInternalRudpSegment;

        }

        public void Start()
        {
            innerchannel.OnMessageReceived += BytesReceived;
            innerchannel.Start();
        }

        protected virtual void BytesReceived(byte[] buffer, int offset, int count)
        {
            // filter flags
            var flag = (MessageFlags)buffer[offset++];
            count--;

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
                    break;

                case MessageFlags.HP:
                case MessageFlags.HPAck:
                    break;
                case MessageFlags.InternalReliableMessage:
                    HandleIncomingInternalRudpSegment(buffer, offset, count);
                    break;
                case MessageFlags.Ping:
                    break;

            }
        }


        protected virtual void HandleInternalReliableMessage(byte[] buffer, int offset, int count)
        {
           
        }

        protected virtual void HandleMessage(byte[] buffer, int offset, int count)
        {
            OnMessageReceived?.Invoke(buffer, offset, count);
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
        public void Dispose()
        {
        }
    }
}
