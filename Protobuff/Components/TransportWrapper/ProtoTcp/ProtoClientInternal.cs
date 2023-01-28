﻿using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net;
using System.Text;
using System.Net.Sockets;

namespace Protobuff.Components.ProtoTcp
{
    internal class ProtoClientInternal:AsyncTpcClient
    {
        public Action<MessageEnvelope> OnMessageReceived;
        public bool DeserializeMessages = true;
        private ConcurrentProtoSerialiser serializer = new ConcurrentProtoSerialiser();
        private ProtoSessionInternal protoSession;

        protected virtual void MapReceivedBytes()
        {
            OnBytesReceived += HandleBytes;
        }
        private void HandleBytes(byte[] bytes, int offset, int count)
        {
            var msg = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
            OnMessageReceived?.Invoke(msg);
        }
        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var ses = new ProtoSessionInternal(e, sessionId);
            ses.socketRecieveBufferSize = SocketRecieveBufferSize;
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.DropOnCongestion = DropOnCongestion;
            ses.OnSessionClosed += (id) => OnDisconnected?.Invoke();
            protoSession= ses;

            if (DeserializeMessages)
                MapReceivedBytes();
            return ses;
        }

      
        public void SendAsyncMessage(MessageEnvelope message)
        {
            if (protoSession != null)
                protoSession.SendAsync(message);

        }
        public void SendAsyncMessage<T>(MessageEnvelope envelope, T message) where T : IProtoMessage
        {
            if (protoSession != null)
                protoSession.SendAsync(envelope, message);

        }
    }
}
