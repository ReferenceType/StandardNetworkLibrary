using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.Base.Interface
{
    public interface IAsyncSession : IDisposable
    {
        event Action<Guid, byte[], int, int> OnBytesRecieved;
        event Action<Guid> OnSessionClosed;
        void SendAsync(byte[] buffer);

        void StartSession();
        void EndSession();

    }
}