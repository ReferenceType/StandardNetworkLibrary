using System.IO;

namespace NetworkLibrary.MessageProtocol
{
    public interface ISerializer
    {
        void Serialize<T>(Stream destination, T instance);
        byte[] Serialize<T>(T instance);
        T Deserialize<T>(Stream source);
        T Deserialize<T>(byte[] buffer, int offset, int count);
    }
}
