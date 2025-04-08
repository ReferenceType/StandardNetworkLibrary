using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.UDP.Jumbo;
using NetworkLibrary.UDP.Reliable.Components;
using NetworkLibrary.Utils;
using System.Net;
using NetworkLibrary.Components;
using NetworkLibrary.DistributedP2P.Channels.Components;
using System.Drawing;
using System.Reflection;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class SecureUdpChannel:UdpChannel
    {
        EphemeralKeyManager keyStore;
        byte[] decryptBuff = new byte[65555];
        private int keyRotateTimeMs = 60000;//every minute

        public SecureUdpChannel(Socket udpSocket, IPEndPoint receiveEp, ConcurrentAesAlgorithm algo, ChannelInfo info,bool isInitiator) : base(udpSocket, receiveEp, info)
        {
            keyStore = new EphemeralKeyManager(algo, isInitiator? keyRotateTimeMs : -1);
            keyStore.SendData += SendKeyMsg;


            SenderModule sender = new SenderModule();
            sender.MaxSegmentSize = 1280;
            sender.MinWindowSize = 1280 * 2;
            
        }

        private void SendKeyMsg(MessageFlags flag, byte[] buffer, int offset, int count)
        {
            SendInternalReliable(flag, buffer, offset, count);
        }

        protected override void BytesReceived(byte[] buffer, int offset, int count)
        {
            var flag = (MessageFlags)buffer[offset++];
            count--;

            if (flag == MessageFlags.HP || flag == MessageFlags.HPAck)
                return;

            var keyNo = buffer[offset++];
            count--;
            
            keyStore.GetAlgorithm(keyNo, out var algo);

            count = algo.DecryptInto(buffer, offset, count, decryptBuff, 0);
            buffer = decryptBuff;
            offset = 0;

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

                case MessageFlags.InternalReliableMessage:
                    HandleIncomingInternalRudpSegment(buffer, offset, count);
                    break;

                case MessageFlags.KeepAliveMessage:
                    break;

                case MessageFlags.HP:
                case MessageFlags.HPAck:
                    break;
                case MessageFlags.Ping:
                    break;

                case MessageFlags.KeyExchange:
                case MessageFlags.KeyExchangeAck:
                case MessageFlags.KeyExchangeFin:
                    keyStore.HandleMessage(flag, buffer, offset, count);
                    break;
            }
        }

        protected override void HandleInternalReliableMessage(byte[] buffer, int offset, int count)
        {
            var flag = (MessageFlags)buffer[offset++];
            count--;

            switch (flag)
            {
                case MessageFlags.KeyExchange:
                case MessageFlags.KeyExchangeAck:
                case MessageFlags.KeyExchangeFin:
                    keyStore.HandleMessage(flag, buffer, offset, count);
                    break;
            }
        }

       
        protected override void SendWithFlag (MessageFlags flag, byte[] buffer, int offset, int count)
        {
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.WriteByte((byte)flag);
            stream.WriteByte(keyStore.CurrKeyNumber);

            stream.Reserve(count + 128);
            keyStore.GetAlgorithm(keyStore.CurrKeyNumber, out var algo);
            stream.Position32 += algo.EncryptInto(buffer, offset, count, stream.GetBuffer(), 2);

            SendInternal(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

    }

}
