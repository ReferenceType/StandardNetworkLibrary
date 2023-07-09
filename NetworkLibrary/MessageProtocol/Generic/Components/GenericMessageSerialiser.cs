using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace NetworkLibrary.MessageProtocol
{
    public class GenericMessageSerializer<E, S> : IMessageSerialiser<E>
        where S : ISerializer, new()
        where E : IMessageEnvelope, new()
    {
        private ConcurrentBag<PooledMemoryStream> streamPool = new ConcurrentBag<PooledMemoryStream>();
        private readonly S Serializer;

        public GenericMessageSerializer()
        {
            Serializer = new S();
            streamPool.Add(new PooledMemoryStream());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Serialize<T>(T record)
        {
            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
            {
                serialisationStream = new PooledMemoryStream();
            }

            Serializer.Serialize(serialisationStream, record);
            var buffer = serialisationStream.GetBuffer();
            var ret = ByteCopy.ToArray(buffer, 0, (int)serialisationStream.Position32);

            serialisationStream.Clear();
            streamPool.Add(serialisationStream);
            return ret;

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Deserialize<T>(byte[] data, int offset, int count)
        {
            if (data == null || count == 0)
                return default;

            return Serializer.Deserialize<T>(data, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T UnpackEnvelopedMessage<T>(E fullEnvelope)
        {
            return Deserialize<T>(fullEnvelope.Payload, fullEnvelope.PayloadOffset, fullEnvelope.PayloadCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E DeserialiseEnvelopedMessage(byte[] buffer, int offset, int count)
        {
            ushort payloadStartIndex = BitConverter.ToUInt16(buffer, offset);

            if (payloadStartIndex == 0)
            {
                return Serializer.Deserialize<E>(buffer, offset + 2, count - 2);
            }

            var envelope = Serializer.Deserialize<E>(buffer, offset + 2, payloadStartIndex - 2);
            envelope.SetPayload(buffer, offset + payloadStartIndex, count - (payloadStartIndex));
            return envelope;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public R DeserialiseOnlyRouterHeader<R>(byte[] buffer, int offset, int count) where R : IRouterHeader, new()
        {
            ushort secondStart = BitConverter.ToUInt16(buffer, offset + 0);
            if (secondStart == 0)
            {
                return Serializer.Deserialize<R>(buffer, offset + 2, count - 2);
            }

            var envelope = Serializer.Deserialize<R>(buffer, offset + 2, secondStart - 2);
            return envelope;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] SerializeMessageEnvelope<T>(E empyEnvelope, T payload)
        {
            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
            {
                serialisationStream = new PooledMemoryStream();
            }
            EnvelopeMessageWithInnerMessage(serialisationStream, empyEnvelope, payload);
            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position32);

            serialisationStream.Clear();
            streamPool.Add(serialisationStream);
            return ret;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnvelopeMessageWithInnerMessage<T>(PooledMemoryStream serialisationStream, E envelope, T innerMsg)
        {
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            Serializer.Serialize(serialisationStream, envelope);
            int delta = serialisationStream.Position32 - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(delta);//msglen +2 

            if (innerMsg == null)
            {
                var pos = serialisationStream.Position32;
                serialisationStream.Position32 = originalPos;
                serialisationStream.WriteTwoZerosUnchecked();
                serialisationStream.Position32 = pos;
                return;

            }

            var pos1 = serialisationStream.Position32;
            serialisationStream.Position32 = originalPos;
            serialisationStream.WriteUshortUnchecked(oldpos);
            serialisationStream.Position32 = pos1;
            Serializer.Serialize(serialisationStream, innerMsg);


        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] SerializeMessageEnvelope(E message)
        {
            return EnvelopeMessageWithBytes(message, message.Payload, message.PayloadOffset, message.PayloadCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] EnvelopeMessageWithBytes(E empyEnvelope, byte[] payloadBuffer, int offset, int count)
        {
            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
            {
                serialisationStream = new PooledMemoryStream();
            }

            EnvelopeMessageWithBytes(serialisationStream, empyEnvelope, payloadBuffer, offset, count);
            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position32);

            serialisationStream.Clear();
            streamPool.Add(serialisationStream);
            return ret;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnvelopeMessageWithBytes(PooledMemoryStream serialisationStream, E envelope, byte[] payloadBuffer, int offset, int count)
        {
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            Serializer.Serialize(serialisationStream, envelope);
            int delta = serialisationStream.Position32 - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(delta);//msglen +2 

            if (count == 0)
            {
                var pos = serialisationStream.Position32;
                serialisationStream.Position32 = originalPos;
                serialisationStream.WriteTwoZerosUnchecked();
                serialisationStream.Position32 = pos;
                return;
            }
            var pos1 = serialisationStream.Position32;
            serialisationStream.Position32 = originalPos;
            serialisationStream.WriteUshortUnchecked(oldpos);
            serialisationStream.Position32 = pos1;

            serialisationStream.Write(payloadBuffer, offset, count);
        }


    }
}


