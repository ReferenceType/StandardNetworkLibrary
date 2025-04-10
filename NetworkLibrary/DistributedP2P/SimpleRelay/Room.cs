using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkLibrary.DistributedP2P.SimpleRelay
{
    public enum RoomProtocol
    {
        Tcp,
        Udp,
    }
    internal class Room
    {
        public Guid roomId;
        public readonly RoomProtocol protocol;
        public Action<PeerRoomState> PeerRegistered;
        public Action<PeerRoomState> PeerLeft;
        public Action<PeerRoomState, byte[], int, int> SendMessage;
        public Action RoomDestroyed; 

        //peerId -> info
        ConcurrentDictionary<Guid, PeerRoomState> roster = new ConcurrentDictionary<Guid, PeerRoomState>();

        public Room(Guid roomId, RoomProtocol protocol)
        {
            this.roomId = roomId;
            this.protocol = protocol;
        }

        // peer id determined by true server
        internal void Add(Guid peerId, PeerRoomState roomState)
        {
           if( roster.TryAdd(peerId, roomState))
            {
                PeerRegistered?.Invoke(roomState);
            }
        }

        internal void HandleMessage(Guid from, byte[] bytes, int offset, int count)
        {
            foreach (var peerInfo in roster.Values) 
            {
                if (peerInfo.EphemeralId.Equals(from))
                    continue;

                SendMessage?.Invoke(peerInfo, bytes, offset, count);
            }
        }

        internal void HandleMessage(IPEndPoint from, byte[] bytes, int offset, int count)
        {
            foreach (var peerInfo in roster.Values)
            {
                if (peerInfo.AssociatedEndpoint.Equals(from))
                    continue;

                SendMessage?.Invoke(peerInfo, bytes, offset, count);
            }
        }

        internal bool RemovePeer(Guid peerId)
        {
            bool removed = roster.TryRemove(peerId, out var st);
            if (removed)
            {
                PeerLeft?.Invoke(st);

                if (roster.Count == 0)
                {
                    RoomDestroyed?.Invoke();
                }
            }
            
            return removed;
        }

        public void Clear()
        {
            PeerRegistered = null;
            PeerLeft = null;
            SendMessage = null;
            RoomDestroyed = null;
        }

        //internal IPEndPoint GetEndpoint(Guid to)
        //{
        //    roster.TryGetValue(to, out PeerRoomState state);
        //    return state.AssociatedEndpoint;
        //}

     
    }
}
