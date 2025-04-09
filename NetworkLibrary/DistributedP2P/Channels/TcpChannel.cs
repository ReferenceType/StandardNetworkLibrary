using NetworkLibrary.Components;
using NetworkLibrary.DistributedP2P.Channels.Components;
using NetworkLibrary.DistributedP2P.Client;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Channels
{

    public class TcpChannel : IChannel
    {
        public ChannelInfo Info { get; private set; }

        public event Action<byte[], int, int> BytesReceived;
        public event Action Disconnected;

        private readonly Socket connectedSocket;
        private int totalBytesReceived;
        private SocketAsyncEventArgs receiveArgs;
        private int Closing = 0;

        protected PooledMemoryStream sendStream = new PooledMemoryStream();
        protected PooledMemoryStream flushStream = new PooledMemoryStream();

        private object bufferMutex = new object();
        private object sendMtex = new object();
        private int sendActive = 0;
        private int msgAvailable = 0;

        private SocketAsyncEventArgs sendArgs;
        private ByteMessageReader reader = new ByteMessageReader();

        private KeepAlive keepAlive;
        private Pinger pinger;

        public TcpChannel(ChannelInfo info, Socket connectedSocket)
        {
            Info = info;
            this.connectedSocket = connectedSocket;

            InitializeReceiver();

            sendArgs = new SocketAsyncEventArgs();
            sendArgs.Completed += Sent;
            reader.OnMessageReady += HandleReceivedBytes;

            keepAlive = new KeepAlive();
            keepAlive.SendData += FlagAndSend;
            keepAlive.NotAlive += () => ErrorAndEnd("Kepp Alive Timed Out");

            pinger = new Pinger();
            pinger.SendData += FlagAndSend;
        }


        public Task<double> Ping()
        {
            return pinger.Ping();
        }

        public void SendAsync(byte[] buffer, int offset, int count)
        {
            FlagAndSend(MessageFlags.StandardMessage, buffer, offset, count);
        }

        protected void FlagAndSend(MessageFlags flag, byte[] buffer, int offset, int count)
        {

            if (IsSessionClosing())
                return;

            lock (bufferMutex)
            {
                try
                {
                    int lenghtPos = sendStream.Position32;
                    sendStream.Position32 += 4;

                    sendStream.WriteByte((byte)flag);
                    int prefixLen = WritePrefix(sendStream);

                    int amountWritten = WriteData(buffer, offset, count);
                    int lastPos = sendStream.Position32;

                    sendStream.Position32 = lenghtPos;
                    sendStream.WriteInt(amountWritten + prefixLen + 1);
                    sendStream.Position32 = lastPos;


                }
                catch (Exception ex)
                {
                    ErrorAndEnd(ex.Message + "\n" + ex.StackTrace);
                    return;
                }


            }
            SignalSend();


        }

        protected virtual int WritePrefix(PooledMemoryStream sendStream)
        {
            return 0;
        }

        protected virtual int WriteData(byte[] buffer, int offset, int count)
        {
            sendStream.Write(buffer, offset, count);
            return count;
        }

        private void SignalSend()
        {
            lock (sendMtex)
            {
                if (Interlocked.CompareExchange(ref sendActive, 1, 0) == 0)
                {
                    lock (bufferMutex)
                    {
                        var flush = Interlocked.Exchange(ref sendStream, flushStream);
                        Interlocked.Exchange(ref flushStream, flush);
                        sendStream.Position32 = 0;
                    }

                    sendArgs.SetBuffer(flushStream.GetBuffer(), 0, flushStream.Position32);
                    if (!connectedSocket.SendAsync(sendArgs))
                    {
                        ThreadPool.UnsafeQueueUserWorkItem(_ => Sent(null, sendArgs), null);
                    }
                }
                else
                {
                    if (sendStream.Position32 != 0)
                        Interlocked.Exchange(ref msgAvailable, 1);
                }
            }
        }
        private void Sent(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (IsSessionClosing())
                    return;

                if (e.SocketError != SocketError.Success)
                {
                    ErrorAndEnd($"While sending a socket error occured {e.SocketError}");
                    CloseChannel();
                    return;
                }

                else if (e.BytesTransferred == 0)
                {
                    Log("0 bytes Sent");
                    CloseChannel();
                    return;
                }
                bool send = false;
                lock (sendMtex)
                {
                    if (Interlocked.CompareExchange(ref msgAvailable, 0, 1) == 1)
                    {
                        lock (bufferMutex)
                        {
                            var flush = Interlocked.Exchange(ref sendStream, flushStream);
                            Interlocked.Exchange(ref flushStream, flush);

                            sendStream.Position32 = 0;
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

                    if (!connectedSocket.SendAsync(sendArgs))
                    {
                        ThreadPool.UnsafeQueueUserWorkItem(_ => Sent(null, sendArgs), null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex.StackTrace);
                CloseChannel();
            }


        }

        public void Start()
        {
            Receive();
        }
        private void InitializeReceiver()
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
                ErrorAndEnd($"While receiving a socket error occured {e.SocketError}");
                CloseChannel();
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                Log("0 bytes");
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
                ErrorAndEnd(ex.Message + "\n" + ex.StackTrace);
                return;
            }

            Receive();
        }

        protected virtual void HandleReceivedBytes(byte[] buffer, int offset, int count)
        {
            var flag = (MessageFlags)buffer[offset++];
            count--;
            HandleReceivedMessage(buffer, offset, count, flag);

        }

        protected virtual void HandleReceivedMessage(byte[] buffer, int offset, int count, MessageFlags flag)
        {
            switch (flag)
            {
                case MessageFlags.StandardMessage:
                    PublishBytes(buffer, offset, count);
                    break;

                case MessageFlags.KeepAliveMessage:
                    keepAlive.HandleMessage(flag, buffer, offset, count);
                    break;

                case MessageFlags.Pong:
                case MessageFlags.Ping:
                    pinger.HandleMessage(flag, buffer, offset, count);
                    break;
            }
        }

        protected void PublishBytes(byte[] buffer, int offset, int count)
        {
            BytesReceived?.Invoke(buffer, offset, count);
        }

        public void CloseChannel()
        {
            Console.WriteLine("Closing channel");
            if (Interlocked.CompareExchange(ref Closing, 1, 0) == 0)
            {
                ReleaseResources();
            }
        }

        protected virtual void ReleaseResources()
        {
            try
            {
                connectedSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            keepAlive.Close();
            Disconnected?.Invoke();
        }


        private void HandleReceived(byte[] buffer, int offset, int bytesTransferred)
        {
            reader.ParseBytes(buffer, offset, bytesTransferred);
        }



        protected void ErrorAndEnd(string errMsg)
        {
            Log(errMsg);
            CloseChannel();
        }

        private void Log(string v)
        {
            Console.WriteLine(v);
        }



        private bool IsSessionClosing()
        {
            return Interlocked.CompareExchange(ref Closing, 0, 0) == 1;
        }

    }
}
