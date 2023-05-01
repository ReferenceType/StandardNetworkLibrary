using MessageProtocol.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary.Components;
using Serialization;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class SerializerTests
    {
        [TestMethod]
        public void PrimitiveTest()
        {
            PooledMemoryStream stream =  new PooledMemoryStream();
            PrimitiveEncoder.WriteBool(stream, true);
            PrimitiveEncoder.WriteBool(stream, false);
            PrimitiveEncoder.WriteChar(stream, '/');
            PrimitiveEncoder.WriteByte(stream, 22);

            PrimitiveEncoder.WriteUint16(stream, 44);
            PrimitiveEncoder.WriteUInt32(stream, 13213213);
            PrimitiveEncoder.WriteUInt64(stream, 12345);

            PrimitiveEncoder.WriteInt16(stream, -11111);
            PrimitiveEncoder.WriteInt32(stream, -222222222);
            PrimitiveEncoder.WriteInt64(stream, -3333333333333);

            PrimitiveEncoder.WriteFloat(stream, 12.666f);
            PrimitiveEncoder.WriteDouble(stream, 12.666d);
            PrimitiveEncoder.WriteDecimal(stream, (decimal)12.666);
            PrimitiveEncoder.WriteStringASCII(stream, "Hello");
            PrimitiveEncoder.WriteStringASCII2(stream, "Hello");
            PrimitiveEncoder.WriteStringUtf8(stream, "Hello");

            var buffer = stream.GetBuffer();
            int offset = 0;

         
            Assert.IsTrue(true == PrimitiveEncoder.ReadBool(buffer, ref offset));
            Assert.IsTrue(false == PrimitiveEncoder.ReadBool(buffer, ref offset));
            Assert.IsTrue('/' == PrimitiveEncoder.ReadChar(buffer, ref offset));
            Assert.IsTrue(22 == PrimitiveEncoder.ReadByte(buffer, ref offset));

            Assert.IsTrue(44 == PrimitiveEncoder.ReadUInt16(buffer, ref offset));
            Assert.IsTrue(13213213 == PrimitiveEncoder.ReadUInt32(buffer, ref offset));
            Assert.IsTrue(12345 == PrimitiveEncoder.ReadUInt64(buffer, ref offset));

            Assert.IsTrue(-11111 == PrimitiveEncoder.ReadInt16(buffer, ref offset));
            Assert.IsTrue(-222222222 == PrimitiveEncoder.ReadInt32(buffer, ref offset));
            Assert.IsTrue(-3333333333333 == PrimitiveEncoder.ReadInt64(buffer, ref offset));

            Assert.IsTrue(12.666f == PrimitiveEncoder.ReadFloat(buffer, ref offset));
            Assert.IsTrue(12.666d == PrimitiveEncoder.ReadDouble(buffer, ref offset));
            Assert.IsTrue((decimal)12.666 == PrimitiveEncoder.ReadDecimal(buffer, ref offset));
            Assert.IsTrue("Hello" == PrimitiveEncoder.ReadStringASCII(buffer, ref offset));
            Assert.IsTrue("Hello" == PrimitiveEncoder.ReadStringASCII(buffer, ref offset));
            Assert.IsTrue("Hello" == PrimitiveEncoder.ReadStringUtf8(buffer, ref offset));

            stream.Position = 0;

            Assert.IsTrue(true == PrimitiveEncoder.ReadBool(stream));
            Assert.IsTrue(false == PrimitiveEncoder.ReadBool(stream));
            Assert.IsTrue('/' == PrimitiveEncoder.ReadChar(stream));
            Assert.IsTrue(22 == PrimitiveEncoder.ReadByte(stream));

            Assert.IsTrue(44 == PrimitiveEncoder.ReadUInt16(stream));
            Assert.IsTrue(13213213 == PrimitiveEncoder.ReadUInt32(stream));
            Assert.IsTrue(12345 == PrimitiveEncoder.ReadUInt64(stream));

            Assert.IsTrue(-11111 == PrimitiveEncoder.ReadInt16(stream));
            Assert.IsTrue(-222222222 == PrimitiveEncoder.ReadInt32(stream));
            Assert.IsTrue(-3333333333333 == PrimitiveEncoder.ReadInt64(stream));

            Assert.IsTrue(12.666f == PrimitiveEncoder.ReadFloat(stream));
            Assert.IsTrue(12.666d == PrimitiveEncoder.ReadDouble(stream));
            Assert.IsTrue((decimal)12.666 == PrimitiveEncoder.ReadDecimal(stream));
            Assert.IsTrue("Hello" == PrimitiveEncoder.ReadStringASCII(stream));
            Assert.IsTrue("Hello" == PrimitiveEncoder.ReadStringASCII(stream));
            Assert.IsTrue("Hello" == PrimitiveEncoder.ReadStringUtf8(stream));

        }


        [TestMethod]
        public void MessageEnvelope()
        {
            MessageEnvelope env = new MessageEnvelope()
            {
                IsInternal = true,
                From = Guid.NewGuid(),
                To = Guid.NewGuid(),
                Header = "rattatta",

                MessageId = Guid.NewGuid(),
                TimeStamp = DateTime.Now,
                KeyValuePairs = new Dictionary<string, string>() {
                    { "K1", "v2" } ,
                    { "K3", "" },
                    { "K2", null } ,
                    { "K4", "%%" } ,
                }
            };

            MessageEnvelope env2 = new MessageEnvelope()
            {
                IsInternal = true,
                From = Guid.NewGuid(),

                Header = "rattatta€!£$%^&",

                MessageId = Guid.NewGuid(),
                TimeStamp = DateTime.Now,
                KeyValuePairs = new Dictionary<string, string>() {
                    { "K1", "v2" } ,
                    { "K3", "" },
                    { "K2", null } ,
                    { "K4", "%%" } ,
                }
            };

            MessageEnvelope env3 = new MessageEnvelope()
            {
                IsInternal = true,
                From = Guid.Empty,

                Header = "rattatta€!£$%^&",

                MessageId = Guid.NewGuid(),
                KeyValuePairs = new Dictionary<string, string>() {
                    { "K1", "v2" } ,
                    { "K3", "  " },
                    { "K2", null } ,
                    { "K4", "%%" } ,
                }
            };

            MessageEnvelope env4 = new MessageEnvelope()
            {
                IsInternal = true,
                From = Guid.Empty,

                Header = "rattatta€!£$%^&",

                MessageId = Guid.NewGuid(),
                TimeStamp = DateTime.Now,
                KeyValuePairs = new Dictionary<string, string>() {
                   
                }
            };

            MessageEnvelope env5 = new MessageEnvelope()
            {
                IsInternal = false,
                From = Guid.Empty,

                Header = " ",

                MessageId = Guid.NewGuid(),
                TimeStamp = DateTime.Now,
               
            };

            MessageEnvelope env6 = new MessageEnvelope()
            {
                IsInternal = false
            };
            MessageEnvelope env7 = new MessageEnvelope();


            Assert.IsTrue(AssertSerializer(env));
            Assert.IsTrue(AssertSerializer(env2));
            Assert.IsTrue(AssertSerializer(env3));
            Assert.IsTrue(AssertSerializer(env4));
            Assert.IsTrue(AssertSerializer(env5));
            Assert.IsTrue(AssertSerializer(env6));
            Assert.IsTrue(AssertSerializer(env7));
        }

        private bool AssertSerializer(MessageEnvelope sample)
        {
           TestSerializer(sample, out var result1, out var result2);
           return IsEqualMemberwise(sample, result1)&&IsEqualMemberwise(sample, result2);
        }
        

        private void TestSerializer(in MessageEnvelope sample, out MessageEnvelope result1, out MessageEnvelope result2)
        {
            PooledMemoryStream stream = new PooledMemoryStream();
            EnvelopeSerializer.Serialize(stream, sample);

            result1 = EnvelopeSerializer.Deserialize(stream.GetBuffer(), 0);
            stream.Position = 0;
            result2 = EnvelopeSerializer.Deserialize(stream);

        }
        bool IsEqualMemberwise(MessageEnvelope env1, MessageEnvelope env2)
        {
            bool primitiveOk =
                env1.IsInternal == env2.IsInternal &&
                env1.From == env2.From &&
                env1.To == env2.To &&
                env1.MessageId == env2.MessageId &&
                env1.TimeStamp == env2.TimeStamp &&
                env2.Header == env2.Header;

            bool dictOk =
                env2.KeyValuePairs == env2.KeyValuePairs;
            return primitiveOk && dictOk;
        }
    }
}
