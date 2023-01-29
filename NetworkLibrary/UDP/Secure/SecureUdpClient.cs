using NetworkLibrary.Components;
using NetworkLibrary.Utils;
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
            var decryptBuffer = BufferPool.RentBuffer(count+256);
            try
            {
                var decriptedAmount = algorithm.DecryptInto(buffer, offset, count, decryptBuffer, 0);
                HandleDecrypedBytes(decryptBuffer, 0, decriptedAmount);
            }
            catch (Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error,nameof(SecureUdpClient) + " Encountered an error whie handling incoming bytes: " +e.Message);
            }
            finally
            {
                BufferPool.ReturnBuffer(decryptBuffer);
            }
        }

        protected virtual void HandleDecrypedBytes(byte[] buffer,int offset,int amount)
        {
            base.HandleBytesReceived(buffer, offset, amount);
        }

        public override void SendAsync(byte[] bytes, int offset, int count)
        {
            var buffer = BufferPool.RentBuffer(count+256);
            try
            {
                int amount = algorithm.EncryptInto(bytes, offset, count, buffer, 0);
                base.SendAsync(buffer, 0, amount);

            }
            catch(Exception ex)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "AnErroroccured while sending udp message: " + ex.Message);
            }
            finally { BufferPool.ReturnBuffer(buffer);}
 
        }
       

    }
}
