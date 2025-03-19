using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Components
{
    // Creation, and managing destruction of sessions.
    //Created after Authentication.
    // Routing messages between sessions.
    // Where do we put rooms? probably here
    internal class SessionManager<S> where S : ISerializer, new()
    {
        internal ConcurrentDictionary<Guid,ServerSession> serverSessions = new ConcurrentDictionary<Guid,ServerSession>();

        internal void HandleMessage(Guid guid, MessageEnvelope envelope)
        {
           
        }

        public void CreateSession(IClientConnection messageConnection1, IAuthenticationResult result, Guid guid, IPEndPoint sessionEp)
        {
            serverSessions.TryAdd(guid, new ServerSession());
        }

        internal void RouteP2PMessageTcp(Guid guid, byte[] bytes, int offset, int count)
        {
            
        }

        internal void RouteP2PMessageUdp(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
           
        }

        internal void HandleDisconnect(Guid guid)
        {
           
        }

        internal void HandleTcpPipeDisconnect(Guid guid)
        {
            
        }

        internal void TcpPipeCreated(Guid guid)
        {
           
        }

        internal void CreateSession<S>(MessageConnection<S> messageConnection, Guid guid, IPEndPoint sessionEp) where S : ISerializer, new()
        {
            throw new NotImplementedException();
        }
    }
}
