using NetworkLibrary.DistributedP2P.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P
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

        public ServerSession()
        {
        }
    }
}
