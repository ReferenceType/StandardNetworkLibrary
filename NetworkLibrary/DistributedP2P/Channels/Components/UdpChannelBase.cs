using NetworkLibrary.DistributedP2P.Client;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{
    internal class UdpChannelBase : IChannel, IDisposable
    {
        private Socket udpSocket;
        private SocketAsyncEventArgs receiveArgs;
        private readonly IPEndPoint associatedEndpoint;

        public ChannelInfo Info { get; private set; }

        public event Action<byte[], int, int> OnMessageReceived;
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
            try
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
                        Console.WriteLine($"Error processing received data: {ex}");
                    }
                }

                Receive();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in receive completion: {ex}");
            }
        }

        private void ProcessReceivedData(byte[] buffer, int offset, int bytesTransferred, EndPoint remoteEndPoint)
        {
            OnMessageReceived?.Invoke(buffer, offset, bytesTransferred);
        }

        private void HandleSocketError(SocketError error)
        {
            Console.WriteLine($"Socket error occurred: {error}");
        }


        public virtual void CloseChannel()
        {
            Dispose();
        }
        public virtual void Dispose()
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
                    udpSocket.Close();
                    udpSocket.Dispose();
                    udpSocket = null;
                }
            }
            catch { }

        }

    }
}
