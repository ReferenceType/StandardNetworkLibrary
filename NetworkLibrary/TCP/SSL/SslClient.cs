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
        private BufferProvider bufferProvider;
        #endregion

        public BufferProvider BufferProvider
        {
            get => bufferProvider;
            set
            {
                if (IsConnecting)
                    throw new InvalidOperationException("Setting buffer manager is not supported after conection is initiated.");
                bufferProvider = value;
            }
        }

        public SslClient(X509Certificate2 certificate)
        {
            this.certificate = certificate;
            RemoteCertificateValidationCallback += DefaultValidationCallbackHandler;
        }

        private Socket GetSocket()
        {
            Socket socket  = new Socket(SocketType.Stream, ProtocolType.Tcp);
            // somehow there is a huge performance impact when you set this buffer sizes.. 
            //socket.SendBufferSize = SocketSendBufferSize;
            //socket.ReceiveBufferSize = SocketRecieveBufferSize;
            return socket;
        }
        #region Connect
        public override void Connect(string ip, int port)
        {
            CheckBufferProvider();
            IsConnecting = true;
            clientSocket =  GetSocket();
          
            clientSocket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
            Connected(ip);
        }

        public override async Task<bool> ConnectAsyncAwaitable(string ip, int port)
        {
            CheckBufferProvider();
            IsConnecting = true;
            clientSocket = GetSocket();
            
            await clientSocket.ConnectAsync(ip, port);

            Connected(ip);
            return true;
        }

        public override void ConnectAsync(string IP, int port)
        {
            Task.Run(async () =>
            {
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

                if (result)
                    OnConnected?.Invoke();
                
            });
        }


        private void Connected(string domainName)
        {
            
            sslStream = new SslStream(new NetworkStream(clientSocket, true), false, ValidateCeriticate);
            sslStream.AuthenticateAsClient(domainName,
            new X509CertificateCollection(new[] { certificate }), System.Security.Authentication.SslProtocols.Tls12, true);

            clientSession = CreateSession(Guid.NewGuid(), sslStream, BufferProvider);
            clientSession.OnBytesRecieved += HandleBytesReceived;
            clientSession.StartSession();

            IsConnecting = false;
            IsConnected = true;
        }
        #endregion Connect

        #region Validate
        private void CheckBufferProvider()
        {
            if(BufferProvider == null)
            {
                BufferProvider = new BufferProvider(1, SendBufferSize, 1, RecieveBufferSize);
            }
        }

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

        internal virtual IAsyncSession CreateSession(Guid guid, SslStream sslStream,BufferProvider bufferProvider)
        {
            var ses = new SslSession(guid, sslStream, bufferProvider);
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.OnSessionClosed +=(id)=> OnDisconnected?.Invoke();

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

        public void SendAsync(byte[] buffer, int offset, int count)
        {
            clientSession.SendAsync(buffer,offset,count);
        }

        public override void Disconnect()
        {
            clientSession.EndSession();
        }
    }
}
