using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Components
{
    public interface IAuthenticationResult 
    {
        bool Success { get;}
    }
    public interface IAuthenticator
    {
        IAuthenticationResult Authenticate(IClientConnection messageConnection, Guid guid);
    }
}
