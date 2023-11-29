using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.P2P.Generic.Room;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace NetworkLibrary.TCP.AES
{
    
    internal class AesTcpSession : TcpSession
    {
        ByteMessageReader msgParser;
        private EncryptedMessageBuffer mq;
        private PooledMemoryStream stream= new PooledMemoryStream();

        internal readonly ConcurrentAesAlgorithm Algorithm;

        public AesTcpSession(SocketAsyncEventArgs acceptedArg, Guid sessionId,ConcurrentAesAlgorithm algorithm) : base(acceptedArg, sessionId)
        {
            Algorithm = algorithm;
        }
        public AesTcpSession(Socket sessionSockt, Guid sessionId,ConcurrentAesAlgorithm algorithm): base(sessionSockt, sessionId) 
        {
            this.Algorithm = algorithm;
        }
        
        public override void StartSession()
        {
            msgParser = new ByteMessageReader(SocketRecieveBufferSize);
            msgParser.OnMessageReady += HandleMessage;

            base.StartSession();
        }

        protected virtual void HandleMessage(byte[] buffer, int offset, int count)
        {
            stream.Position32 = 0;
            stream.Reserve(count+256);
            var buff = stream.GetBuffer();
            int decr = Algorithm.DecryptInto(buffer, offset, count, buff, 0);
            base.HandleReceived(buff, 0, decr);
        }

        
        protected sealed override void HandleReceived(byte[] buffer, int offset, int count)
        {
            msgParser.ParseBytes(buffer, offset, count);
        }

        protected override IMessageQueue CreateMessageQueue()
        {
            mq= new EncryptedMessageBuffer(MaxIndexedMemory, Algorithm);
            return mq;
        }

        internal void SendAsync(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendOrEnqueue(data1, offset1, count1, data2, offset2, count2);
            }
            catch (Exception e)
            {
                if (!IsSessionClosing())
                    MiniLogger.Log(MiniLogger.LogLevel.Error,
                        "Unexcpected error while sending async with tcp session" + e.Message + "Trace " + e.StackTrace);
            }
        }

        void SendOrEnqueue(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
                ReleaseSendResourcesIdempotent();
            if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(data1, offset1, count1,data2,offset2,count2))
            {
                enqueueLock.Release();

                return;
            }
            enqueueLock.Release();

            if (DropOnCongestion && SendSemaphore.IsTaken())
                return;


            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                SendSemaphore.Release();
                return;
            }

            mq.TryEnqueueMessage(data1, offset1, count1, data2, offset2, count2);
            mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            FlushSendBuffer(0, amountWritten);

            return;
        }

    }
}
