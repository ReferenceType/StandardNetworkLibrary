using NetworkLibrary.MessageProtocol;
using System;
using System.IO;

namespace CustomSerializer
{
    public class Serializer_ : ISerializer
    {
        public T Deserialize<T>(Stream source)
        {
            throw new NotImplementedException();
        }

        public T Deserialize<T>(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public void Serialize<T>(Stream destination, T instance)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize<T>(T instance)
        {
            throw new NotImplementedException();
        }
    }
}
