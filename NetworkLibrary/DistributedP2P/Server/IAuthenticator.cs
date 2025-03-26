using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server
{
    public interface IAuthenticationResult
    {
        bool IsValid { get;  }
        string UserId { get; }
        string Error { get; }
        IReadOnlyDictionary<string, string> Claims { get; }
    }
    public interface IAuthenticator
    {
        Task<IAuthenticationResult> Authenticate(string AuthenticationToken, string AuthenticationMethod, string Cookies);
    }
}
