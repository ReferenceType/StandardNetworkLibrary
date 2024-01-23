using NetworkLibrary.Components.Statistics;
using System;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.Base
{
    public abstract class TcpClientBase
    {
        /// <summary>
        ///<br/> Determines whether to use queue or buffer for message gathering mechanism.
        /// <br/><br/> UseQueue requires your byte[] sources to be not modified after send because your data may be copied asyncronusly.
        /// <br/><br/> UseBuffer will copy your data into a buffer on caller thread. Socket will perform buffer swaps.
        /// You can modify or reuse your data safely.
        /// </summary>
        public ScatterGatherConfig GatherConfig = ScatterGatherConfig.UseQueue;

        /// <summary>
        /// Callback delegate of bytes recieved
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public delegate void BytesRecieved(byte[] bytes, int offset, int count);

        /// <summary>
        /// Fires when client is connected;
        /// </summary>
        public Action OnConnected { get; set; }

        /// <summary>
        /// Fires when connection is failed when the connection is initiated with <see cref="ConnectAsync(string, int)"/>
        /// </summary>
        public Action<Exception> OnConnectFailed { get; set; }

        /// <summary>
        /// Fires when client is disconnected.
        /// </summary>
        public Action OnDisconnected { get; set; }

        /// <summary>
        /// Invoked when bytes are received. New receive operation will not be performed until this callback is finalised.
        /// <br/><br/>Callback data is region of the socket buffer.
        /// <br/>Do a copy if you intend to store the data or use it on different thread.
        /// </summary>
        public BytesRecieved OnBytesReceived { get; set; }


        /// <summary>
        /// Send buffer size option to set on the socket
        /// </summary>
        public int SocketSendBufferSize { get; set; } = 128000;

        /// <summary>
        /// Receive buffer size option to set on the socket
        /// </summary>
        public int SocketRecieveBufferSize { get; set; } = 128000;

        /// <summary>
        /// Byte buffer size of the send copy buffer.
        /// </summary>
        public int SendBufferSize { get; set; } = 128000;

        /// <summary>
        /// Byte buffer size of the receive copy buffer.
        /// </summary>
        public int RecieveBufferSize { get; set; } = 128000;

        /// <summary>
        /// Maximum amount of indexed memory to be held inside the message queue.
        /// it is the cumulative message lengths that are queued.
        /// </summary>
        public int MaxIndexedMemory { get; set; } = 128000000;

        /// <summary>
        /// Indicates whether if we should drop the messages on congestion pressure
        /// this condition occurs when queue is full and send operation is still in progress.
        /// if the messages will not dropped, sender thread will block until operation is finished.
        /// </summary>
        public bool DropOnCongestion { get; set; } = false;

        /// <summary>
        /// Is Client connecting
        /// </summary>
        public bool IsConnecting { get; internal set; }

        /// <summary>
        /// Is client sucessfully connected.
        /// </summary>
        public bool IsConnected { get; internal set; }

        /// <summary>
        /// Connect synronusly
        /// </summary>
        /// <param name="IP"></param>
        /// <param name="port"></param>
        public abstract void Connect(string IP, int port);

        /// <summary>
        /// Connects asyncronusly and notifies the results from either <see cref="OnConnected"/> or <see cref="OnConnectFailed"/>
        /// </summary>
        /// <param name="IP"></param>
        /// <param name="port"></param>
        public abstract void ConnectAsync(string IP, int port);

        /// <summary>
        /// Connects asynronusly with an awaitable task.
        /// </summary>
        /// <param name="IP"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public abstract Task<bool> ConnectAsyncAwaitable(string IP, int port);

        /// <summary>
        /// Sends the message asynronusly or enqueues a message
        /// </summary>
        /// <param name="buffer"></param>
        public abstract void SendAsync(byte[] buffer);

        /// <summary>
        /// Sends or enqueues mesage asyncronously
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public abstract void SendAsync(byte[] buffer, int offset, int count);

        /// <summary>
        /// Disconnects the client.
        /// </summary>
        public abstract void Disconnect();


        /// <summary>
        /// Gets session statistics.
        /// </summary>
        /// <param name="generalStats"></param>
        public abstract void GetStatistics(out TcpStatistics generalStats);



    }
}
