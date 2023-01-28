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
        AesAlgorithm encryptor;
        //AesDecryptor processor;
        private AesMessageDecryptor decr;

        internal CustomSslSession(SocketAsyncEventArgs acceptedArg, Guid sessionId,byte[] aesKey) : base(acceptedArg, sessionId)
        {
            encryptor = new AesAlgorithm(aesKey, aesKey);
            decr =new AesMessageDecryptor(encryptor);
        }

        public override void SendAsync(byte[] bytes)
        {
           
            base.SendAsync(bytes);
            //base.SendAsync(encryptor.Encrypt(bytes));
        }

       
        protected override void HandleMessage(byte[] buffer, int offset, int count)
        {
            // here we are sure that encypted message is more or equal than original message.
            var decriptedAmount = encryptor.DecryptInto(buffer, offset, count, buffer, offset);
            base.HandleMessage(buffer, offset, decriptedAmount);

            //byte[] data = new byte[count];
            //Buffer.BlockCopy(buffer, offset, data, 0, count);

            //decr.SetBuffer(ref buffer, offset);
            //decr.ProcessMessage(data);
            //base.HandleMessage(decr.Buffer, offset, decr.count);

            //var res = encryptor.Decrypt(buffer, offset, count);
            //base.HandleMessage(res, 0, res.Length);

            //byte[] data = new byte[count];
            //Buffer.BlockCopy(buffer, offset, data, 0, count);
            //var am = processor.ProcessMessage(data, buffer, offset);
            //base.HandleMessage(buffer, offset, am);
        }

        protected override IMessageQueue CreateMessageQueue()
        {
            var a = new AesMessageEncryptor(algorithm: encryptor);
            var Q = new MessageQueue<AesMessageEncryptor>(MaxIndexedMemory,a);


            return Q;
        }
    }
}
