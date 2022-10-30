using NetworkLibrary.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary.UDP.Secure
{
    public class SecureUdpClient:AsyncUdpClient
    {
        ConcurrentAesAlgorithm algorithm;
        ConcurrentBag<byte[]> bufferPool = new ConcurrentBag<byte[]>();

        readonly byte[] decryptBuffer = new byte[64000];
        private readonly object l=new object();
      //  readonly byte[] encryptBuffer = new byte[64000];
        public SecureUdpClient(ConcurrentAesAlgorithm algorithm, int port) : base(port)
        {
            this.algorithm = algorithm;
        }
        public SecureUdpClient(ConcurrentAesAlgorithm algorithm)
        {
            this.algorithm = algorithm;
        }
        public void SwapAlgorith(ConcurrentAesAlgorithm algorithm)
        {
            this.algorithm = algorithm;
        }
        protected override void HandleBytesReceived(byte[] buffer, int offset, int count)
        {
            lock (l)
            {
                var decriptedAmount = algorithm.DecryptInto(buffer, offset, count, decryptBuffer, 0);
                HandleDecrypedBytes(decryptBuffer, 0, decriptedAmount);
            }
           
        }
        protected virtual void HandleDecrypedBytes(byte[] buffer,int offset,int amount)
        {
            base.HandleBytesReceived(buffer, offset, amount);

        }

       public void SendAsyncWithPrefix(byte[] prefix, byte[] bytes, int offset, int count)
        {
            if (!bufferPool.TryTake(out var buffer))
            {
                buffer = new byte[64000];
            }
            Buffer.BlockCopy(prefix, 0, buffer, 0, prefix.Length);
            int amount = algorithm.EncryptInto(bytes, offset, count, buffer, prefix.Length);
            base.SendAsync(buffer, 0, amount+prefix.Length);
            bufferPool.Add(buffer);
        }

        public override void SendAsync(byte[] bytes, int offset, int count)
        {
            if (!bufferPool.TryTake(out var buffer))
            {
                buffer = new byte[64000];
            }
            int amount = algorithm.EncryptInto(bytes, offset, count, buffer, 0);
            base.SendAsync(buffer, 0, amount);
            bufferPool.Add(buffer);
        }
       

    }
}
