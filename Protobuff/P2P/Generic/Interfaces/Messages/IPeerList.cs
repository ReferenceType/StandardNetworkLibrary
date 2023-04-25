using System;
using System.Collections.Generic;

namespace Protobuff.P2P.Generic.Interfaces.Messages
{
    public interface IPeerList<T>
    {
        Dictionary<Guid, T> PeerIds { get; set; }
    }
}