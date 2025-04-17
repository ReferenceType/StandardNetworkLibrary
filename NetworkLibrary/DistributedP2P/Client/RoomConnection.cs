using NetworkLibrary.DistributedP2P.Channels.Components;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;

namespace NetworkLibrary.DistributedP2P.Client
{
    public class RoomConnection
    {
        internal IChannel communicationChannel;
        internal byte assignedId;
        internal Guid roomId;

        ConcurrentDictionary<byte, Guid> aliasMap = new ConcurrentDictionary<byte, Guid>();

        public event Action ConnectionLost;
        public event Action<Guid, byte[], int, int> MessageReceived;
        public event Action<Guid> PeerJoined;
        public event Action<Guid> PeerLeft;

        internal event Action LeaveRequested;

        public RoomConnection(IChannel communicationChannel, Guid roomId, byte assignedId)
        {
            this.communicationChannel = communicationChannel;
            this.roomId = roomId;
            this.assignedId = assignedId;

            communicationChannel.OnBytesReceived += BytesReceived;
        }
        // server should call this directly
        internal void PeerJoinedRoom(Guid peerId, byte alias)
        {
            aliasMap.TryAdd(alias, peerId);
            PeerJoined?.Invoke(peerId);
        }

        internal void PeerLeftRoom(Guid peerId, byte alias)
        {
            aliasMap.TryRemove(alias, out _);
            PeerLeft?.Invoke(peerId);
        }

        private void BytesReceived(byte[] buffer, int offset, int count)
        {
            byte alias = buffer[offset++];
            count--;

            if (aliasMap.TryGetValue(alias, out var guid))
            {
                MessageReceived?.Invoke(guid, buffer, offset, count);
            }
        }

        public void Send(byte[] buffer, int offset, int count)
        {
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.WriteByte(assignedId);
            stream.Write(buffer, offset, count);

            communicationChannel.Send(stream.GetBuffer(), 0, stream.Position32);

            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

        //public void SendReliable(byte[] buffer, int offset, int count)
        //{
        //    if(communicationChannel.Info.ChannelType == ChannelType.Udp)
        //        ((UdpChannel)communicationChannel).SendReliable()
        //}


        public void LeaveRoom()
        {

        }

    }
}