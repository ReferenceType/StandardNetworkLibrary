using NetworkLibrary.Components.Statistics;
using System;
using System.Net;

namespace NetworkLibrary.TCP.Base
{
    internal interface IAsyncSession : IDisposable
    {
        /// <summary>
        /// Bytes received event
        /// </summary>
        event Action<Guid, byte[], int, int> OnBytesRecieved;

        /// <summary>
        /// Called when session is closed.
        /// </summary>
        event Action<Guid> OnSessionClosed;

        /// <summary>
        /// RemoteEnpoint of this sesssion
        /// </summary>
        IPEndPoint RemoteEndpoint { get; }

        /// <summary>
        /// Sends buffer asycronusly.
        /// </summary>
        /// <param name="buffer"></param>
        void SendAsync(byte[] buffer);

        /// <summary>
        /// Sends buffer region asyncronusly
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        void SendAsync(byte[] buffer, int offset, int count);

        /// <summary>
        /// Starts the session
        /// </summary>
        void StartSession();

        /// <summary>
        /// Disconnects the client and disposes the resources.
        /// </summary>
        void EndSession();

        /// <summary>
        /// gets the session statistics.
        /// </summary>
        /// <returns></returns>
        SessionStatistics GetSessionStatistics();

    }
}