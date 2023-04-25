using MessageProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace DataContractNetwork.Components
{
    public class DataContractSerialiser : ISerializer
    {
        static ConcurrentDictionary<Type, DataContractSerializer> seralizers = new ConcurrentDictionary<Type, DataContractSerializer>();
        DataContractSerializer serializer;
        public DataContractSerialiser()
        {
            serializer = new DataContractSerializer(typeof(MessageEnvelope));
            seralizers.TryAdd(typeof(MessageEnvelope), serializer);
        }

        private DataContractSerializer GetSerializer<T>()
        {
           if(seralizers.TryGetValue(typeof(T), out var serializer))
            {
                return serializer;
            }
            serializer = new DataContractSerializer(typeof(MessageEnvelope));
            seralizers.TryAdd(typeof(T), serializer);
            return serializer;
        }

        public T Deserialize<T>(Stream source)
        {
            DataContractSerializer serializer = GetSerializer<T>();
            return (T)serializer.ReadObject(source);
        }

      

        public T Deserialize<T>(byte[] buffer, int offset, int count)
        {
            DataContractSerializer serializer = GetSerializer<T>();
            return (T)serializer.ReadObject(new MemoryStream(buffer,offset,count));
        }

        public void Serialize<T>(Stream destination, T instance)
        {
            DataContractSerializer serializer = GetSerializer<T>();
            //var ms = new MemoryStream();

            //serializer.WriteObject(ms, instance);
            //destination.Write(ms.GetBuffer(), 0, (int)ms.Length);

            serializer.WriteObject(destination, instance);
        }

        public byte[] Serialize<T>(T instance)
        {
            DataContractSerializer serializer = GetSerializer<T>();
            var ms =  new MemoryStream();
            serializer.WriteObject(ms, instance);
            return ms.ToArray();
        }
    }
}
