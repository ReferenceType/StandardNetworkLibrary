using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkLibrary.DistributedP2P.SimpleRelay
{
    internal class PeerRoomState
    {
        public Guid RoomId { get; internal set; }
        public Guid ExpectedClient { get; internal set; }
        public Guid EphemeralId { get; internal set; }
        public IPEndPoint AssociatedEndpoint { get; internal set; }
        public bool isTcp;

        internal bool Verify(PipeToken token, Guid ephemeralId)
        {
            if(ExpectedClient.Equals(token.Token))
            {
                isTcp = true;
                EphemeralId = ephemeralId;
                return true;
            }
            return false;
        }

        internal bool Verify(PipeToken token, IPEndPoint clientEp)
        {
            if (ExpectedClient.Equals(token.Token))
            {
                AssociatedEndpoint = clientEp;
                return true;
            }
            return false;
        }
    }
}
