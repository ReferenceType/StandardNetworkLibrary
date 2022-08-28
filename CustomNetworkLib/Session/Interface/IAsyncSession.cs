using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    public interface IAsyncSession
    {
        event Action<byte[],int,int>  OnBytesRecieved;
        void SendAsync(byte[] buffer);
    }
}