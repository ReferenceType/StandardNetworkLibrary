using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    public class TcpSession : IAsyncSession
    {
        
        protected byte[] sendBuffer;
        protected byte[] recieveBuffer;

        protected SocketAsyncEventArgs ClientSendEventArg;
        protected SocketAsyncEventArgs ClientRecieveEventArg;

        protected Socket sessionSocket;
        protected ConcurrentQueue<byte[]> SendQueue = new ConcurrentQueue<byte[]>();

        public event Action<byte[], int, int> OnBytesRecieved;
        public Guid SessionId;
        protected int sendBufferSize = 128000;
        private int recieveBufferSize = 128000;
        private int maxMem = 128000000;
        private int currMem = 0;
        private bool Drop = false;

        private bool fragmentedMessageExist = false;
        private bool isHeaderWritten;
        private int fragmetMsgOffset = 0;
        private byte[] fragmentedMsg;

        protected int prefixLenght = 0;

        protected readonly object locker = new object();

        public TcpSession(SocketAsyncEventArgs acceptedArg, Guid sessionId)
        {
            sessionSocket = acceptedArg.AcceptSocket;
            ConfigureSocket();
            ConfigureSendArgs(acceptedArg);
            ConfigureRecieveArgs(acceptedArg);

            StartRecieving();
        }

        protected virtual void ConfigureSocket()
        {
            sessionSocket.ReceiveBufferSize = 128000;
            sessionSocket.SendBufferSize = 128000;
        }

        protected virtual void ConfigureSendArgs(SocketAsyncEventArgs acceptedArg)
        {
            var sendArg = new SocketAsyncEventArgs();
            sendArg.Completed += Sent;

            var token = new UserToken();
            token.Guid = Guid.NewGuid();

            sendArg.UserToken = token;
            sendBuffer = BufferManager.GetBuffer();

            sendArg.SetBuffer(sendBuffer, 0, sendBuffer.Length);
            ClientSendEventArg = sendArg;

            //ClientSendEventArg.SendPacketsFlags = TransmitFileOptions.UseDefaultWorkerThread;
        }

        protected virtual void ConfigureRecieveArgs(SocketAsyncEventArgs acceptedArg)
        {
            var recieveArg = new SocketAsyncEventArgs();
            recieveArg.Completed += BytesRecieved;

            var token = new UserToken();
            token.Guid = Guid.NewGuid();

            recieveArg.UserToken = new UserToken();
            recieveBuffer = BufferManager.GetBuffer(); ;

            ClientRecieveEventArg = recieveArg;
            ClientRecieveEventArg.SetBuffer(recieveBuffer, 0, recieveBuffer.Length);

            //ClientRecieveEventArg.SendPacketsFlags = TransmitFileOptions.UseDefaultWorkerThread;
        }
        #region Recieve 
        protected virtual void StartRecieving()
        {
            sessionSocket.ReceiveAsync(ClientRecieveEventArg);
        }

        protected virtual void BytesRecieved(object sender, SocketAsyncEventArgs e)
        {

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving header from ");
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                Console.WriteLine("Disconnecting cl");
                DisconnectClient(e);
                return;
            }

            HandleRecieveComplete(e.Buffer, e.Offset, e.BytesTransferred);

            e.SetBuffer(0, e.Buffer.Length);
            if (!sessionSocket.ReceiveAsync(e))
            {
                BytesRecieved(null, e);
            }
        }

        protected virtual void HandleRecieveComplete(byte[] buffer, int offset, int count)
        {
            OnBytesRecieved?.Invoke(buffer, offset, count);
        }
         #endregion Recieve


       

        #region Send
        public virtual void SendAsync(byte[] bytes)
        {
            SendOrEnqueue(ref bytes);
        }


        protected virtual void SendOrEnqueue(ref byte[] bytes)
        {
            var token = (UserToken)ClientSendEventArg.UserToken;
            lock (locker)
            {
                if (Volatile.Read(ref currMem) < maxMem && token.OperationSemaphore.CurrentCount < 1)
                {
                    Interlocked.Add(ref currMem, bytes.Length + prefixLenght);
                    SendQueue.Enqueue(bytes);
                    return;
                }
            }

            if (Drop && token.OperationSemaphore.CurrentCount < 1)
                return;

            token.WaitOperationCompletion();
            Interlocked.Add(ref currMem, bytes.Length+ prefixLenght);

            SendQueue.Enqueue(bytes);
            ConsumeQueueAndSend();
            return;

        }

        private bool ConsumeQueueAndSend()
        {
            bool ret = FlushQueue(ref sendBuffer, 0, out int offset);
            if (ret)
            {
                ClientSendEventArg.SetBuffer(sendBuffer, 0, offset);
                if (!sessionSocket.SendAsync(ClientSendEventArg))
                {
                   Task.Run(()=>Sent(null, ClientSendEventArg));
                }
            }

            return ret;
        }
        private bool FlushQueue(ref byte[] sendBuffer, int offset, out int count)
        {
            count = 0;
            CheckLeftoverMessage(ref sendBuffer, ref offset, ref count);
            if (count == sendBuffer.Length)
                return true;
            
            while (SendQueue.TryDequeue(out var bytes))
            {
                // buffer cant carry entire message not enough space
                if (prefixLenght + bytes.Length - fragmetMsgOffset > sendBuffer.Length - offset)
                {
                    fragmentedMsg = bytes;
                    fragmentedMessageExist = true;

                    int available = sendBuffer.Length - offset;
                    if (available < prefixLenght)
                        break;

                    Interlocked.Add(ref currMem, -(available));
                    WriteMessagePrefix(ref sendBuffer, offset, bytes.Length);
                    isHeaderWritten = true;

                    offset += prefixLenght;
                    available -= prefixLenght;

                    Buffer.BlockCopy(bytes, fragmetMsgOffset, sendBuffer, offset, available);
                    count = sendBuffer.Length;

                    fragmetMsgOffset += available;
                    break;
                }
                //---- regular msg
                //SendQueue.TryDequeue(out bytes);
                Interlocked.Add(ref currMem, -(prefixLenght + bytes.Length));

                WriteMessagePrefix(ref sendBuffer, offset, bytes.Length);
                offset += prefixLenght;

                Buffer.BlockCopy(bytes, 0, sendBuffer, offset, bytes.Length);
                offset += bytes.Length;

                count += prefixLenght + bytes.Length;
            }
            return count != 0;

        }

        private void CheckLeftoverMessage(ref byte[] sendBuffer, ref int offset, ref int count)
        {
            if (fragmentedMessageExist)
            {
                int available = sendBuffer.Length - offset;
                if (!isHeaderWritten)
                {
                    isHeaderWritten = true;
                    WriteMessagePrefix(ref sendBuffer, offset, fragmentedMsg.Length);
                    offset += prefixLenght;
                    available -= prefixLenght;
                }
                // will fit
                if (fragmentedMsg.Length - fragmetMsgOffset <= available)
                {
                    Interlocked.Add(ref currMem, -(fragmentedMsg.Length - fragmetMsgOffset));
                    Buffer.BlockCopy(fragmentedMsg, fragmetMsgOffset, sendBuffer, offset, fragmentedMsg.Length - fragmetMsgOffset);

                    offset += fragmentedMsg.Length - fragmetMsgOffset;
                    count += offset;

                    fragmetMsgOffset = 0;
                    fragmentedMsg = null;
                    isHeaderWritten = false;
                    fragmentedMessageExist = false;
                }
                // wont fit read until you fill buffer
                else
                {
                    Interlocked.Add(ref currMem, -(available));
                    Buffer.BlockCopy(fragmentedMsg, fragmetMsgOffset, sendBuffer, offset, available);

                    count = sendBuffer.Length;
                    fragmetMsgOffset += available;

                   // return true;
                }
            }
        }
        protected virtual void Sent(object ignored, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while sending the client");

                return;
            }

            else if (e.BytesTransferred < e.Count)
            {
                e.SetBuffer(e.Offset + e.BytesTransferred, e.Count - e.BytesTransferred);
                if (!sessionSocket.SendAsync(e))
                {
                    Task.Run(()=>Sent(null, e));
                }
                return;
            }
           
            if (!ConsumeQueueAndSend())
            {
                bool flushAgain= false;
                // here it means queue was empty and there was nothing to flush.
                // but if during the couple cycles inbetween something is enqueued, 
                // i have to flush that part , or it will stuck at queue since this consumer is exiting.
                lock (locker)
                {
                    if (SendQueue.Count > 0)
                    {
                        flushAgain = true;
                    }
                    else
                    {
                        ((UserToken)e.UserToken).OperationCompleted();
                        return;
                    }
                }
                if (flushAgain)
                    ConsumeQueueAndSend();
            }
        }

        #endregion Send


        protected virtual void WriteMessagePrefix(ref byte[] buffer,int offset,int messageLength) {}

        // TODO
        protected void DisconnectClient(SocketAsyncEventArgs e)
        {
        }

        // TODO 
        protected void HandleError(SocketAsyncEventArgs e, string v)
        {
            Console.WriteLine(v);
        }
    }
}
