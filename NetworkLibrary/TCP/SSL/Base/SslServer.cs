using NetworkLibrary.TCP.Base.Interface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetworkLibrary.TCP.SSL.Base
{
    public class SslServer
    {
        private Socket ServerSocket;
        private int ServerPort;
        private int MaxClients;
        public int MaxMemoryPerClient = 12800000;
        private X509Certificate2 certificate;

        public Action<Guid, byte[], int, int> OnBytesReceived;
        public bool DropOnCongestion = false;


        public ConcurrentDictionary<Guid, IAsyncSession> Sessions = new ConcurrentDictionary<Guid, IAsyncSession>();
        public SslServer(int port, int maxClients, X509Certificate2 certificate)
        {
            MaxClients = maxClients;
            ServerPort = port;
            this.certificate = certificate;

        }

        public void StartServer()
        {
            ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.ReceiveBufferSize = 1280000000;
            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, ServerPort));

            ServerSocket.Listen(MaxClients);
            ServerSocket.BeginAccept(Accepted, null);
            
        }

        private void Accepted(IAsyncResult ar)
        {
            Socket clientsocket = ServerSocket.EndAccept(ar);

            var sslStream = new SslStream(new NetworkStream(clientsocket, true), false, ValidateCeriticate);
            sslStream.BeginAuthenticateAsServer(certificate, true, System.Security.Authentication.SslProtocols.Tls12, false, EndAuth, sslStream);

            ServerSocket.BeginAccept(Accepted, null);

        }

        private bool ValidateCeriticate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None || sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
                return true;
            return false;
        }

        private void EndAuth(IAsyncResult ar)
        {
            ((SslStream)ar.AsyncState).EndAuthenticateAsServer(ar);
            var sessionId = Guid.NewGuid();
            var ses = CreateSession(sessionId, (SslStream)ar.AsyncState);
            ses.OnBytesRecieved += HandleBytesReceived;
            ses.StartSession();
            Sessions.TryAdd(sessionId, ses);
        }

        protected virtual IAsyncSession CreateSession(Guid guid, SslStream sslStream)
        {
            var ses = new SslSession(guid, sslStream);
            ses.MaxIndexedMemory = MaxMemoryPerClient;
            ses.DropOnCongestion = DropOnCongestion;
            return ses;
        }

        private void HandleBytesReceived(Guid arg1, byte[] arg2, int arg3, int arg4)
        {
            OnBytesReceived?.Invoke(arg1, arg2, arg3, arg4);
        }

        public void SendBytesToClient(Guid clientId, byte[] bytes)
        {
            Sessions[clientId].SendAsync(bytes);
        }

        public void SendBytesToAllClients(byte[] bytes)
        {
            foreach (var session in Sessions)
            {
                session.Value.SendAsync(bytes);
            }
        }
    }
}
