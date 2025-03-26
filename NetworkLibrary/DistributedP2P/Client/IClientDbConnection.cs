using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Client
{
    public interface IClientDbConnection
    {
        byte[] GetClientPublicData();
    }
}
