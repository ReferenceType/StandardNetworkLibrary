using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static NetworkLibrary.TCP.Base.AsyncTcpServer;

namespace NetworkLibrary.TCP.SSL.Base
{
    public class SslServer : TcpServerBase
    {
        public BytesRecieved OnBytesReceived;
        public ClientAccepted OnClientAccepted;
        public ClientDisconnected OnClientDisconnected;
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;
        // this returns bool
        public ClientConnectionRequest OnClientRequestedConnection;

        protected ConcurrentDictionary<Guid, IAsyncSession> Sessions = new ConcurrentDictionary<Guid, IAsyncSession>();
        public int SessionCount => Sessions.Count;

        internal ConcurrentDictionary<Guid, TcpStatistics> Stats { get; } = new ConcurrentDictionary<Guid, TcpStatistics>();


        private Socket serverSocket;
        private X509Certificate2 certificate;
        private TcpServerStatisticsPublisher statisticsPublisher;

        public bool Stopping { get; private set; }

        public SslServer(int port, X509Certificate2 certificate)
        {
            ServerPort = port;
            this.certificate = certificate;
            OnClientRequestedConnection = (socket) => true;
            RemoteCertificateValidationCallback += DefaultValidationCallback;
           
            statisticsPublisher = new TcpServerStatisticsPublisher(Sessions);
        }
       
        public override void StartServer()
        {
            serverSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            serverSocket.ReceiveBufferSize = ServerSockerReceiveBufferSize;
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, ServerPort));

            serverSocket.Listen(10000);
            serverSocket.BeginAccept(Accepted, null);
        }

        private void Accepted(IAsyncResult ar)
        {
            if (Stopping)
                return;
            Socket clientsocket = null;
            try
            {
                clientsocket = serverSocket.EndAccept(ar);

            }
            catch (ObjectDisposedException) { return; }

            if (ar.CompletedSynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem(s => serverSocket.BeginAccept(Accepted, null), null);
            }
            else
            {
                serverSocket.BeginAccept(Accepted, null);
            }
            if (!ValidateConnection(clientsocket))
            {
                return;
            }

            var sslStream = new SslStream(new NetworkStream(clientsocket, true), false, ValidateCeriticate);
            try
            {
                sslStream.BeginAuthenticateAsServer(certificate,
                                               true,
                                               System.Security.Authentication.SslProtocols.Tls12,
                                               false,
                                               EndAuthenticate,
                                               new ValueTuple<SslStream, IPEndPoint>(sslStream, (IPEndPoint)clientsocket.RemoteEndPoint));
            }
            catch(Exception ex) 
            when(ex is AuthenticationException || ex is ObjectDisposedException)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Athentication as server failed: " + ex.Message);
            }

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
            try
            {
                ((ValueTuple<SslStream, IPEndPoint>)ar.AsyncState).Item1.EndAuthenticateAsServer(ar);
            }
            catch (Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Athentication as server failed: " + e.Message);
                try
                {
                    ((SslStream)ar.AsyncState).Close();

                }
                catch { }
                return;
            }
            var sessionId = Guid.NewGuid();
            var ses = CreateSession(sessionId, (ValueTuple<SslStream, IPEndPoint>)ar.AsyncState );
            ses.OnBytesRecieved += HandleBytesReceived;
            ses.OnSessionClosed += HandeDeadSession;
            ses.StartSession();
            Sessions.TryAdd(sessionId, ses);

            OnClientAccepted?.Invoke(sessionId);
        }

        private void HandeDeadSession(Guid id)
        {
            OnClientDisconnected?.Invoke(id);
            if(Sessions.TryRemove(id, out _))
                Console.WriteLine("Removed "+id);
        }

        protected virtual IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var ses = new SslSession(guid, tuple.Item1);
            ses.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            ses.DropOnCongestion = DropOnBackPressure;
            ses.RemoteEndpoint = tuple.Item2;

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                ses.UseQueue = true;
            else
                ses.UseQueue = false;

            return ses;
        }

        private void HandleBytesReceived(Guid arg1, byte[] arg2, int arg3, int arg4)
        {
            OnBytesReceived?.Invoke(arg1, arg2, arg3, arg4);
        }

        public override void SendBytesToClient(in Guid clientId, byte[] bytes)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                session.SendAsync(bytes);
        }

        public void SendBytesToClient(in Guid clientId, byte[] bytes, int offset, int count)
        {
            if(Sessions.TryGetValue(clientId, out var session))
                session.SendAsync(bytes, offset, count);
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
            serverSocket.Close();
            serverSocket.Dispose();
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

        public override void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
        {
            statisticsPublisher.GetStatistics(out generalStats, out sessionStats);
        }

        public override IPEndPoint GetSessionEndpoint(Guid sessionId)
        {
            if (Sessions.TryGetValue(sessionId, out var session))
            {
                return session.RemoteEndpoint;
            }

            return null;
        }
    }
    
}
