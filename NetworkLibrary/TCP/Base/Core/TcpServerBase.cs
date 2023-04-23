using NetworkLibrary.Components.Statistics;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

public enum ScatterGatherConfig
{
    UseQueue,
    UseBuffer
}
namespace NetworkLibrary.TCP.Base
{
    public abstract class TcpServerBase
    {

        /// <summary>
        /// Use queue if your messages are from static resources such a a byte[] without segmentation.
        /// Use buffer if your messsages are mainly from segment of byte[] i.e. buffer, offset, count.
        /// </summary>
        public ScatterGatherConfig GatherConfig = ScatterGatherConfig.UseQueue;

        /// <summary>
        /// Client accepted callback delegate with session id as Guid
        /// </summary>
        /// <param name="guid"></param>
        public delegate void ClientAccepted(Guid guid);

        /// <summary>
        /// Bytes received callback delegate with client session Id
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public delegate void BytesRecieved(in Guid guid, byte[] bytes, int offset, int count);

        /// <summary>
        /// Connection Request callback delegate
        /// </summary>
        /// <param name="acceptedSocket"></param>
        /// <returns></returns>
        public delegate bool ClientConnectionRequest(Socket acceptedSocket);

        /// <summary>
        /// Client is disconnected.
        /// </summary>
        /// <param name="guid"></param>
        public delegate void ClientDisconnected(Guid guid);



        /// <summary>
        /// Client send buffer size
        /// Does nothing if server is already started.
        /// </summary>
        public int ClientSendBufsize { get; set; } = 128000;

        /// <summary>
        /// Client receive buffer size.
        /// Does nothing if server is already started.
        /// </summary>
        public int ClientReceiveBufsize { get; set; } = 128000;

        /// <summary>
        /// Maximum amount of indexed memory to be held inside the message queue.
        /// it is the maximum cumulative message lengths that are queued per client.
        /// </summary>
        public int MaxIndexedMemoryPerClient { get; set; } = 1280000;

        /// <summary>
        /// Server socket receive buffer size. Keep it large for high number of clients.
        /// </summary>
        public int ServerSockerReceiveBufferSize { get; set; } = 2080000000;

        /// <summary>
        /// Indicates whether if we should drop the messages on congestion pressure
        /// this condition occurs when queue is full and send operation is still in progress.
        /// if the messages will not dropped, sender thread will block until operation is finished.
        /// </summary>
        public bool DropOnBackPressure { get; set; } = false;

        /// <summary>
        /// Enables or disables naggle no  delay property of the server.
        /// </summary>
        public bool NaggleNoDelay { get; set; } = false;

        /// <summary>
        /// Server port
        /// </summary>
        public int ServerPort { get; protected set; }

        /// <summary>
        /// Starts the server.
        /// </summary>
        public abstract void StartServer();

        public abstract void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats);

        public abstract IPEndPoint GetSessionEndpoint(Guid sessionId);

        /// <summary>
        /// Shuts down the server. 
        /// Shutdown disposes all client resources.
        /// </summary>
        public abstract void ShutdownServer();

        /// <summary>
        /// closes a given session
        /// </summary>
        /// <param name="sessionId"></param>
        public abstract void CloseSession(Guid sessionId);

        /// <summary>
        /// Sends or enqueues bytes to be send to a given client
        /// </summary>
        /// <param name="id"></param>
        /// <param name="bytes"></param>
        public abstract void SendBytesToClient(in Guid id, byte[] bytes);

        /// <summary>
        /// Multicats message to all clients.
        /// </summary>
        /// <param name="bytes"></param>
        public abstract void SendBytesToAllClients(byte[] bytes);

    }
}
