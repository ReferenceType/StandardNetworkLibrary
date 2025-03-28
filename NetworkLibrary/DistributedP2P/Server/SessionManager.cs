using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto.DigitalSignature;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server
{
    class Flags
    {
        public const byte Route = 1;
    }


   

    // Creation, and managing destruction of sessions.
    //Created after Authentication.
    // Routing messages between sessions.
    // Where do we put rooms? probably here
    internal class SessionManager<S> where S : ISerializer, new()
    {
        internal ConcurrentDictionary<Guid, ServerSession> serverSessions = new ConcurrentDictionary<Guid, ServerSession>();


        IDistributedConnection serverConnection;

        public SessionManager(IDistributedConnection serverConnection)
        {
            this.serverConnection = serverConnection;
        }


        internal bool HandleMessage(Guid from, MessageEnvelope envelope)
        {
            switch (envelope.Header)
            {
                
            }
            return false;
        }

      

        public void CreateSession(IClientDbInfo clientInfo, Guid ephemeralClientId, IPEndPoint sessionEp)
        {
            serverSessions.TryAdd(ephemeralClientId, new ServerSession(clientInfo));
            //Send a feedback
        }

        internal void DestroySession(Guid guid)
        {

        }

    }
}
