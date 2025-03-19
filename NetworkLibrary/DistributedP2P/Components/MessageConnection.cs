using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Text;
using NetworkLibrary.MessageProtocol;


namespace NetworkLibrary.DistributedP2P.Components
{
    internal class MessageConnection<S>: IClientConnection where S : ISerializer, new()
    {
        SecureMessageServer<S> server;
        public MessageConnection(SecureMessageServer<S> serverConnection ) 
        {
            this.server = serverConnection;
        }


    }
}
