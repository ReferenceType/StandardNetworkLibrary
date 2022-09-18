using NetworkLibrary.Components;
using NetworkLibrary.TCP.ByteMessage;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace NetworkLibrary.TCP.SSL.Custom
{
    internal class CustomSslSession : ByteMessageSession
    {
        AesEncryptor encryptor;

        internal CustomSslSession(SocketAsyncEventArgs acceptedArg, Guid sessionId, BufferProvider bufferManager,byte[] aesKey) : base(acceptedArg, sessionId, bufferManager)
        {
            encryptor = new AesEncryptor(aesKey, aesKey);
        }

        public override void SendAsync(byte[] bytes)
        {
           
            base.SendAsync(encryptor.Encrypt(bytes));
        }

       
        protected override void HandleMessage(byte[] buffer, int offset, int count)
        {
           // here we are sure that encypted message is more or equal than original message.
            var decriptedAmount = encryptor.DecryptInto(buffer, offset, count,buffer,offset);
            base.HandleMessage(buffer, offset, decriptedAmount);
        }
    }
}
