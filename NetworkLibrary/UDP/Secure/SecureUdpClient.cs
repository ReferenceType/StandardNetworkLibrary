using NetworkLibrary.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.UDP.Secure
{
    internal class SecureUdpClient:AsyncUdpClient
    {
        AesAlgorithm algorithm;

        public SecureUdpClient(AesAlgorithm algorithm, int port) : base(port)
        {
            this.algorithm = algorithm;
        }
        public SecureUdpClient(AesAlgorithm algorithm )
        {
            this.algorithm = algorithm;
        }

        protected override void HandleBytesReceived(byte[] buffer, int offset, int count)
        {
            var decriptedAmount = algorithm.DecryptInto(buffer, offset, count, buffer, offset);
            base.HandleBytesReceived(buffer, offset, decriptedAmount);
        }

        protected override void SendAsyncInternal(byte[] bytes)
        {
            bytes = algorithm.Encrypt(bytes);
            base.SendAsyncInternal(bytes);
        }

    }
}
