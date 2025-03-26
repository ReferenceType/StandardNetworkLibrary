using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server
{
    public interface IClientDbInfo
    {
        bool IsValid { get; }
        string Error { get; }
        Guid ClientId { get; }
    }
    public interface IServerDbConnector
    {
        Task<IClientDbInfo> GetClientData(IAuthenticationResult result);
        Task<IClientDbInfo> RegisterClient(IAuthenticationResult tokenResult, byte[] payload);
    }
}
