using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NetworkLibrary.P2P.Components
{
    public class MockSerializer:ISerializer
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
