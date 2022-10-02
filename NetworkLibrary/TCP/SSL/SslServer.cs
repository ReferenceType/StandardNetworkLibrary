using NetworkLibrary.TCP.Base;
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
using System.Threading;

namespace NetworkLibrary.TCP.SSL.Base
{
    public class SslServer : TcpServerBase
    {
        public BytesRecieved OnBytesReceived;
        public ClientAccepted OnClientAccepted;
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;

        // this returns bool
        public ClientConnectionRequest OnClientRequestedConnection;

        internal ConcurrentDictionary<Guid, IAsyncSession> Sessions = new ConcurrentDictionary<Guid, IAsyncSession>();

        private Socket ServerSocket;
        private X509Certificate2 certificate;
        public BufferProvider BufferProvider { get; private set; }
        public bool Stopping { get; private set; }

        public SslServer(int port, int maxClients, X509Certificate2 certificate)
        {
            MaxClients = maxClients;
            ServerPort = port;
            this.certificate = certificate;
            BufferProvider = new BufferProvider(MaxClients, ClientSendBufsize, MaxClients, ClientReceiveBufsize);
            OnClientRequestedConnection = (socket) => true;
            RemoteCertificateValidationCallback += DefaultValidationCallback;
        }

        public override void StartServer()
        {

            ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.ReceiveBufferSize = ServerSockerReceiveBufferSize;
            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, ServerPort));

            ServerSocket.Listen(MaxClients);
            ServerSocket.BeginAccept(Accepted, null);
        }

        private void Accepted(IAsyncResult ar)
        {
            if (Stopping)
                return;
            Socket clientsocket = null;
            try
            {
                clientsocket = ServerSocket.EndAccept(ar);

            }
            catch (ObjectDisposedException) { return; }
            if (ar.CompletedSynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem(s => ServerSocket.BeginAccept(Accepted, null), null);
            }
            else
            {
                ServerSocket.BeginAccept(Accepted, null);
            }
            if (!ValidateConnection(clientsocket))
            {
                return;
            }

            var sslStream = new SslStream(new NetworkStream(clientsocket, true), false, ValidateCeriticate);
            sslStream.BeginAuthenticateAsServer(certificate, true, System.Security.Authentication.SslProtocols.Tls12, false, EndAuthenticate, sslStream);
        }
        protected virtual bool ValidateConnection(Socket clientsocket)
        {
            return OnClientRequestedConnection.Invoke(clientsocket);
        }
        private bool ValidateCeriticate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
           
        }
        private bool DefaultValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            return false;
        }

        private void EndAuthenticate(IAsyncResult ar)
        {
            ((SslStream)ar.AsyncState).EndAuthenticateAsServer(ar);
            var sessionId = Guid.NewGuid();
            var ses = CreateSession(sessionId, (SslStream)ar.AsyncState, BufferProvider);
            ses.OnBytesRecieved += HandleBytesReceived;
            ses.StartSession();
            Sessions.TryAdd(sessionId, ses);

            OnClientAccepted?.Invoke(sessionId);
        }

        internal virtual IAsyncSession CreateSession(Guid guid, SslStream sslStream, BufferProvider bufferProvider)
        {
            var ses = new SslSession(guid, sslStream, bufferProvider);
            ses.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            ses.DropOnCongestion = DropOnBackPressure;
            return ses;
        }

        private void HandleBytesReceived(Guid arg1, byte[] arg2, int arg3, int arg4)
        {
            OnBytesReceived?.Invoke(arg1, arg2, arg3, arg4);
        }

        public override void SendBytesToClient(Guid clientId, byte[] bytes)
        {
            Sessions[clientId].SendAsync(bytes);
        }

        public override void SendBytesToAllClients(byte[] bytes)
        {
            foreach (var session in Sessions)
            {
                session.Value.SendAsync(bytes);
            }
        }

        public override void ShutdownServer()
        {
            Stopping = true;
            ServerSocket.Close();
            ServerSocket.Dispose();
            foreach (var item in Sessions)
            {
                item.Value.EndSession();
            }
            Sessions.Clear();
        }

        public override void CloseSession(Guid sessionId)
        {
            if (Sessions.TryGetValue(sessionId, out var session))
            {
                session.EndSession();
            }
        }
    }
}
