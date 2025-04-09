using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto;
using NetworkLibrary.DistributedP2P.Channels.Components;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.TCP.AES;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class SecureTcpChannel : TcpChannel
    {
        private readonly bool isInitiator;
        private EphemeralKeyManager keyManager;

        private byte[] encBuff;
        private byte[] decBuff;
        /// <summary>
        /// -1 means never rotate keys
        /// </summary>
        public int KeyRotationPeriodMs { get; private set; } = 1000;

        public SecureTcpChannel(ConcurrentAesAlgorithm algo,ChannelInfo info, Socket connectedSocket, bool isInitiator) : base(info, connectedSocket)
        {
            this.isInitiator = isInitiator;
            encBuff = BufferPool.RentBuffer(128000);
            decBuff = BufferPool.RentBuffer(128000);


            keyManager = new EphemeralKeyManager(algo, isInitiator ? KeyRotationPeriodMs : -1);
            keyManager.SendData += SendKeyMsg;

        }

        public void SetKeyRotationPeriod(int timeMs)
        {
            if (isInitiator)
            {
                KeyRotationPeriodMs = timeMs;
                keyManager.SetKeyRotationTime(timeMs);
            }
        }

        private void SendKeyMsg(MessageFlags flags, byte[] buffer, int offset, int count)
        {
            FlagAndSend(flags, buffer, offset, count);
        }

        protected override int WritePrefix(PooledMemoryStream sendStream)
        {
            sendStream.WriteByte(keyManager.CurrKeyNumber);
            return 1;
        }

        protected override int WriteData(byte[] buffer, int offset, int count)
        {
            EnsureCapacityEnc(count);

            keyManager.GetAlgorithm(keyManager.CurrKeyNumber, out var algo);
            int amountEnc = algo.EncryptInto(buffer, offset, count, encBuff, 0);

            return base.WriteData(encBuff, 0, amountEnc);
        }

        protected override void HandleReceivedBytes(byte[] buffer, int offset, int count)
        {
            var flag = (MessageFlags)buffer[offset++]; count--;

            if (flag == MessageFlags.HP || flag == MessageFlags.HPAck) return;

            var keyNo = buffer[offset++]; count--;

            keyManager.GetAlgorithm(keyNo, out var algo);

            EnsureCapacityDec(count);
            count = algo.DecryptInto(buffer, offset, count, decBuff, 0); // not sure if try catch this.
            buffer = decBuff;
            offset = 0;

            HandleReceivedMessage(buffer, offset, count, flag);
        }

        protected override void HandleReceivedMessage(byte[] buffer, int offset, int count, MessageFlags flag)
        {
            switch (flag)
            {
                case MessageFlags.KeyExchange:
                case MessageFlags.KeyExchangeAck:
                case MessageFlags.KeyExchangeFin:
                    keyManager.HandleMessage(flag, buffer, offset, count);
                    break;
            }
            base.HandleReceivedMessage(buffer, offset, count, flag);
        }

        private void EnsureCapacityEnc(int count)
        {
            if(encBuff.Length < count + 256)
            {
                BufferPool.ReturnBuffer(encBuff);
                encBuff = BufferPool.RentBuffer(count + 256);
            }
        }
        private void EnsureCapacityDec(int count)
        {
            if (decBuff.Length < count + 256)
            {
                BufferPool.ReturnBuffer(decBuff);
                decBuff = BufferPool.RentBuffer(count + 256);
            }
        }

    }
}
