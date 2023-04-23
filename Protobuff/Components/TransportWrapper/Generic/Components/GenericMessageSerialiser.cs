//using NetworkLibrary.Components;
//using NetworkLibrary.Utils;
//using Protobuff.Components.Serialiser;
//using Protobuff.Components.TransportWrapper.Generic.Interfaces;
//using System;
//using System.Collections.Concurrent;
//using System.IO;
//using System.Runtime.CompilerServices;

//namespace Protobuff
//{
//    public class GenericMessageSerializer<S> : IMessageSerialiser where S : ISerializer, new()
//    {
//        private ConcurrentBag<PooledMemoryStream> streamPool = new ConcurrentBag<PooledMemoryStream>();
//        private readonly S Serializer;

//        public GenericMessageSerializer()
//        {
//            Serializer = new S();
//            streamPool.Add(new PooledMemoryStream());
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public byte[] Serialize<T>(T record)
//        {
//            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
//            {
//                serialisationStream = new PooledMemoryStream();
//            }

//            Serializer.Serialize(serialisationStream, record);
//            var buffer = serialisationStream.GetBuffer();
//            var ret = ByteCopy.ToArray(buffer, 0, (int)serialisationStream.Position);

//            serialisationStream.Flush();
//            streamPool.Add(serialisationStream);
//            return ret;

//        }



//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public T Deserialize<T>(byte[] data, int offset, int count)
//        {
//            if (data == null || count == 0)
//                return default;

//            return Serializer.Deserialize<T>(data, offset, count);
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public T UnpackEnvelopedMessage<E, T>(E fullEnvelope) where E : IMessageEnvelope
//        {
//            return Deserialize<T>(fullEnvelope.Payload, fullEnvelope.PayloadOffset, fullEnvelope.PayloadCount);
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public E DeserialiseEnvelopedMessage<E>(byte[] buffer, int offset, int count) where E : IMessageEnvelope
//        {
//            ushort payloadStartIndex = BitConverter.ToUInt16(buffer, offset);

//            if (payloadStartIndex == 0)
//            {
//                return Serializer.Deserialize<E>(buffer, offset + 2, count - 2);
//            }

//            var envelope = Serializer.Deserialize<E>(buffer, offset + 2, payloadStartIndex - 2);
//            envelope.SetPayload(buffer, offset + payloadStartIndex, count - payloadStartIndex);
//            return envelope;
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public R DeserialiseOnlyRouterHeader<R>(byte[] buffer, int offset, int count) where R : IRouterHeader
//        {
//            ushort secondStart = BitConverter.ToUInt16(buffer, offset + 0);
//            if (secondStart == 0)
//            {
//                return Serializer.Deserialize<R>(buffer, offset + 2, count - 2);
//            }

//            var envelope = Serializer.Deserialize<R>(buffer, offset + 2, secondStart - 2);
//            return envelope;
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public byte[] SerializeMessageEnvelope<E, T>(E empyEnvelope, T payload) where E : IMessageEnvelope
//        {
//            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
//            {
//                serialisationStream = new PooledMemoryStream();
//            }
//            EnvelopeMessageWithInnerMessage(serialisationStream, empyEnvelope, payload);
//            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

//            serialisationStream.Flush();
//            streamPool.Add(serialisationStream);
//            return ret;

//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public void EnvelopeMessageWithInnerMessage<E, T>(Stream serialisationStream, E empyEnvelope, T payload) where E : IMessageEnvelope
//        {
//            serialisationStream.Position = 2;
//            Serializer.Serialize(serialisationStream, empyEnvelope);

//            if (serialisationStream.Position >= ushort.MaxValue)
//            {
//                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
//            }
//            ushort oldpos = (ushort)serialisationStream.Position;//msglen +2

//            if (payload == null)
//            {
//                serialisationStream.Position = 0;
//                serialisationStream.Write(new byte[2], 0, 2);
//                serialisationStream.Position = oldpos;
//                return;
//            }

//            var envLen = BitConverter.GetBytes(oldpos);
//            serialisationStream.Position = 0;
//            serialisationStream.Write(envLen, 0, 2);
//            serialisationStream.Position = oldpos;

//            Serializer.Serialize(serialisationStream, payload);


//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public byte[] SerializeMessageEnvelope<E>(E message) where E : IMessageEnvelope
//        {
//            return EnvelopeMessageWithBytes(message, message.Payload, message.PayloadOffset, message.PayloadCount);
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public byte[] EnvelopeMessageWithBytes<E>(E empyEnvelope, byte[] payloadBuffer, int offset, int count) where E : IMessageEnvelope
//        {
//            if (!streamPool.TryTake(out PooledMemoryStream serialisationStream))
//            {
//                serialisationStream = new PooledMemoryStream();
//            }

//            EnvelopeMessageWithBytes(serialisationStream, empyEnvelope, payloadBuffer, offset, count);
//            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

//            serialisationStream.Flush();
//            streamPool.Add(serialisationStream);
//            return ret;

//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public void EnvelopeMessageWithBytes<E>(Stream serialisationStream, E empyEnvelope, byte[] payloadBuffer, int offset, int count) where E : IMessageEnvelope
//        {
//            serialisationStream.Position = 2;
//            Serializer.Serialize(serialisationStream, empyEnvelope);

//            if (serialisationStream.Position >= ushort.MaxValue)
//            {
//                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
//            }

//            ushort oldpos = (ushort)serialisationStream.Position;//msglen +2

//            if (count == 0)
//            {
//                serialisationStream.Position = 0;
//                serialisationStream.Write(new byte[2], 0, 2);
//                serialisationStream.Position = oldpos;
//                return;
//            }

//            var secondStartsAt = BitConverter.GetBytes(oldpos);
//            serialisationStream.Position = 0;
//            serialisationStream.Write(secondStartsAt, 0, 2);
//            serialisationStream.Position = oldpos;

//            serialisationStream.Write(payloadBuffer, offset, count);
//        }


//    }
//}

