using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageProcessor.Unmanaged;
using NetworkLibrary.DistributedP2P.Client;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetworkLibrary.Components.MessageBuffer;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class TcpChannel:IChannel
    {
        public ChannelInfo Info { get; private set; }

        public event Action<byte[], int, int> BytesReceived;
        public event Action Disconnected;

        private readonly Socket connectedSocket;
        private int totalBytesReceived;
        private SocketAsyncEventArgs receiveArgs;
        private int Closing = 0;

        public PooledMemoryStream sendStream = new PooledMemoryStream();
        public PooledMemoryStream flushStream = new PooledMemoryStream();

        private object bufferMutex = new object();
        private object sendMtex = new object();
        private int sendActive = 0;
        private int msgAvailable = 0;

        private SocketAsyncEventArgs sendArgs;
        private ByteMessageReader reader = new ByteMessageReader();

        public TcpChannel(ChannelInfo info, Socket connectedSocket)
        {
            Info = info;
            this.connectedSocket = connectedSocket;

            StartReceiver();

            sendArgs = new SocketAsyncEventArgs();
            sendArgs.Completed += Sent;
            reader.OnMessageReady += (b, o, c) => BytesReceived?.Invoke(b,o,c);
        }

     
        public void SendAsync(byte[] buffer, int offset, int count)
        {
            if (IsSessionClosing())
                return;

            lock (bufferMutex)
            {
                sendStream.WriteInt(count);// also write flag
                sendStream.Write(buffer, offset, count);
            }

            SignalSend();
        }

        private void SignalSend()
        {
            lock (sendMtex)
            {
                if(Interlocked.CompareExchange(ref sendActive,1,0) == 0)
                {
                    lock (bufferMutex)
                    {
                        var temp = sendStream;
                        sendStream = flushStream;
                        flushStream = temp;
                    }
                    sendArgs.SetBuffer(flushStream.GetBuffer(), 0, flushStream.Position32);
                    if (!connectedSocket.SendAsync(sendArgs))
                    {
                       ThreadPool.UnsafeQueueUserWorkItem(_=> Sent(null, sendArgs),null);
                    }
                }
                else
                {
                    Interlocked.Exchange(ref msgAvailable, 1);
                }
            }
        }
        private void Sent(object sender, SocketAsyncEventArgs e)
        {
            if (IsSessionClosing())
                return;

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving from ");
                CloseChannel();
                return;
            }

            else if (e.BytesTransferred == 0)
            {
                CloseChannel();
                return;
            }
            bool send = false;
            lock (sendMtex)
            {
                if(Interlocked.CompareExchange(ref msgAvailable, 0, 1) == 1)
                {
                    lock (bufferMutex)
                    {
                        var temp = sendStream;
                        sendStream = flushStream;
                        flushStream = temp;
                    }
                    send = true;
                }
                else
                {
                    Interlocked.Exchange(ref sendActive, 0);    
                }
            }

            if (send)
            {
                sendArgs.SetBuffer(flushStream.GetBuffer(), 0, flushStream.Position32);
                flushStream.Position32 = 0;

                if (!connectedSocket.SendAsync(sendArgs))
                {
                    Sent(null, sendArgs);
                }
            }

        }

        public void Start()
        {
            Receive();
        }
        private void StartReceiver()
        {
            receiveArgs = new SocketAsyncEventArgs();
            var buff = new byte[1280000];
            receiveArgs.SetBuffer(buff, 0, buff.Length);
            receiveArgs.Completed += Received;
          
        }

        private void Receive()
        {
            if (!connectedSocket.ReceiveAsync(receiveArgs))
            {
                ThreadPool.UnsafeQueueUserWorkItem(_ => Received(null, receiveArgs), null);
            }
        }

        private void Received(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving from ");
                CloseChannel();
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                CloseChannel();
                return;
            }
            totalBytesReceived += e.BytesTransferred;
            try
            {
                HandleReceived(e.Buffer, e.Offset, e.BytesTransferred);
            }
            catch (Exception ex)
            {
                Log( ex.Message + "\n" + ex.StackTrace);
                CloseChannel();
                return;
            }

            Receive();
        }

        public void CloseChannel()
        {
            if(Interlocked.CompareExchange(ref Closing,1,0) == 0)
            {
                try
                {
                    connectedSocket.Shutdown(SocketShutdown.Both);
                }
                catch { }

            }
        }

        private void Log(string v)
        {
           
        }

        private void HandleReceived(byte[] buffer, int offset, int bytesTransferred)
        {
            reader.ParseBytes(buffer, offset, bytesTransferred);
        }

      

        private void HandleError(SocketAsyncEventArgs e, string v)
        {
            
        }

       

        private bool IsSessionClosing()
        {
            return Interlocked.CompareExchange(ref Closing, 0, 0) == 1;
        }

    }
}
