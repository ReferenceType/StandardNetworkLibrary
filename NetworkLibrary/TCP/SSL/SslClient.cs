using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace NetworkLibrary.TCP.SSL.Base
{
    public class SslClient:TcpClientBase
    {
        #region Fields & Props

        //public BytesRecieved OnBytesReceived;
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;

        protected Socket clientSocket;
        protected SslStream sslStream;
        internal IAsyncSession clientSession;
        private X509Certificate2 certificate;
        #endregion

        public SslClient(X509Certificate2 certificate)
        {
            this.certificate = certificate;
            RemoteCertificateValidationCallback += DefaultValidationCallbackHandler;
        }

        private Socket GetSocket()
        {
            Socket socket  = new Socket(SocketType.Stream, ProtocolType.Tcp);
            return socket;
        }
        #region Connect
        public override void Connect(string ip, int port)
        {
            try
            {
                IsConnecting = true;
                clientSocket = GetSocket();

                clientSocket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
                Connected(ip);
            }
            catch{ throw; }
            finally
            {
                IsConnecting = false;
            }
           
        }

        public override async Task<bool> ConnectAsyncAwaitable(string ip, int port)
        {
            try
            {
                IsConnecting = true;
                clientSocket = GetSocket();

                await clientSocket.ConnectAsync(ip, port);

                Connected(ip);
                return true;
            }
            catch { throw; }
            finally
            {
                IsConnecting = false;
            }
            
        }

        public override void ConnectAsync(string IP, int port)
        {
            Task.Run(async () =>
            {
                IsConnecting= true;
                bool result = false;
                try
                {
                    result = await ConnectAsyncAwaitable(IP, port);

                }
                catch (Exception ex) 
                {
                    OnConnectFailed?.Invoke(ex);
                    return;
                }
                finally { IsConnecting = false; }

                if (result)
                    OnConnected?.Invoke();
                
            });
        }


        private void Connected(string domainName)
        {
            
            sslStream = new SslStream(new NetworkStream(clientSocket, true), false, ValidateCeriticate);
            sslStream.AuthenticateAsClient(domainName,
            new X509CertificateCollection(new[] { certificate }), System.Security.Authentication.SslProtocols.Tls12, true);

            clientSession = CreateSession(Guid.NewGuid(), new ValueTuple<SslStream, IPEndPoint>(sslStream, (IPEndPoint)clientSocket.RemoteEndPoint));
            clientSession.OnBytesRecieved += HandleBytesReceived;
            clientSession.StartSession();

            IsConnecting = false;
            IsConnected = true;
        }
        #endregion Connect

        #region Validate
        

        protected virtual bool ValidateCeriticate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
           return RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
           
        }

        private bool DefaultValidationCallbackHandler(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            //return true;
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            return false;
        }

        #endregion Validate

        internal virtual IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var ses = new SslSession(guid, tuple.Item1);
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.OnSessionClosed +=(id)=> OnDisconnected?.Invoke();
            ses.RemoteEndpoint = tuple.Item2;

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                ses.UseQueue = true;
            else
                ses.UseQueue = false;

            return ses;
        }

        protected virtual void HandleBytesReceived(Guid sesonId, byte[] bytes, int offset, int count)
        {
            OnBytesReceived?.Invoke(bytes, offset, count);
        }

        public override void SendAsync(byte[] bytes)
        {
            clientSession.SendAsync(bytes);
        }

        public override void SendAsync(byte[] buffer, int offset, int count)
        {
            clientSession.SendAsync(buffer,offset,count);
        }

        public override void Disconnect()
        {
            clientSession?.EndSession();
        }
    }
}
