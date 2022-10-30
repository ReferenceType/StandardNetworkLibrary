using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.Components.MessageProcessor.Unmanaged;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Text;

namespace NetworkLibrary.TCP.SSL.ByteMessage
{
    internal class SslByteMessageSession : SslSession
    {
        ByteMessageReader reader;

        public SslByteMessageSession(Guid sessionId, SslStream sessionStream, BufferProvider bufferProvider) : base(sessionId, sessionStream, bufferProvider)
        {
            reader = new ByteMessageReader(sessionId);
            reader.OnMessageReady += HandleMessage;
        }

        private void HandleMessage(byte[] arg1, int arg2, int arg3)
        {
            base.HandleReceived(arg1, arg2, arg3);
        }
        protected override void HandleReceived(byte[] buffer, int offset, int count)
        {
            reader.ParseBytes(buffer, offset, count);
        }

        protected override IMessageProcessQueue CreateMessageQueue()
        {
            if (UseQueue)
                return new MessageQueue<DelimitedMessageWriter>(MaxIndexedMemory, new DelimitedMessageWriter());

            else
                return new MessageBuffer(MaxIndexedMemory, writeLengthPrefix: true);



            //if (CoreAssemblyConfig.UseUnmanaged)
            //    return new MessageQueue<UnsafeDelimitedMessageWriter>(MaxIndexedMemory, new UnsafeDelimitedMessageWriter());
            //else
            //    //return new MessageQueue<DelimitedMessageWriter>(MaxIndexedMemory, new DelimitedMessageWriter());
            //    return new MessageBuffer(MaxIndexedMemory);
        }

    }
}
