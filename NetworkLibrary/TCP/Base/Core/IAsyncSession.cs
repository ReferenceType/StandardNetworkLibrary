using NetworkLibrary.Components.Statistics;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.Base
{
    internal interface IAsyncSession : IDisposable
    {
        event Action<Guid, byte[], int, int> OnBytesRecieved;
        event Action<Guid> OnSessionClosed;

        IPEndPoint RemoteEndpoint { get; }
        void SendAsync(byte[] buffer);

        void SendAsync(byte[] buffer, int offset, int count);

        void StartSession();
        void EndSession();

        SessionStatistics GetSessionStatistics();

    }
}