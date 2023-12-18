using NetworkLibrary.Components;
using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetworkLibrary.TCP.AES
{
    public class AesTcpServer : AsyncTcpServer
    {
        private ConcurrentAesAlgorithm algorithm;
        public AesTcpServer(int port):base(port)
        { }

        public AesTcpServer(IPEndPoint endpointToBind, ConcurrentAesAlgorithm aesAlgorithm) : base(endpointToBind)
        {
            if (aesAlgorithm == null)
            {

            }
            this.algorithm = aesAlgorithm;
        }

        private protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new AesTcpSession(e, sessionId, algorithm);
            session.socketSendBufferSize = ClientSendBufsize;
            session.SocketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            session.UseQueue = false;
            return session;

        }
        //public void SetAlgorithm(ConcurrentAesAlgorithm algorithm)
        //{
        //    this.algorithm = algorithm;
        //    foreach (var ses in Sessions.Values)
        //    {
        //        ((AesTcpSession)ses).Algorithm = algorithm;
        //    }
        //}
        

        internal void SendBytesToClient(Guid clientId, byte[] buffer, int offset, int count, byte[] buffer2, int offset2, int count2)
        {
            if(Sessions.TryGetValue(clientId, out var session))
            {
                ((AesTcpSession)session).SendAsync(buffer, offset, count, buffer2, offset2, count2);
            }

        }
    }
}
