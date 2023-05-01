using MessageProtocol.Serialization;
using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Xml.Schema;

namespace Serialization
{
    public class EnvelopeSerializer
    { //w2
        public static void Serialize(PooledMemoryStream stream, MessageEnvelope envelope)
        {
            byte index = 0;
            // Reserve for index
            var oldPos = stream.Position32;
            stream.WriteByte(index);

            if (envelope.IsInternal)
            {
                stream.WriteByte(1);
                index = 1;
            }
            if (envelope.To != Guid.Empty)
            {
                PrimitiveEncoder.WriteGuid(stream, envelope.To);
                index += 2;
            }

            if (envelope.From != Guid.Empty)
            {
                PrimitiveEncoder.WriteGuid(stream, envelope.From);
                index += 4;
            }

            if (envelope.MessageId != Guid.Empty)
            {
                PrimitiveEncoder.WriteGuid(stream, envelope.MessageId);
                index += 8;
            }

            if (envelope.TimeStamp.Ticks > 0)
            {
                PrimitiveEncoder.WriteFixedInt64(stream, envelope.TimeStamp.Ticks);
                //BinaryEncoder.WriteDatetime(stream, envelope.TimeStamp);
                index += 16;
            }

            if (envelope.Header != null)
            {
                PrimitiveEncoder.WriteStringUtf8(stream, envelope.Header);
                index += 32;
            }

            if (envelope.KeyValuePairs != null)
            {
                index += 64;
                PrimitiveEncoder.WriteInt32(stream, envelope.KeyValuePairs.Count);
                foreach (var item in envelope.KeyValuePairs)
                {
                    PrimitiveEncoder.WriteStringUtf8(stream, item.Key);
                    PrimitiveEncoder.WriteStringUtf8(stream, item.Value);
                }
            }
            var buf = stream.GetBuffer();
            buf[oldPos] = index;

        }

        public static MessageEnvelope Deserialize(PooledMemoryStream stream)
        {
            var index = stream.ReadByte();
            var envelope = new MessageEnvelope();
            if((index & 1) != 0)
            {
                stream.Position32++;
                envelope.IsInternal = true;
            }
           

            if ((index & (1 << 1)) != 0)
            {
                envelope.To = PrimitiveEncoder.ReadGuid(stream);
            }
            if ((index & (1 << 2)) != 0)
            {

                envelope.From = PrimitiveEncoder.ReadGuid(stream);
            }
            if ((index & (1 << 3)) != 0)
            {

                envelope.MessageId = PrimitiveEncoder.ReadGuid(stream);
            }

            if ((index & (1 << 4)) != 0)
            {
                envelope.TimeStamp = new DateTime(PrimitiveEncoder.ReadFixedInt64(stream));

            }
            if ((index & (1 << 5)) != 0)
            {
                envelope.Header = PrimitiveEncoder.ReadStringUtf8(stream);
            }
            if ((index & (1 << 6)) != 0)
            {
                var kvPairs = new Dictionary<string, string>();
                var DictCount = PrimitiveEncoder.ReadInt32(stream);
                while (DictCount-- > 0)
                {
                    kvPairs.Add(PrimitiveEncoder.ReadStringUtf8(stream), PrimitiveEncoder.ReadStringUtf8(stream));
                }
                envelope.KeyValuePairs = kvPairs;
            }

            return envelope;
        }
        public static MessageEnvelope Deserialize(byte[] buffer, int offset)
        {
            var index = buffer[offset++];
            var envelope = new MessageEnvelope();
            if ((index & 1) != 0)
            {
                offset++;
                envelope.IsInternal = true;
            }

            if ((index & (1 << 1)) != 0)
            {
                envelope.To = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            }
            if ((index & (1 << 2)) != 0)
            {
                envelope.From = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            }
            if ((index & (1 << 3)) != 0)
            {
                envelope.MessageId = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            }
            if ((index & (1 << 4)) != 0)
            {
                envelope.TimeStamp = new DateTime(PrimitiveEncoder.ReadFixedInt64(buffer, ref offset));
                //envelope.TimeStamp = BinaryEncoder.ReadDatetime(buffer, ref offset); 

            }
            if ((index & (1 << 5)) != 0)
            {
                envelope.Header = PrimitiveEncoder.ReadStringUtf8(buffer, ref offset);
            }
            if ((index & (1 << 6)) != 0)
            {
                var kvPairs = new Dictionary<string, string>();
                var DictCount = PrimitiveEncoder.ReadInt32(buffer, ref offset);
                while (DictCount-- > 0)
                {
                    kvPairs.Add(PrimitiveEncoder.ReadStringUtf8(buffer, ref offset), PrimitiveEncoder.ReadStringUtf8(buffer, ref offset));
                }
                envelope.KeyValuePairs = kvPairs;
            }

            return envelope;
        }

        public static RouterHeader DeserializeToRouterHeader(PooledMemoryStream stream)
        {
            var index = stream.ReadByte();

            var envelope = new RouterHeader();
            if ((index & 1) != 0)
            {
                stream.Position32++;
                envelope.IsInternal = true;
            }

            if ((index & (1 << 1)) != 0)
            {
                envelope.To = PrimitiveEncoder.ReadGuid(stream);
            }

            return envelope;
        }

        public static RouterHeader DeserializeToRouterHeader(byte[] buffer, int offset)
        {
            var index = buffer[offset++];

            var envelope = new RouterHeader();
            if ((index & 1) != 0)
            {
                offset++;
                envelope.IsInternal = true;
            }

            if ((index & (1 << 1)) != 0)
            {
                envelope.To = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            }

            return envelope;
        }

