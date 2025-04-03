using System;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Components
{
    public interface ITimeProvider
    {
        DateTime GetDateTime();
        double GetTime();

        Task<bool> SyncTime();
    }
}