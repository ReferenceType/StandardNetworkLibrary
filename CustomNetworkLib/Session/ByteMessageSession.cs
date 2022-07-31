using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    internal class ByteMessageSession : TcpSession
    {
        public ByteMessageSession(SocketAsyncEventArgs acceptedArg,Guid sessionId) : base(acceptedArg,sessionId)
        {
        }

        protected override void ConfigureRecieveArgs(SocketAsyncEventArgs acceptedArg)
        {
            var recieveArg = new SocketAsyncEventArgs();
            recieveArg.Completed += RecievedHeader;

            recieveArg.UserToken = new UserToken(acceptedArg.AcceptSocket);
            recieveArg.AcceptSocket = acceptedArg.AcceptSocket;
            ClientRecieveEventArg = recieveArg;
            ClientRecieveEventArg.SetBuffer(recieveBuffer, 0, 4);
        }


        #region Recieve
        private void RecievedHeader(object sender, SocketAsyncEventArgs e)
        {

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving header from ");
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                DisconnectClient(e);
                return;
            }
            else if (e.BytesTransferred + e.Offset < 4)
            {
                e.SetBuffer(e.BytesTransferred, 4 - e.BytesTransferred);
                if (!e.AcceptSocket.ReceiveAsync(e))
                {
                    RecievedHeader(null, e);
                }
                return;
            }
            int expectedLen = BufferManager.ReadByteFrame(e.Buffer, 0);
            if (expectedLen > recieveBuffer.Length + 4)
            {
                // todo max len
                recieveBuffer = new byte[expectedLen + 4];

            }


            e.SetBuffer(0, expectedLen);

            e.Completed -= RecievedHeader;
            e.Completed += RecievedBody;

            if (!e.AcceptSocket.ReceiveAsync(e))
                RecievedBody(null, e);

        }

        private void RecievedBody(object sender, SocketAsyncEventArgs e)
        {

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving message body from ");
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                DisconnectClient(e);
                return;
            }
            else if (e.BytesTransferred < e.Count)
            {
                // count decreasing
                e.SetBuffer(e.Offset + e.BytesTransferred, e.Count - e.BytesTransferred);
                if (!e.AcceptSocket.ReceiveAsync(e))
                {
                    RecievedBody(null, e);
                }
                return;
            }
            HandleRecieveComplete(e.Buffer, e.Offset, e.Count);

            e.SetBuffer(0, 4);

            e.Completed -= RecievedBody;
            e.Completed += RecievedHeader;
            if (!e.AcceptSocket.ReceiveAsync(e))
            {
                RecievedHeader(null, e);
            }
        }
        #endregion


        protected override void Send(byte[] bytes)
        {
            byte[] byteFrame = BitConverter.GetBytes(bytes.Length);
            for (int i = 0; i < 4; i++)
            {
                sendBuffer[i] = byteFrame[i];
            }
            Buffer.BlockCopy(bytes, 0, sendBuffer, 4, bytes.Length);

            ClientSendEventArg.SetBuffer(0, bytes.Length + 4);
            if (!sessionEventArg.AcceptSocket.SendAsync(ClientSendEventArg))
            {
                Sent(null, ClientSendEventArg);
            }
        }

    }
}

