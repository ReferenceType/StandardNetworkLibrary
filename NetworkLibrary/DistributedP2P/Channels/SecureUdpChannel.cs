using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto.Algorithms;
using NetworkLibrary.DistributedP2P.Channels.Components;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.UDP.Reliable.Components;
using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Sockets;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class SecureUdpChannel : UdpChannel
    {
        EphemeralKeyManager keyManager;
        byte[] decryptBuff = new byte[65555];
        private readonly bool isInitiator;

        public int KeyRotationPeriodMs { get; private set; } = 1000;//every minute

        public SecureUdpChannel(Socket udpSocket, IPEndPoint receiveEp, IAesAlgorithm algo, ChannelInfo info, bool isInitiator, ILogger logger) : base(udpSocket, receiveEp, info, logger)
        {
            keyManager = new EphemeralKeyManager(algo, isInitiator ? KeyRotationPeriodMs : -1);
            keyManager.SendData += SendKeyMsg;


            SenderModule sender = new SenderModule();
            sender.MaxSegmentSize = 1280;
            sender.MinWindowSize = 1280 * 2;
            this.isInitiator = isInitiator;
        }

        public void SetKeyRotationPeriod(int timeMs)
        {
            if (isInitiator)
            {
                KeyRotationPeriodMs = timeMs;
                keyManager.SetKeyRotationTime(timeMs);
            }
        }

        private void SendKeyMsg(MessageFlags flag, byte[] buffer, int offset, int count)
        {
            SendInternalReliable(flag, buffer, offset, count);
        }

        protected override void BytesReceived(byte[] buffer, int offset, int count)
        {
            var flag = (MessageFlags)buffer[offset++];

            if (flag == MessageFlags.HP || flag == MessageFlags.HPAck)
                return;

            var keyNo = buffer[offset++];
            count -= 2;

            keyManager.GetAlgorithm(keyNo, out var algo);

            try
            {
                count = algo.DecryptInto(buffer, offset, count, decryptBuff, 0);
            }
            catch
            { 
                Log(LogType.Error, "Decryption failed");
                return;
            }
           
            buffer = decryptBuff;
            offset = 0;

            HandleReivedMessage(buffer, offset, count, flag);
        }

        protected override void HandleReivedMessage(byte[] buffer, int offset, int count, MessageFlags flag)
        {
            switch (flag)
            {
                case MessageFlags.KeyExchange:
                case MessageFlags.KeyExchangeAck:
                case MessageFlags.KeyExchangeFin:
                    HandleKeyMessage(buffer, offset, count, flag);
                    break;
            }

            base.HandleReivedMessage(buffer, offset, count, flag);
        }

        private void HandleKeyMessage(byte[] buffer, int offset, int count, MessageFlags flag)
        {
            try
            {
                keyManager.HandleMessage(flag, buffer, offset, count);
            }
            catch (Exception e)
            {
                Log(e);
                CloseChannel();
            }

        }

        protected override void SendWithFlag(MessageFlags flag, byte[] buffer, int offset, int count)
        {
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.WriteByte((byte)flag);
            stream.WriteByte(keyManager.CurrKeyNumber);

            stream.Reserve(count + 128);
            keyManager.GetAlgorithm(keyManager.CurrKeyNumber, out var algo);
            stream.Position32 += algo.EncryptInto(buffer, offset, count, stream.GetBuffer(), 2);

            SendInternal(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

        protected override void ReleseResources()
        {
            base.ReleseResources();
            keyManager.Close();
        }

    }

}
