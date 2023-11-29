using NetworkLibrary.Components;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.ByteMessage;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace NetworkLibrary.TCP.AES
{


    public class AesTcpClient : AsyncTpcClient
    {
        private AesTcpSession sessionInternal;
        private ConcurrentAesAlgorithm algorithm;

        public AesTcpClient(ConcurrentAesAlgorithm algorithm)
        { 
            this.algorithm = algorithm;
        }

        public AesTcpClient(Socket clientSocket, ConcurrentAesAlgorithm algorithm) 
        {
    
            this.algorithm = algorithm;
            SetConnectedSocket(clientSocket,ScatterGatherConfig.UseBuffer);
        }

        private protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new AesTcpSession(e, sessionId,algorithm);
            session.socketSendBufferSize = SocketSendBufferSize;
            session.SocketRecieveBufferSize = SocketRecieveBufferSize;
            session.MaxIndexedMemory = MaxIndexedMemory;
            session.DropOnCongestion = DropOnCongestion;
            sessionInternal = session;
            return session;

        }
        //public void SetAlgorithm(ConcurrentAesAlgorithm algorithm)
        //{
        //    this.algorithm = algorithm;
        //    sessionInternal.Algorithm = algorithm;
        //}
        private protected override IAsyncSession CreateSession(Socket socket, Guid sessionId)
        {
            var session = new AesTcpSession(socket, sessionId, algorithm);
            session.socketSendBufferSize = SocketSendBufferSize;
            session.SocketRecieveBufferSize = SocketRecieveBufferSize;
            session.MaxIndexedMemory = MaxIndexedMemory;
            session.DropOnCongestion = DropOnCongestion;
            sessionInternal = session;
            return session;
        }

        public void SendAsync(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2)
        {
            sessionInternal.SendAsync(data1, offset1, count1, data2, offset2, count2);
        }
    }
}
