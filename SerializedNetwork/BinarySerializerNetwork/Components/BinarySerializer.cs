using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace BinarySerializerNetwork.Components
{
    public class BinarySerializer : ISerializer
    {
        private BinaryFormatter serializer;

        public BinarySerializer()
        {
             serializer =  new BinaryFormatter();
        }

        public T Deserialize<T>(Stream source)
        {
           return (T)serializer.Deserialize(source);
        }

        public T Deserialize<T>(byte[] buffer, int offset, int count)
        {
            using (var stream =  new MemoryStream(buffer,offset,count))
                return (T)serializer.Deserialize(stream);
        }

        public MessageEnvelope Deserialize(byte[] buffer, int offset, int count)
        {
           return Deserialize<MessageEnvelope>(buffer, offset, count);
        }

        public void Serialize<T>(Stream destination, T instance)
        {
          //  var ms  = new MemoryStream();
           //serializer.Serialize(ms, instance);

           // var buf = ms.GetBuffer();
           // destination.Write(buf, 0, buf.Length);
           serializer.Serialize(destination, instance);
          // destination.Position = destination.Length;
        }

        public byte[] Serialize<T>(T instance)
        {
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream,instance);
                return stream.ToArray();
            }

             
        }

        public void Serialize(Stream destination, MessageEnvelope instance)
        {
            Serialize<MessageEnvelope>(destination, instance);
        }
    }
}
