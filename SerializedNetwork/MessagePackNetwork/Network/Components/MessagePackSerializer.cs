using MessagePack;
using MessageProtocol;
using NetworkLibrary.Components;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MessagePackNetwork.Network.Components
{
    public class MessagepackSerializer : ISerializer
    {
        [ThreadStatic]
        BufferWriter wrt = new BufferWriter();

        public MessagepackSerializer()
        {
          
        }

        public T Deserialize<T>(Stream source)
        {
            var buff = ((PooledMemoryStream)source).GetBuffer();
            return MessagePackSerializer.Deserialize<T>(new ReadOnlyMemory<byte>(buff,0,(int)source.Position));   
        }

        public T Deserialize<T>(byte[] buffer, int offset, int count)
        {
            return MessagePackSerializer.Deserialize<T>(new ReadOnlyMemory<byte>(buffer,offset,count));
        }

        public void Serialize<T>(Stream destination, T instance)
        {
            //wrt.SetStream((PooledMemoryStream)destination);
            var wrt = new BufferWriter((PooledMemoryStream)destination);
            //byte[] bytes =  MessagePackSerializer.Serialize(instance);
            //destination.Write(bytes, 0, bytes.Length);

            MessagePackSerializer.Serialize(wrt, instance);
        }

        public byte[] Serialize<T>(T instance)
        {
            return MessagePackSerializer.Serialize(instance);
        }
    }
}
