using NetworkLibrary.DistributedP2P.SimpleRelay;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace NetworkLibrary.DistributedP2P.Server
{
    public class RoomInfo
    {
        public Guid RoomId { get; set; }
        public string Name { get; set; }

        public byte[] RoomKey;

        public ConcurrentDictionary<Guid, byte> Peers = new ConcurrentDictionary<Guid, byte>();
        private ConcurrentQueue<byte> idStack = new ConcurrentQueue<byte>();

        public RoomInfo()
        {
            for (byte i = 0; i < byte.MaxValue; i++)
            {
                idStack.Enqueue(i);
            }
        }

        internal bool AddPeer(Guid peerId, out byte idAlias)
        {
            idAlias = 0;
            if (idStack.TryDequeue(out byte alias))
            {
                if (Peers.TryAdd(peerId, alias))
                {
                    idAlias = alias;
                    return true;
                }
                else
                {
                    idStack.Enqueue(alias);
                    return false;
                }
            }
            return false;

        }
    }
    class RoomResult
    {
        public RoomInfo RoomInfo;
        public bool IsCreated;
        public byte idAlias;
        public byte[] RoomToken;
    }
    internal class RoomManager
    {
        ConcurrentDictionary<string, RoomInfo> rooms = new ConcurrentDictionary<string, RoomInfo>();
        RandomNumberGenerator rng =  RandomNumberGenerator.Create();
        internal RoomResult CreateOrJoinRoom(string roomName, string roomPassword, Guid peerId, RoomProtocol protocol)
        {
            RoomResult result = new RoomResult();
            if (rooms.TryGetValue(roomName, out var roomInfo))
            {
                if (roomInfo.AddPeer(peerId, out var Idalias))
                {
                    result.RoomInfo = roomInfo;
                    result.IsCreated = false;
                    result.idAlias = Idalias;
                    return result;
                }
                return null;
            }

            var roomInf = new RoomInfo();
            roomInf.RoomId = Guid.NewGuid();
            roomInf.RoomKey = new byte[16];
            rng.GetBytes(roomInf.RoomKey);

            roomInf.AddPeer(peerId, out byte alias);

            rooms.TryAdd(roomName, roomInf);
            result.RoomInfo = roomInfo;
            result.IsCreated = false;
            result.idAlias = alias;

            return result;

        }
    }
}
