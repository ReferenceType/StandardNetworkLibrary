using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    public interface IAsyncSession
    {
        event EventHandler<byte[]>  OnBytesRecieved;

        void SendAsync(byte[] buffer);
    }
}