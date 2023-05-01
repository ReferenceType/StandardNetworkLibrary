using NetSerializer;
using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;

namespace NetSerializerNetwork.Components
{
    public class NetSerialiser : NetworkLibrary.MessageProtocol.ISerializer
    {
        public T Deserialize<T>(Stream source)
        {
            UpdateModels(typeof(T));
            serializer.DeserializeDirect(source, out T value);
            return value;
        }

        public T Deserialize<T>(byte[] buffer, int offset, int count)
        {
            UpdateModels(typeof(T));

            var ms = new MemoryStream(buffer, offset, count);

            serializer.DeserializeDirect(ms, out T value);
            return value;


        }

        public void Serialize<T>(Stream destination, T instance)
        {
            UpdateModels(typeof(T));

            serializer.SerializeDirect(destination, instance);
        }

        public byte[] Serialize<T>(T instance)
        {
            UpdateModels(typeof(T));

            MemoryStream stream = new MemoryStream();
            serializer.SerializeDirect(stream, instance);
            return stream.ToArray();

        }

        static Dictionary<Type, uint> map;
        static Serializer serializer;
        public NetSerialiser()
        {
            if(serializer == null)
            {
                serializer = new Serializer(new List<Type> { typeof(MessageEnvelope) });
                map = serializer.GetTypeMap();
            }
           
        }
        private static readonly object locker =  new object();
        private void UpdateModels(Type t)
        {
            if (!map.ContainsKey(t))
            {
                lock (locker)
                {
                    if (!map.ContainsKey(t))
                    {
                        serializer.AddTypes(new List<Type> { t });
                        var mapLocal = serializer.GetTypeMap();
                        Interlocked.Exchange(ref map, mapLocal);
                    }
                        
                }
               
            }
        }
    }
}
