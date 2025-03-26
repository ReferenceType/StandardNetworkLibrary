using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Server
{
    enum Sessionstate
    {
        Uninitialized,
        Authenticating,
        ObtainingClientInfo,
        EstablishingUdp,
        Running
    }

    // should hold the data about the client. Keys etc everything.

    internal class ServerSession
    {
        public Sessionstate state;
        public IClientDbInfo ClientInfo { get; }
        public ServerSession(IClientDbInfo clientInfo)
        {
            ClientInfo = clientInfo;
        }


    }
}
