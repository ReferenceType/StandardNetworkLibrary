using MessageProtocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace JsonMessageNetwork.Components
{
    public class JsonSerializer : ISerializer
    {

        public T Deserialize<T>(Stream source)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(source);
        }

        public T Deserialize<T>(byte[] buffer, int offset, int count)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(new ReadOnlySpan<byte>(buffer,offset,count));
        }

        public void Serialize<T>(Stream destination, T instance)
        {
            System.Text.Json.JsonSerializer.Serialize( destination, instance);
        }

        public byte[] Serialize<T>(T instance)
        {
            using (MemoryStream ms =  new MemoryStream())
            {
                 System.Text.Json.JsonSerializer.Serialize<T>(ms,instance);
                 return ms.ToArray();
            }
        }
    }
}
