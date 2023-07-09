using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using System;
using System.Runtime.CompilerServices;


namespace NetworkLibrary.MessageProtocol
{
    public class GenericMessageSerializer<S> : IMessageSerialiser
       where S : ISerializer, new()
    {
        // private ConcurrentBag<PooledMemoryStream> streamPool = new ConcurrentBag<PooledMemoryStream>();
        [ThreadStatic]
        public static PooledMemoryStream serialisationStream;
        private readonly S Serializer;

        public GenericMessageSerializer()
        {
            Serializer = new S();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Serialize<T>(T record)
        {
            if (serialisationStream == null)
            {
                serialisationStream = new PooledMemoryStream();
            }

            Serializer.Serialize(serialisationStream, record);
            var buffer = serialisationStream.GetBuffer();
            var ret = ByteCopy.ToArray(buffer, 0, (int)serialisationStream.Position);

            serialisationStream.Clear();
            serialisationStream.Position32 = 0;
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
                return EnvelopeSerializer.Deserialize(buffer, offset + 2);
            }

            var envelope = EnvelopeSerializer.Deserialize(buffer, offset + 2);
            envelope.SetPayload(buffer, offset + payloadStartIndex, count - (payloadStartIndex));
            return envelope;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RouterHeader DeserialiseOnlyRouterHeader(byte[] buffer, int offset, int count)
        {
            return EnvelopeSerializer.DeserializeToRouterHeader(buffer, offset + 2);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] SerializeMessageEnvelope<T>(MessageEnvelope empyEnvelope, T payload)
        {
            if (serialisationStream == null)
            {
                serialisationStream = new PooledMemoryStream();
            }
            EnvelopeMessageWithInnerMessage(serialisationStream, empyEnvelope, payload);
            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

            serialisationStream.Clear();
            serialisationStream.Position32 = 0;
            return ret;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnvelopeMessageWithInnerMessage<T>(PooledMemoryStream serialisationStream, MessageEnvelope empyEnvelope, T innerMsg)
        {
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            EnvelopeSerializer.Serialize(serialisationStream, empyEnvelope);
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
        public void EnvelopeMessageWithInnerMessage(PooledMemoryStream serialisationStream, MessageEnvelope empyEnvelope, Action<PooledMemoryStream> serializationCallback)
        {
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            EnvelopeSerializer.Serialize(serialisationStream, empyEnvelope);
            int delta = serialisationStream.Position32 - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(delta);//msglen +2 

            if (serializationCallback == null)
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
            serializationCallback.Invoke(serialisationStream);


        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] SerializeMessageEnvelope(MessageEnvelope message)
        {
            return EnvelopeMessageWithBytes(message, message.Payload, message.PayloadOffset, message.PayloadCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] EnvelopeMessageWithBytes(MessageEnvelope empyEnvelope, byte[] payloadBuffer, int offset, int count)
        {
            if (serialisationStream == null)
            {
                serialisationStream = new PooledMemoryStream();
            }

            EnvelopeMessageWithBytes(serialisationStream, empyEnvelope, payloadBuffer, offset, count);
            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

            serialisationStream.Clear();
            serialisationStream.Position32 = 0;
            return ret;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnvelopeMessageWithBytes(PooledMemoryStream serialisationStream, MessageEnvelope envelope, byte[] payloadBuffer, int offset, int count)
        {
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            EnvelopeSerializer.Serialize(serialisationStream, envelope);
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
                serialisationStream.Position32 += pos;
                return;
            }
            var pos1 = serialisationStream.Position32;
            serialisationStream.Position32 = originalPos;
            serialisationStream.WriteUshortUnchecked(oldpos);
            serialisationStream.Position32 = pos1;

            serialisationStream.Write(payloadBuffer, offset, count);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnvelopeMessageWithBytesDontWritePayload(PooledMemoryStream serialisationStream, MessageEnvelope envelope, int payloadCount)
        {
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            EnvelopeSerializer.Serialize(serialisationStream, envelope);
            int delta = serialisationStream.Position32 - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(delta);//msglen +2 


            var pos1 = serialisationStream.Position32;
            serialisationStream.Position32 = originalPos;
            if(payloadCount>0)
                serialisationStream.WriteUshortUnchecked(oldpos);
            else
                serialisationStream.WriteTwoZerosUnchecked();

            serialisationStream.Position32 = pos1;

            //serialisationStream.Write(payloadBuffer, offset, count);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void EnvelopeMessageWithBytesDontWritePayload(byte[] buffer,ref int offset, MessageEnvelope envelope, int payloadCount)
        {
            int originalPos = 0;
            offset += 2;

            EnvelopeSerializer.Serialize(buffer,ref offset, envelope);
            int delta = offset - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(delta);//msglen +2 

            fixed (byte* b = &buffer[originalPos])
                *(short*)b = (short)oldpos;

            if (payloadCount > 0)
            {
                fixed (byte* b = &buffer[originalPos])
                    *(short*)b = (short)oldpos;
            }

            else
            {
                fixed (byte* b = &buffer[originalPos])
                    *(short*)b = 0;
            }
               
         

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void EnvelopeMessageWithBytesDontWritePayload(byte* buffer, ref int offset, MessageEnvelope envelope, int payloadCount)
        {
            int originalPos = 0;
            offset += 2;

            EnvelopeSerializer.Serialize(buffer, ref offset, envelope);
            int delta = offset - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(delta);//msglen +2 

           
            if (payloadCount > 0)
            {
                byte* b = buffer + originalPos;
                *(short*)b = (short)oldpos;
            }

            else
            {
                byte* b = buffer + originalPos;
                *(short*)b = 0;
            }
        }


    }
}
