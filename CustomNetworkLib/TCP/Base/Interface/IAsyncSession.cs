using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    public interface IAsyncSession: IDisposable
    {
        event Action<byte[],int,int>  OnBytesRecieved;
        event Action<Guid> OnSessionClosed;
        void SendAsync(byte[] buffer);

        void StartSession();
        void EndSession();

    }
}