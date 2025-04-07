using NetworkLibrary.Components.Crypto.Algorithms;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.UDP.Jumbo;
using NetworkLibrary.UDP.Reliable.Components;
using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Sockets;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class UdpMessageChannel : IChannel
    {
        public ChannelInfo Info { get; private set; }

        private UdpChannel innerchannel;

        public event Action<byte[], int, int> OnMessageReceived;

        protected JumboModule JumboUdp = new JumboModule(0);
        internal ReliableModule ReliableUdp;

        public UdpMessageChannel(Socket udpSocket, IPEndPoint receiveEp, ChannelInfo info)
        {
           
            Info = info;
            innerchannel = new UdpChannel(udpSocket, receiveEp, info);
            JumboUdp.SendToSocket = SendJumboSegment;
            JumboUdp.MessageReceived = HandleMessage;

            SenderModule sender = new SenderModule();
           
            sender.MaxSegmentSize = 1280;
            sender.MinWindowSize = 1280*2;

            ReliableUdp = new ReliableModule(receiveEp,sender);

            ReliableUdp.OnReceived += (e, b, o, c) => HandleMessage(b, o, c);
            ReliableUdp.OnSend += SendRudpSegment;

        }

        public void Start()
        {
            innerchannel.OnMessageReceived += BytesReceived;
            innerchannel.Start();
        }

        protected virtual void BytesReceived(byte[] buffer, int offset, int count)
        {
            // filter flags
            var flag = (UdpFlags)buffer[offset++];
            count--;

            switch (flag)
            {
                case UdpFlags.StandardMessage:
                    HandleMessage(buffer, offset, count);
                    break;
                case UdpFlags.JumboMessage:
                    HandleJumboSegment(buffer, offset, count);
                    break;
                case UdpFlags.ReliableMessage:
                    HandleRudpSegment(buffer, offset, count);
                    break;
                case UdpFlags.KeepAliveMessage:
                    break;

                case UdpFlags.HP:
                case UdpFlags.HPAck:
                    break;
            }
        }



        protected void HandleMessage(byte[] buffer, int offset, int count)
        {
            OnMessageReceived?.Invoke(buffer, offset, count);
        }

        protected void HandleJumboSegment(byte[] buffer, int offset, int count)
        {
            JumboUdp.HandleReceivedSegment(buffer, offset, count);
        }

        protected virtual void SendJumboSegment(byte[] arg1, int arg2, int arg3)
        {
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.WriteByte((byte)UdpFlags.JumboMessage);
            stream.Write(arg1, arg2, arg3);
            SendInternal(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

        protected void HandleRudpSegment(byte[] buffer, int offset, int count)
        {
            ReliableUdp.HandleBytes(buffer, offset, count);
        }
        internal virtual void SendRudpSegment(ReliableModule module, byte[] buffer, int offset, int count)
        {
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.WriteByte((byte)UdpFlags.ReliableMessage);
            stream.Write(buffer, offset, count);
            SendInternal(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }


        public virtual void Send(byte[] buffer, int offset, int count)
        {

            if (count > 64000)
            {
                JumboUdp.Send(buffer, offset, count);
            }
            else
            {
                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                stream.WriteByte((byte)UdpFlags.StandardMessage);
                stream.Write(buffer, offset, count);
                SendInternal(stream.GetBuffer(), 0, stream.Position32);
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
            }

        }

        public void SendReliable(byte[] buffer, int offset, int count)
        {
            ReliableUdp.Send(buffer, offset, count);
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
