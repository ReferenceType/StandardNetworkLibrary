using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;

namespace Protobuff
{
    public class ConcurrentProtoSerialiser
    {

        private ConcurrentBag<PooledMemoryStream> streamPool = new ConcurrentBag<PooledMemoryStream>();
        private static bool isWarmedUp = false;
        public ConcurrentProtoSerialiser()
        {
            streamPool.Add(new PooledMemoryStream());
            if (!isWarmedUp)
            {
                isWarmedUp = true;
                Serializer.PrepareSerializer<MessageEnvelope>();
                Serializer.PrepareSerializer<RouterHeader>();
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Serialize<T>(T record) where T : IProtoMessage
        {
            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
            {
                serialisationStream = new PooledMemoryStream();
            }

            Serializer.Serialize(serialisationStream, record);
            var buffer = serialisationStream.GetBuffer();
            var ret = ByteCopy.ToArray(buffer, 0, (int)serialisationStream.Position);

            serialisationStream.Clear();
            streamPool.Add(serialisationStream);
            return ret;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SerializeInto<T>(T record, ref byte[] buffer, int offset, out int count)
        {
            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
            {
                serialisationStream = new PooledMemoryStream();
            }
            Serializer.Serialize(serialisationStream, record);
            count = (int)serialisationStream.Position;
            serialisationStream.Clear();

            return true;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Deserialize<T>(byte[] data) 
        {
            if (null == data)
                return default(T);

            ReadOnlySpan<byte> seq = new ReadOnlySpan<byte>(data);
            return Serializer.Deserialize<T>(seq);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Deserialize<T>(byte[] data, int offset, int count) 
        {
            if (data == null || count == 0)
                return default;

            ReadOnlySpan<byte> seq = new ReadOnlySpan<byte>(data, offset, count);
            return Serializer.Deserialize<T>(seq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T UnpackEnvelopedMessage<T>(MessageEnvelope fullEnvelope) 
        {
            return Deserialize<T>(fullEnvelope.Payload, fullEnvelope.PayloadOffset, fullEnvelope.PayloadCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MessageEnvelope DeserialiseEnvelopedMessage(byte[] buffer, int offset, int count)
        {
            ushort payloadStartIndex = BitConverter.ToUInt16(buffer, offset);

            if (payloadStartIndex == 0)
            {
                ReadOnlySpan<byte> envelopeByes = new ReadOnlySpan<byte>(buffer, offset + 2, count - 2);
                return Serializer.Deserialize<MessageEnvelope>(envelopeByes);
            }

            ReadOnlySpan<byte> seq = new ReadOnlySpan<byte>(buffer, offset + 2, payloadStartIndex - 2);
            var envelope = Serializer.Deserialize<MessageEnvelope>(seq);
            envelope.SetPayload(buffer, offset + payloadStartIndex, count - payloadStartIndex);
            return envelope;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal RouterHeader DeserialiseOnlyRouterHeader(byte[] buffer, int offset, int count)
        {
            ushort secondStart = BitConverter.ToUInt16(buffer, offset + 0);
            if (secondStart == 0)
            {
                ReadOnlySpan<byte> seq0 = new ReadOnlySpan<byte>(buffer, offset + 2, count - 2);
                return Serializer.Deserialize<RouterHeader>(seq0);
            }

            ReadOnlySpan<byte> seq = new ReadOnlySpan<byte>(buffer, offset + 2, secondStart - 2);
            var envelope = Serializer.Deserialize<RouterHeader>(seq);
            return envelope;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte[] SerializeMessageEnvelope<T>(MessageEnvelope empyEnvelope, T payload) 
        {
            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
            {
                serialisationStream = new PooledMemoryStream();
            }
            EnvelopeMessageWithInnerMessage(serialisationStream, empyEnvelope, payload);
            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

            serialisationStream.Clear();
            streamPool.Add(serialisationStream);
            return ret;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnvelopeMessageWithInnerMessage<T>(Stream serialisationStream, MessageEnvelope empyEnvelope, T payload) 
        {
            serialisationStream.Position = 2;
            Serializer.Serialize(serialisationStream, empyEnvelope);

            if (serialisationStream.Position >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)serialisationStream.Position;//msglen +2

            if (payload == null)
            {
                serialisationStream.Position = 0;
                serialisationStream.Write(new byte[2], 0, 2);
                serialisationStream.Position = oldpos;
                return;
            }

            var envLen = BitConverter.GetBytes(oldpos);
            serialisationStream.Position = 0;
            serialisationStream.Write(envLen, 0, 2);
            serialisationStream.Position = oldpos;

            Serializer.Serialize(serialisationStream, payload);


        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte[] SerializeMessageEnvelope(MessageEnvelope message)
        {
            return EnvelopeMessageWithBytes(message, message.Payload, message.PayloadOffset, message.PayloadCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte[] EnvelopeMessageWithBytes(MessageEnvelope empyEnvelope, byte[] payloadBuffer, int offset, int count)
        {
            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
            {
                serialisationStream = new PooledMemoryStream();
            }

            EnvelopeMessageWithBytes(serialisationStream, empyEnvelope, payloadBuffer, offset, count);
            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

            serialisationStream.Clear();
            streamPool.Add(serialisationStream);
            return ret;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnvelopeMessageWithBytes(Stream serialisationStream, MessageEnvelope empyEnvelope, byte[] payloadBuffer, int offset, int count)
        {
            serialisationStream.Position = 2;
            Serializer.Serialize(serialisationStream, empyEnvelope);

            if (serialisationStream.Position >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }

            ushort oldpos = (ushort)serialisationStream.Position;//msglen +2

            if (count == 0)
            {
                serialisationStream.Position = 0;
                serialisationStream.Write(new byte[2], 0, 2);
                serialisationStream.Position = oldpos;
                return;
            }

            var secondStartsAt = BitConverter.GetBytes(oldpos);
            serialisationStream.Position = 0;
            serialisationStream.Write(secondStartsAt, 0, 2);
            serialisationStream.Position = oldpos;

            serialisationStream.Write(payloadBuffer, offset, count);
        }
    }
}

