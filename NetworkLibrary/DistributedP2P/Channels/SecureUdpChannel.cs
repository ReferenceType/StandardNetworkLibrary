using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.UDP.Jumbo;
using NetworkLibrary.UDP.Reliable.Components;
using NetworkLibrary.Utils;
using System.Net;
using NetworkLibrary.Components;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class SecureUdpChannel:UdpChannel
    {
        private readonly ConcurrentAesAlgorithm algo;
        byte[] decryptBuff = new byte[65555];
        public SecureUdpChannel(Socket udpSocket, IPEndPoint receiveEp, ConcurrentAesAlgorithm algo, ChannelInfo info) : base(udpSocket, receiveEp, info)
        {
            this.algo = algo;
        }

        protected override void BytesReceived(byte[] buffer, int offset, int count)
        {
            var flag = (UdpFlags)buffer[offset++];
            count--;

            if (flag == UdpFlags.HP || flag == UdpFlags.HPAck)
                return;

            count = algo.DecryptInto(buffer, offset, count, decryptBuff, 0);
            buffer = decryptBuff;
            offset = 0;

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

        public override void Send(byte[] buffer, int offset, int count)
        {
            if (count > 64000)
            {
                JumboUdp.Send(buffer, offset, count);
            }
            else
            {
                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                stream.WriteByte((byte)UdpFlags.StandardMessage);

                stream.Reserve(count + 128);
                stream.Position32 += algo.EncryptInto(buffer, offset, count, stream.GetBuffer(), 1);


                SendInternal(stream.GetBuffer(), 0, stream.Position32);
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
            }
        }

        protected override void SendJumboSegment(byte[] buffer, int offset, int count)
        {
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.WriteByte((byte)UdpFlags.JumboMessage);

            stream.Reserve(count + 128);
            stream.Position32 += algo.EncryptInto(buffer, offset, count, stream.GetBuffer(), 1);

            SendInternal(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

        internal override void SendRudpSegment(ReliableModule module,byte[] buffer, int offset, int count)
        {
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.WriteByte((byte)UdpFlags.ReliableMessage);

            stream.Reserve(count+128);
            stream.Position32 += algo.EncryptInto(buffer, offset, count, stream.GetBuffer(),1);

            SendInternal(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }
    }

}
