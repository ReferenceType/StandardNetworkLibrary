using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.P2P.Generic;
using NetworkLibrary.UDP.Jumbo;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Generic.Room
{
    public class SecureLobbyServer<S> : SecureRelayServerBase<S> where S : ISerializer, new()
    {
        private ConcurrentDictionary<string, Room<S>>
            rooms = new ConcurrentDictionary<string, Room<S>>();

        private ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>
            peerToRoomMap = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>();

        // represents peers that have contacted. used for broadasting disconnect message 
        private ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>>
            peerRelationalMap = new ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>>();

        private object roomLock = new object();
        public int dispatchms = 10;

        AsyncDispatcher batchBroadcastDispatcher =  new AsyncDispatcher();
        AsyncDispatcher roomPublishOperation =  new AsyncDispatcher();
        ConcurrentDictionary<string, ConcurrentQueue<BroadcastBytes>> tcpBatchBrodacastCollector = new ConcurrentDictionary<string, ConcurrentQueue<BroadcastBytes>>();
        public int NumClients=> RegisteredPeers.Count;
        public SecureLobbyServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
          
        }

        public override void StartServer()
        {
            StartRoomPublisher();
            Task.Run(() => { StartPeriodicMessageDispatcher(); });
            base.StartServer();
        }
        private async void StartRoomPublisher()
        {
            try
            {
                await roomPublishOperation.ExecuteBySignal(() =>
                {
                   
                    MessageEnvelope roomList = CreateRoomListMsg();

                    foreach (var rp in RegisteredPeers)
                    {
                        SendAsyncMessage(rp.Key, roomList);
                    }
                    
                    
                }).ConfigureAwait(false);
            }
            catch (AggregateException e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error,
                    $"An Error occured on Room publisher: \n{e.InnerException.Message}\n{e.InnerException.StackTrace}");

                StartRoomPublisher();
            }

        }

        private async void StartPeriodicMessageDispatcher()
        {
            Dictionary<Guid, Queue<BroadcastBytes>> toDispatch = new Dictionary<Guid, Queue<BroadcastBytes>>();

            try
            {
                await batchBroadcastDispatcher.LoopPeriodic(() =>
                {

                    foreach (var roomBCMsgs in tcpBatchBrodacastCollector)
                    {

                        string roomName = roomBCMsgs.Key;
                        ICollection<Guid> roomPeerIds;

                        if (TryGetRoom(roomName, out Room<S> room))
                        {
                            roomPeerIds = room.GetPeerIdList();
                        }
                        else
                        {
                            if (tcpBatchBrodacastCollector.TryRemove(roomBCMsgs.Key, out ConcurrentQueue<BroadcastBytes> queue))
                            {
                                while (queue.TryDequeue(out BroadcastBytes msg))
                                {
                                    msg.ReturnBuffer();
                                }
                            }

                            continue;
                        }

                        List<BroadcastBytes> disposalList = new List<BroadcastBytes>(roomBCMsgs.Value.Count+10);
                        while (roomBCMsgs.Value.TryDequeue(out BroadcastBytes msgToBc))
                        {
                            Guid senderId = msgToBc.From;
                            disposalList.Add(msgToBc);

                            foreach (var destination in roomPeerIds)
                            {
                                if (destination != senderId)
                                {
                                    if (!toDispatch.ContainsKey(destination))
                                    {
                                        toDispatch[destination] = new Queue<BroadcastBytes>();
                                    }
                                    toDispatch[destination].Enqueue(msgToBc);// has duplicates..
                                }
                            }
                        }


                        Parallel.ForEach(toDispatch, toDispatchKv =>
                        {
                            var segments = new List<ArraySegment<byte>>();

                            while (toDispatchKv.Value.Count > 0)
                            {
                                BroadcastBytes msg = toDispatchKv.Value.Dequeue();
                                segments.Add(new ArraySegment<byte>(msg.Data, msg.Offset, msg.Count));

                            }

                            if (segments.Count > 0)
                                SendSegmentsToClient(toDispatchKv.Key, segments);

                        });

                        foreach (var container in disposalList)
                        {
                            container.ReturnBuffer();
                        }
                        disposalList.Clear();

                    }

                }, dispatchms).ConfigureAwait(false);

            }
            catch (AggregateException e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error,
                    $"An Error occured on Batch broadcast dispatch: \n{e.InnerException.Message}\n{e.InnerException.StackTrace}");

                StartPeriodicMessageDispatcher();
            }
        }

        private MessageEnvelope CreateRoomListMsg()
        {
            MessageEnvelope roomList = new MessageEnvelope();
            roomList.Header = Constants.RoomAvalibalilityUpdate;
            roomList.KeyValuePairs = new Dictionary<string, string>();
            lock (roomLock)
            {
                foreach (var item in rooms)
                {
                    roomList.KeyValuePairs.Add(item.Key, null);
                }
                return roomList;
            }
           
        }
       
        #region Overrides
        #region Override Disbale Behaviour
        protected override void NotifyCurrentPeerList(Guid clientId)
        {
            // skip, we dont publish peer registered anymore
        }

        protected override void PublishPeerRegistered(Guid clientId)
        {
            // this on base sets the task to publish peer list
            // only send available room to newcomer
            var roomList = CreateRoomListMsg();
            SendAsyncMessage(clientId, roomList);
        }
        #endregion

        protected override void PublishPeerUnregistered(Guid clientId)
        {

            // here we care because it could be dc.
            //TODO Send a Special Disconnect Message.... to who ??
            HashSet<Guid> notificationList = new HashSet<Guid>();
            lock (roomLock)
            {
                if (!peerToRoomMap.TryRemove(clientId, out var roomList))
                {
                    return;
                }
                foreach (var item in roomList)
                {
                    var roomName = item.Key;
                    if (TryGetRoom(roomName, out var room))
                    {
                        int remaining = room.Remove(clientId);
                        var peerList = room.GetPeerIdList();
                        foreach (var id in peerList)
                        {
                            notificationList.Add(id);
                        }
                        if (remaining == 0)
                        {
                            TryRemoveRoom(roomName, out _);
                            MiniLogger.Log(MiniLogger.LogLevel.Info, $"[{roomName}] Room Destroyed");
                            room.Close();
                        }
                      
                    }
                }
            }

            var msg = new MessageEnvelope()
            {
                Header = Constants.PeerDisconnected,
                From = clientId
            };
            foreach (var peerId in notificationList)
            {
                SendAsyncMessage(peerId, msg);
            }
        }

     
        protected override void HandleMessageReceivedInternal(Guid clientId, MessageEnvelope message)
        {
            if (message.Header == Constants.JoinRoom)
            {
                CreateOrJoinRoom(clientId, message);
            }
            else if (message.Header == Constants.LeaveRoom)
            {
                LeaveRoom(clientId, message);
            }
            else if (message.Header == Constants.GetAvailableRooms)
            {
                SendAvailableRooms(clientId, message);
            }
            else
            {
                base.HandleMessageReceivedInternal(clientId, message);
            }
        }

        private void SendAvailableRooms(Guid clientId, MessageEnvelope message)
        {
            lock (roomLock)
            {
                message.KeyValuePairs = new Dictionary<string, string>();
                foreach (var item in rooms)
                {
                    message.KeyValuePairs.Add(item.Key, null);
                }
            }
           
            SendAsyncMessage(clientId, message);
        }
        #endregion

        private void CreateOrJoinRoom(Guid clientId, MessageEnvelope message)
        {
            if (message.KeyValuePairs != null)
            {
                var roomName = message.KeyValuePairs.Keys.First();
                Room<S> room = null;
                MessageEnvelope peerListMsg = new MessageEnvelope();
                lock (roomLock)
                {
                    if (!TryGetRoom(roomName, out room))
                    {
                        room = new Room<S>(roomName, this);
                        TryAddRoom(roomName, room);
                        MiniLogger.Log(MiniLogger.LogLevel.Info, $"Room Created [{roomName}]");
                    }
                    try
                    {
                        room.LockRoom();
                        room.Add(clientId);

                        if (!peerToRoomMap.ContainsKey(clientId))
                            peerToRoomMap.TryAdd(clientId, new ConcurrentDictionary<string, string>());

                        peerToRoomMap[clientId].TryAdd(roomName, null);

                        peerListMsg = room.GetPeerListMsg();
                        peerListMsg.MessageId = message.MessageId;
                        SendAsyncMessage(clientId, peerListMsg);

                        MiniLogger.Log(MiniLogger.LogLevel.Info, $"{clientId} Joined the room [{roomName}]");
                    }
                    finally { room.UnlockRoom(); }
                   

                }
                // here send the room state, all the peers inside.
            }
        }

        private void LeaveRoom(Guid clientID, MessageEnvelope message)
        {
            if (message.KeyValuePairs != null)
            {
                var roomName = message.KeyValuePairs.Keys.First();
                lock (roomLock)
                {
                    if (TryGetRoom(roomName, out var room))
                    {
                        try
                        {
                            room.LockRoom();

                            int remaining = room.Remove(clientID);
                            if (remaining == 0)
                            {
                                TryRemoveRoom(roomName, out _);
                                MiniLogger.Log(MiniLogger.LogLevel.Info, $"[{roomName}] Room Destroyed");
                                room.Close();
                            }

                            if (peerToRoomMap.ContainsKey(clientID))
                                peerToRoomMap[clientID].TryRemove(roomName, out _);
                        }
                        finally 
                        { 
                            room.UnlockRoom();
                        }
                        
                    }
                    
                }
            }
        }

        class BroadcastBytes
        {
            public Guid From;
            public int Offset;
            public int Count;
            public byte[] Data => GetData();
            private byte[] GetData()
            {
                if (Interlocked.CompareExchange(ref released, 0, 0) == 1)
                {
                    throw new InvalidOperationException("Attempted to access released buffer");
                }
                return data;
            }

            private byte[] data;
            private int released = 0;
            private PooledMemoryStream stream;


            public BroadcastBytes(MessageEnvelope msg, GenericMessageSerializer<S> serializer)
            {
                this.From = msg.From;

                stream = SharerdMemoryStreamPool.RentStreamStatic();
                stream.Position = 0;
                serializer.EnvelopeMessageWithBytes(stream, msg, msg.Payload, msg.PayloadOffset, msg.PayloadCount);

                var buff = stream.GetBuffer();
                this.data = buff;
                this.Offset = 0; 
                this.Count = stream.Position32;
            }

            internal void ReturnBuffer()
            {
                if(Interlocked.Exchange(ref released, 1) == 0)
                {
                    var buff = Interlocked.Exchange(ref data, null);
                    SharerdMemoryStreamPool.ReturnStreamStatic(stream);
                }
               
            }
        }

        protected override void BroadcastMessage(Guid senderId, byte[] bytes, int offset, int count)
        {
            var messageEnvelope = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
            messageEnvelope.From = senderId;

            if (messageEnvelope.KeyValuePairs != null)
            {

                if (messageEnvelope.KeyValuePairs.TryGetValue(Constants.RoomBroadcast, out string roomName))
                {
                    TryGetRoom(roomName, out var room);
                    var snapshot = room.GetPeerIdList();

                    messageEnvelope.KeyValuePairs.Remove(Constants.RoomBroadcast);
                    if (messageEnvelope.KeyValuePairs.Count == 0)
                        messageEnvelope.KeyValuePairs = null;

                    foreach (var peerId in snapshot)
                    {
                        if (peerId != senderId)
                            SendAsyncMessage(peerId, messageEnvelope);
                    }
                }
                else if (messageEnvelope.KeyValuePairs.TryGetValue(Constants.RoomBroadcastBatched, out  roomName))
                {
                    messageEnvelope.KeyValuePairs.Remove(Constants.RoomBroadcastBatched);
                    if (messageEnvelope.KeyValuePairs.Count == 0)
                        messageEnvelope.KeyValuePairs = null;

                    PushBatchQueueTcp(roomName, messageEnvelope);
                }
            }
        }

        private void PushBatchQueueTcp(string roomName,MessageEnvelope msg)
        {
            var queue = tcpBatchBrodacastCollector.GetOrAdd(roomName, new ConcurrentQueue<BroadcastBytes>());
            queue.Enqueue(new BroadcastBytes(msg,serialiser));
        }

        // we have to do something about this in future when we have reliable udp.
        protected override void BroadcastUdp(byte[] buffer, int lenght)
        {
            var messageEnvelope = serialiser.DeserialiseEnvelopedMessage(buffer, 0, lenght);
            peerReachabilityMatrix.TryGetValue(messageEnvelope.From, out var edgeMap);
            if (messageEnvelope.KeyValuePairs != null)
            {
                if (messageEnvelope.KeyValuePairs.TryGetValue(Constants.RoomBroadcast, out string roomName))
                {
                    // find the room
                    TryGetRoom(roomName, out var room);
                    var peerIdList = room.GetPeerIdList();
                    foreach (var peerId in peerIdList)
                    {
                        if (edgeMap != null && edgeMap.TryGetValue(peerId, out _))
                        {
                            continue;
                        }

                        if (messageEnvelope.From != peerId)
                            RelayUdpMessage(peerId, buffer, 0, lenght);

                    }
                }
               
            } 
        }
       
        private bool TryGetRoom(string roomName, out Room<S> room)
        {
            return rooms.TryGetValue(roomName, out room);
        }

        private bool TryRemoveRoom(string roomName, out Room<S> room)
        {
            if( rooms.TryRemove(roomName, out room))
            {
                roomPublishOperation.Signal();
                return true;
            }
            return false;

        }

        private bool TryAddRoom(string roomName, Room<S> room)
        {
            if( rooms.TryAdd(roomName, room))
            {
                roomPublishOperation.Signal();
                return true;
            }
            return false;
        }

        public override void ShutdownServer()
        {
            batchBroadcastDispatcher.Abort();
            roomPublishOperation.Abort();

            base.ShutdownServer();
        }

        internal class Room<T> where T : ISerializer, new()
        {
            public readonly string RoomName;
            private readonly SecureLobbyServer<T> server;
            private ConcurrentDictionary<Guid, double> roomMates = new ConcurrentDictionary<Guid, double>();
            private AsyncDispatcher publishChangesOperation = new AsyncDispatcher();

            SemaphoreSlim publishLock =  new SemaphoreSlim(1,1);
            public Room(string roomName, SecureLobbyServer<T> server )
            {
                RoomName = roomName;
                this.server = server;
                PublishRotune();
            }
            public void LockRoom()
            {
                publishLock.Wait();
            }
            public void UnlockRoom() 
            {
                publishLock.Release();
            }
            private async void PublishRotune()
            {
                await publishChangesOperation.ExecuteBySignalEfficient(async () => 
                {
                    try
                    {
                        await publishLock.WaitAsync();
                        try 
                        {
                            RoomPeerList rpl = GenerateRoomPeerList();
                            MessageEnvelope message = PrepareMessage(rpl, out var toReturn);
                            
                            foreach (var peer in roomMates)
                            {
                                server.SendAsyncMessage(peer.Key, message);
                            }
                            SharerdMemoryStreamPool.ReturnStreamStatic(toReturn);
                            
                        }
                        finally
                        {
                            publishLock.Release(); 
                        }
                        
                    }
                    catch(Exception e)
                    {
                        MiniLogger.Log(MiniLogger.LogLevel.Error,
                                 $"An Error occured on Batch broadcast dispatch: " +
                                 $"\n{e.InnerException.Message}" +
                                 $"\n{e.InnerException.StackTrace}");

                        PublishRotune();
                    }
                  
                });

                
            }

            public MessageEnvelope GetPeerListMsg()
            {
                RoomPeerList rpl = GenerateRoomPeerList();
                MessageEnvelope message = PrepareMessage(rpl, out var toReturn);
                message.LockBytes();
                SharerdMemoryStreamPool.ReturnStreamStatic(toReturn);
                return message;
            }

            private MessageEnvelope PrepareMessage(RoomPeerList rpl, out PooledMemoryStream returnAfterFinished)
            {
                var message = new MessageEnvelope()
                {
                    Header = Constants.RoomUpdate,
                };

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                KnownTypeSerializer.SerializeRoomPeerList(stream, rpl);
                message.SetPayload(stream.GetBuffer(), 0, stream.Position32);

                returnAfterFinished = stream;
                return message;
            }

            private RoomPeerList GenerateRoomPeerList()
            {
                RoomPeerList rpl = new RoomPeerList();
                rpl.RoomName = RoomName;
                rpl.Peers = new PeerList();
                rpl.Peers.PeerIds = new Dictionary<Guid, PeerInfo>();
               
               
                foreach (var item in roomMates)
                {
                    var ep = server.GetIPEndPoint(item.Key);
                    var peerInfo = new PeerInfo()
                    {
                        Address = ep.Address.GetAddressBytes(),
                        Port = (ushort)ep.Port,
                        RegisteryTime = item.Value

                    };
                    rpl.Peers.PeerIds[item.Key] = peerInfo;
                }
             
               
                return rpl;
            }

            public void Add(Guid peerId)
            {
                if (roomMates.TryAdd(peerId, server.GetTime()))
                    publishChangesOperation.Signal();
            }

            public int Remove(Guid peerId)
            {
                if (roomMates.TryRemove(peerId, out _))
                {
                    publishChangesOperation.Signal();
                }

                   
                var cnt =  roomMates.Count;
                return cnt;
            }

            internal ICollection<Guid> GetPeerIdList()
            {
                return roomMates.Keys;   
            }


            public void Close()
            {
                publishChangesOperation.Abort();
            }
        }

      
    }

}
