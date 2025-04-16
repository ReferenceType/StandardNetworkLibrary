using NetworkLibrary.DistributedP2P.Client;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{
    internal class UdpChannelBase : IDisposable
    {
        private Socket udpSocket;
        private SocketAsyncEventArgs receiveArgs;
        private readonly IPEndPoint associatedEndpoint;
        int disposed = 0;


        public ChannelInfo Info { get; private set; }

        public event Action<byte[], int, int> OnBytesReceived;
        public Action<string> LogAvailable;
        public event Action OnDisconnected;
        public UdpChannelBase(Socket udpSocket, IPEndPoint receiveEp, ChannelInfo info)
        {
            this.udpSocket = udpSocket;
            associatedEndpoint = receiveEp;
            Info = info;
        }

        public void Start()
        {
            StartReceiver();
        }

        public void Send(byte[] data, int offset, int count)
        {
            udpSocket.SendTo(data, offset, count, SocketFlags.None, associatedEndpoint);
        }

        private void StartReceiver()
        {
            var buff = BufferPool.RentBuffer(65536);

            receiveArgs = new SocketAsyncEventArgs();
            receiveArgs.SetBuffer(buff, 0, buff.Length);
            receiveArgs.Completed += OnReceiveCompleted;
            receiveArgs.RemoteEndPoint = associatedEndpoint;
            Receive();
        }

        private void Receive()
        {
            if (!udpSocket.ReceiveFromAsync(receiveArgs))
            {
                ThreadPool.UnsafeQueueUserWorkItem((s) => OnReceiveCompleted(null, receiveArgs), null);
            }
        }

        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            while (true)
            {
                if (e.SocketError != SocketError.Success)
                {
                    HandleSocketError(e.SocketError);
                    return;
                }

                if (e.BytesTransferred > 0)
                {
                    try
                    {
                        ProcessReceivedData(e.Buffer, e.Offset, e.BytesTransferred, e.RemoteEndPoint);
                    }
                    catch (Exception ex)
                    {
                        Log($"{ex.Message}\n{ex.StackTrace}");
                        CloseChannel();
                        throw;
                    }
                }
                else
                {
                    CloseChannel();
                    return;
                }


                if (Interlocked.CompareExchange(ref disposed, 0, 0) == 1)
                {
                    return;
                }
                if (udpSocket.ReceiveFromAsync(receiveArgs))
                {
                    return;
                }
            }

        }

        private void ProcessReceivedData(byte[] buffer, int offset, int bytesTransferred, EndPoint remoteEndPoint)
        {
            OnBytesReceived?.Invoke(buffer, offset, bytesTransferred);
        }

        private void HandleSocketError(SocketError error)
        {
            if (error != SocketError.Shutdown)
                Log($"Socket error occurred: {error}");

            CloseChannel();
        }

        private void Log(string err)
        {
            LogAvailable?.Invoke(err);
        }

        public virtual void CloseChannel()
        {
            Interlocked.Exchange(ref OnDisconnected, null)?.Invoke();
            Dispose();
        }


        public virtual void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) == 0)
            {
                try
                {
                    if (receiveArgs != null)
                    {

                        BufferPool.ReturnBuffer(receiveArgs.Buffer);
                        receiveArgs.Dispose();
                        receiveArgs = null;
                    }

                    if (udpSocket != null)
                    {
                        udpSocket.Shutdown(SocketShutdown.Both);
                        udpSocket.Close();
                        udpSocket.Dispose();
                        udpSocket = null;
                    }
                }
                catch { }
                LogAvailable = null;
                OnBytesReceived = null;
                OnDisconnected = null;
            }
        }

    }
}
