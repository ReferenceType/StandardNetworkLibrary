using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using static System.Collections.Specialized.BitVector32;

namespace NetworkLibrary.DistributedP2P.Server
{

    // should hold the data about the client. Keys etc everything.
    class PeerStatusList
    {
        public Guid WhoNeedsToKnow;
        public ConcurrentDictionary<Guid, PeerStatus> NewOnline = new ConcurrentDictionary<Guid, PeerStatus>();
        public ConcurrentDictionary<Guid, PeerStatus> WentOffline = new ConcurrentDictionary<Guid, PeerStatus>();

        public bool IsEmpty()
        {
            return NewOnline.Count == 0 && WentOffline.Count == 0;
        }
    }

    public class PeerStatus
    {
        public Guid EphemeralId;
        public Guid PeerId;
        public DateTime OnlineSince;
    }
    internal class ServerSession
    {
        public IClientDbInfo ClientInfo { get; }
        public Guid PeerId { get; internal set; }
        public Guid EphemeralId { get; internal set; }
        public DateTime OnlineSince { get; internal set; }


        private PeerStatusList statusList = new PeerStatusList();

        ConcurrentDictionary<Guid,PeerStatus> onlinePeers = new ConcurrentDictionary<Guid,PeerStatus>();
        PeerStatus status =  new PeerStatus();
        public ServerSession(IClientDbInfo clientInfo,Guid ephemeralId)
        {
            ClientInfo = clientInfo;
            EphemeralId = ephemeralId;
            PeerId = clientInfo.ClientId;
            statusList.WhoNeedsToKnow = EphemeralId;
            OnlineSince = DateTime.UtcNow;

            status.EphemeralId = EphemeralId;
            status.PeerId = PeerId;
            status.OnlineSince = OnlineSince;

        }

        internal bool Knows(Guid newPeer)
        {
            return true;
        }

        internal void AddNewPeer(Guid ephemeralId,PeerStatus status)
        {
            if (onlinePeers.TryAdd(ephemeralId, status))
            {
                statusList.NewOnline.TryAdd(ephemeralId, status);
                statusList.WentOffline.TryRemove(ephemeralId, out _);
            }
        }

        internal void RemovePeer(Guid ephemeralId)
        {
            if (onlinePeers.TryRemove(ephemeralId, out var status))
            {
                statusList.WentOffline.TryAdd(ephemeralId, status);
                statusList.NewOnline.TryRemove(ephemeralId, out _);
            }
        }

        internal PeerStatus GetPeerStatus()
        {
            return status;
        }

        internal PeerStatusList GetPublishInfo()
        {
            if (statusList.IsEmpty())
                return null;
            // must be hard copy
            // either hard copy or lock until all published over network.
            PeerStatusList list =  new PeerStatusList();
            list.WentOffline = new ConcurrentDictionary<Guid, PeerStatus>(statusList.WentOffline);
            list.NewOnline = new ConcurrentDictionary<Guid, PeerStatus>(statusList.NewOnline);
            list.WhoNeedsToKnow = EphemeralId;

            return list;
        }

        internal void ResetPublishInfo()
        {
            statusList.WentOffline.Clear();
            statusList.NewOnline.Clear();
        }
    }
}