        //---------------------------------------------------------------------------------

        public static void Serialize<E>(PooledMemoryStream stream, E envelope) where E : IMessageEnvelope, new()
        {
            byte index = 0;
            // Reserve for index
            var oldPos = stream.Position32;
            stream.WriteByte(0);
          
            if (envelope.IsInternal)
            {
                stream.WriteByte(1);
                index = 1;
            }

            if (envelope.To != Guid.Empty)
            {
                PrimitiveEncoder.WriteGuid(stream, envelope.To);
                index += 2;
            }

            if (envelope.From != Guid.Empty)
            {
                PrimitiveEncoder.WriteGuid(stream, envelope.From);
                index += 4;
            }

            if (envelope.MessageId != Guid.Empty)
            {
                PrimitiveEncoder.WriteGuid(stream, envelope.MessageId);
                index += 8;
            }

            if (envelope.TimeStamp.Ticks > 0)
            {
                PrimitiveEncoder.WriteFixedInt64(stream, envelope.TimeStamp.Ticks);
                //BinaryEncoder.WriteDatetime(stream, envelope.TimeStamp);
                index += 16;
            }

            if (envelope.Header != null)
            {
                PrimitiveEncoder.WriteStringUtf8(stream, envelope.Header);
                index += 32;
            }

            if (envelope.KeyValuePairs != null)
            {
                index += 64;
                PrimitiveEncoder.WriteInt32(stream, envelope.KeyValuePairs.Count);
                foreach (var item in envelope.KeyValuePairs)
                {
                    PrimitiveEncoder.WriteStringUtf8(stream, item.Key);
                    PrimitiveEncoder.WriteStringUtf8(stream, item.Value);
                }
            }
            var buf = stream.GetBuffer();
            buf[oldPos] = index;

        }

        public static E Deserialize<E>(PooledMemoryStream stream) where E : IMessageEnvelope, new()
        {
            var index = stream.ReadByte();
            var envelope = new E();
            if ((index & 1) != 0)
            {
                stream.Position32++;
                envelope.IsInternal = true;
            }

            if ((index & (1 << 1)) != 0)
            {
                envelope.To = PrimitiveEncoder.ReadGuid(stream);
            }
            if ((index & (1 << 2)) != 0)
            {

                envelope.From = PrimitiveEncoder.ReadGuid(stream);
            }
            if ((index & (1 << 3)) != 0)
            {

                envelope.MessageId = PrimitiveEncoder.ReadGuid(stream);
            }

            if ((index & (1 << 4)) != 0)
            {
                envelope.TimeStamp = new DateTime(PrimitiveEncoder.ReadFixedInt64(stream));

            }
            if ((index & (1 << 5)) != 0)
            {
                envelope.Header = PrimitiveEncoder.ReadStringUtf8(stream);
            }
            if ((index & (1 << 6)) != 0)
            {
                var kvPairs = new Dictionary<string, string>();
                var DictCount = PrimitiveEncoder.ReadInt32(stream);
                while (DictCount-- > 0)
                {
                    kvPairs.Add(PrimitiveEncoder.ReadStringUtf8(stream), PrimitiveEncoder.ReadStringUtf8(stream));
                }
                envelope.KeyValuePairs = kvPairs;
            }

            return envelope;
        }
        public static E Deserialize<E>(byte[] buffer, int offset) where E : IMessageEnvelope, new()
        {
            var index = buffer[offset++];
            var envelope = new E();
            if ((index & 1) != 0)
            {
                offset++;
                envelope.IsInternal = true;
            }

            if ((index & (1 << 1)) != 0)
            {
                envelope.To = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            }
            if ((index & (1 << 2)) != 0)
            {
                envelope.From = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            }
            if ((index & (1 << 3)) != 0)
            {
                envelope.MessageId = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            }
            if ((index & (1 << 4)) != 0)
            {
                envelope.TimeStamp = new DateTime(PrimitiveEncoder.ReadFixedInt64(buffer, ref offset));
                //envelope.TimeStamp = BinaryEncoder.ReadDatetime(buffer, ref offset); 

            }
            if ((index & (1 << 5)) != 0)
            {
                envelope.Header = PrimitiveEncoder.ReadStringUtf8(buffer, ref offset);
            }
            if ((index & (1 << 6)) != 0)
            {
                var kvPairs = new Dictionary<string, string>();
                var DictCount = PrimitiveEncoder.ReadInt32(buffer, ref offset);
                while (DictCount-- > 0)
                {
                    kvPairs.Add(PrimitiveEncoder.ReadStringUtf8(buffer, ref offset), PrimitiveEncoder.ReadStringUtf8(buffer, ref offset));
                }
                envelope.KeyValuePairs = kvPairs;
            }

            return envelope;
        }

        public static R DeserializeToRouterHeader<R>(PooledMemoryStream stream) where R : IRouterHeader, new()
        {
            var index = stream.ReadByte();

            var envelope = new R();
            if ((index & 1) != 0)
            {
                stream.Position32++;
                envelope.IsInternal = true;
            }

            if ((index & (1 << 1)) != 0)
            {
                envelope.To = PrimitiveEncoder.ReadGuid(stream);
            }

            return envelope;
        }

        public static R DeserializeToRouterHeader<R>(byte[] buffer, int offset) where R : IRouterHeader, new()
        {
            var index = buffer[offset++];

            var envelope = new R();
            if ((index & 1) != 0)
            {
                offset++;
                envelope.IsInternal = true;
            }

            if ((index & (1 << 1)) != 0)
            {
                envelope.To = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            }

            return envelope;
        }
    }

}



