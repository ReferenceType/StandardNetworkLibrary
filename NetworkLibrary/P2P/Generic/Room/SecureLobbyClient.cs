using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.P2P.Generic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Generic.Room
{
    public class SecureLobbyClient<S> where S : ISerializer, new()
    {
        public Guid SessionId => client.SessionId;
        public bool IsConnected => client.IsConnected;

        public Action<string, Guid, PeerInfo> OnPeerJoinedRoom;
        public Action<string, Guid, PeerInfo> OnPeerLeftRoom;
        public Action<Guid> OnPeerDisconnected;
        //public Action<string, MessageEnvelope> OnTcpRoomMesssageReceived;
        //public Action<string, MessageEnvelope> OnUdpRoomMesssageReceived;
        public Action<MessageEnvelope> OnTcpMessageReceived;
        public Action<MessageEnvelope> OnUdpMessageReceived;
        public Action<List<string>> AvailableRoomsChanged;
        public Action OnDisconnected;
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;
        private RelayClientBase<S> client;

        private ConcurrentDictionary<string, Room>
            rooms = new ConcurrentDictionary<string, Room>();

        // [peerId] => Collection<RoomName>
        private ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>
            peersInRooms = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>();
        public SecureLobbyClient(X509Certificate2 clientCert)
        {
            client = new RelayClientBase<S>(clientCert);
            client.OnMessageReceived += HandleMessage;
            client.OnUdpMessageReceived += HandleUdpMessage;
            client.OnDisconnected += HandleDisconnected;
            client.RemoteCertificateValidationCallback += CertificateValidation;
          
        }
        public Task<bool> SyncTime() => client.SyncTime();
        public void StartAutoTimeSync(int periodMS, bool disconnectOnTimeout, bool usePTP = false) => client.StartAutoTimeSync(periodMS,disconnectOnTimeout, usePTP);

        public void StopAutoTimeSync()=>client.StopAutoTimeSync();

        public double GetTime() => client.GetTime();
        public DateTime GetDateTime() => client.GetDateTime();
        private bool CertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (RemoteCertificateValidationCallback == null)
                return true;
            return RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
        }

        public Task<List<ServerInfo>> TryFindRelayServer(int port)
        {
            return client.TryFindRelayServer(port);
        }

        public Task<bool> ConnectAsync(string ip, int port)
        {
            return client.ConnectAsync(ip, port);
        }

        public void Connect(string ip, int port)
        {
            client.Connect(ip, port);
        }

        public Task<bool> RequestHolePunchAsync(Guid destinationId, int timeot = 10000)
        {
            return client.RequestHolePunchAsync(destinationId, timeot);
        }

        public Task<bool> RequestTcpHolePunchAsync(Guid destinationId, int timeot = 10000)
        {
            return client.RequestTcpHolePunchAsync(destinationId);
        }

        public RoomPeerList CreateOrJoinRoom(string roomName)
        {
            return CreateOrJoinRoomAsync(roomName).Result;
        }


        public Task<RoomPeerList> CreateOrJoinRoomAsync(string roomName)
        {
            var returnVal = new TaskCompletionSource<RoomPeerList>();

            var message = new MessageEnvelope();
            message.IsInternal = true;
            message.Header = Constants.JoinRoom;
            message.KeyValuePairs = new Dictionary<string, string>()
            {
                {roomName ,null}
            };

            rooms.TryAdd(roomName, new Room(roomName));

            client.tcpMessageClient.SendMessageAndWaitResponse(message)
                .ContinueWith((response) =>
                {
                    if (response.Result.Header != MessageEnvelope.RequestTimeout)
                    {
                        var currentList = KnownTypeSerializer.DeserializeRoomPeerList(response.Result.Payload, response.Result.PayloadOffset);
                        returnVal.TrySetResult(currentList);
                    }
                    else
                    {
                        rooms.TryRemove(roomName, out _);
                        returnVal.TrySetResult(null);
                    }
                });
            return returnVal.Task;
        }

        public void LeaveRoom(string roomName)
        {
            rooms.TryRemove(roomName, out _);

            var message = new MessageEnvelope();
            message.IsInternal = true;
            message.Header = Constants.LeaveRoom;
            message.KeyValuePairs = new Dictionary<string, string>()
            {
                {roomName ,null}
            };
            client.tcpMessageClient.SendAsyncMessage(message);
        }

        public Task<List<string>> GetAvailableRooms()
        {
            var returnVal = new TaskCompletionSource<List<string>>();
            var message = new MessageEnvelope();
            message.Header = Constants.GetAvailableRooms;
            message.IsInternal = true;

            client.tcpMessageClient.SendMessageAndWaitResponse(message)
                .ContinueWith((response) =>
                {
                    if (response.Result.Header != MessageEnvelope.RequestTimeout)
                    {
                        returnVal.TrySetResult(response.Result.KeyValuePairs.Keys.ToList());
                    }
                    else
                    {
                        returnVal.TrySetResult(null);
                    }
                });
            return returnVal.Task;
        }

        #region Send
        private bool CanSend(string roomName)
        {
            if (rooms.TryGetValue(roomName, out var room))
            {
                if (room.PeerCount > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void PrepareEnvelopeBC(string roomName, ref MessageEnvelope messageEnvelope)
        {
            if (messageEnvelope.KeyValuePairs == null)
                messageEnvelope.KeyValuePairs = new Dictionary<string, string>();

            messageEnvelope.KeyValuePairs[Constants.RoomBroadcast] = roomName;
            messageEnvelope.To = Guid.Empty;
            messageEnvelope.From = Guid.Empty;
        }

        private void PrepareEnvelopeBCBatched(string roomName, ref MessageEnvelope messageEnvelope)
        {
            if (messageEnvelope.KeyValuePairs == null)
                messageEnvelope.KeyValuePairs = new Dictionary<string, string>();

            messageEnvelope.KeyValuePairs[Constants.RoomBroadcastBatched] = roomName;
            messageEnvelope.To = Guid.Empty;
            messageEnvelope.From = Guid.Empty;
        }

        private void PrepareEnvelopeDM(string roomName, ref MessageEnvelope messageEnvelope)
        {
            if (messageEnvelope.KeyValuePairs == null)
                messageEnvelope.KeyValuePairs = new Dictionary<string, string>();

            messageEnvelope.KeyValuePairs[Constants.RoomBroadcast] = roomName;
            messageEnvelope.From = client.SessionId;
        }

        #region Room Messages

        public void BroadcastMessageToRoom(string roomName, MessageEnvelope message)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                client.tcpMessageClient.SendAsyncMessage(message);
            }
        }

        public void BroadcastMessageToRoom<T>(string roomName, MessageEnvelope message, T innerMessage)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                client.tcpMessageClient.SendAsyncMessage(message, innerMessage);
            }
        }

        public void BroadcastUdpMessageToRoom(string roomName, MessageEnvelope message)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                if (rooms.TryGetValue(roomName, out var room))
                    client.MulticastUdpMessage(message, room.PeerIds);
            }
        }

        public void BroadcastUdpMessageToRoom<T>(string roomName, MessageEnvelope message, T innerMessage)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                if (rooms.TryGetValue(roomName, out var dict))
                    client.MulticastUdpMessage(message, dict.PeerIds, innerMessage);
            }
        }

        public void BroadcastRudpMessageToRoom(string roomName, MessageEnvelope message, RudpChannel channel = RudpChannel.Ch1)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                if (rooms.TryGetValue(roomName, out var roomDict))
                {
                    foreach (var peerId in roomDict.PeerIds)
                    {
                        if (peerId != SessionId)
                            client.SendRudpMessage(peerId, message, channel);
                    }
                }
            }
        }

        public void BroadcastRudpMessageToRoom<T>(string roomName, MessageEnvelope message, T innerMessage, RudpChannel channel = RudpChannel.Ch1)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                if (rooms.TryGetValue(roomName, out var roomDict))
                {
                    foreach (var peerId in roomDict.PeerIds)
                    {
                        if (peerId != SessionId)
                            client.SendRudpMessage(peerId, message, innerMessage, channel);
                    }
                }
            }
        }

        //---
        public void BroadcastMessageToRoomBatched(string roomName, MessageEnvelope message)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBCBatched(roomName, ref message);
                client.tcpMessageClient.SendAsyncMessage(message);
            }
        }

        public void BroadcastMessageToRoomBatched<T>(string roomName, MessageEnvelope message, T innerMessage)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBCBatched(roomName, ref message);
                client.tcpMessageClient.SendAsyncMessage(message, innerMessage);
            }
        }

        //---
        #endregion

        #region Direct Messages
        //Tcp
        public void SendAsyncMessage(Guid peerId, MessageEnvelope message)
        {
            client.SendAsyncMessage(peerId, message);
        }
        public void SendAsyncMessage<T>(Guid peerId, T message, string messageHeader = null)
        {
            client.SendAsyncMessage(peerId, message, messageHeader);
        }
        public void SendAsyncMessage<T>(Guid peerId, MessageEnvelope message, T innerMessage)
        {
            client.SendAsyncMessage(peerId, message, innerMessage);
        }

        public Task<MessageEnvelope> SendRequestAndWaitResponse(Guid peerId, MessageEnvelope message, int timeoutMs = 10000)
        {
            return client.SendRequestAndWaitResponse(peerId, message, timeoutMs);
        }

        public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid peerId, MessageEnvelope message,T innerMessage, int timeoutMs = 10000)
        {
            return client.SendRequestAndWaitResponse(peerId, message, innerMessage, timeoutMs);
        }
        public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid peerId, T innerMessage, string messageHeader = null, int timeoutMs = 10000)
        {
            return client.SendRequestAndWaitResponse(peerId, innerMessage, messageHeader, timeoutMs);
        }
        //---
        // Udp
        public void SendUdpMessage(Guid peerId, MessageEnvelope message)
        {
            client.SendUdpMessage(peerId, message);
        }

        public void SendUdpMessage<T>(Guid peerId, MessageEnvelope message, T innerMessage)
        {
            client.SendUdpMessage(peerId, message, innerMessage);
        }

        //--
        // Rudp
        public void SendRudpMessage(Guid peerId, MessageEnvelope message, RudpChannel channel = RudpChannel.Ch1)
        {
            client.SendRudpMessage(peerId, message, channel);
        }
        public void SendRudpMessage<T>(Guid peerId, MessageEnvelope message, T innerMessage, RudpChannel channel = RudpChannel.Ch1)
        {
            client.SendRudpMessage(peerId, message, innerMessage,channel);
        }
        public Task<MessageEnvelope> SendRudpMessageAndWaitResponse(Guid peerId, MessageEnvelope message, int timeoutMs = 10000,RudpChannel channel =  RudpChannel.Ch1)
        {
            return client.SendRudpMessageAndWaitResponse(peerId, message, timeoutMs, channel);
        }
        public Task<MessageEnvelope> SendRudpMessageAndWaitResponse<T>(Guid peerId, MessageEnvelope message, T innerMessage, int timeoutMs = 10000, RudpChannel channel = RudpChannel.Ch1)
        {
            return client.SendRudpMessageAndWaitResponse(peerId, message, innerMessage, timeoutMs,channel);
        }
        //--
        #endregion

        #endregion

        private void HandleUdpMessage(MessageEnvelope message)
        {
            //if (message.KeyValuePairs != null && message.KeyValuePairs.TryGetValue(Constants.RoomName, out string roomName))
            //{
            //    OnUdpRoomMesssageReceived?.Invoke(roomName, message);
            //}elseif
            if(peersInRooms.ContainsKey(message.From))
            {
                OnUdpMessageReceived?.Invoke(message);
            }
        }
        private void HandleMessage(MessageEnvelope message)
        {
            if (message.Header == Constants.RoomUpdate)
            {
                UpdateRooms(message);
            }
            else if(message.Header == Constants.RoomAvalibalilityUpdate)
            {
                AvailableRoomsChanged?.Invoke(message.KeyValuePairs?.Keys?.ToList()?? new List<string>());
            }
            else if (message.Header == Constants.PeerDisconnected)
            {
                HandlePeerDisconnected(message);
            }
            else
                HandleTcpReceived(message);
        }

        private void HandleTcpReceived(MessageEnvelope message)
        {
            //if (message.KeyValuePairs != null && message.KeyValuePairs.TryGetValue(Constants.RoomName, out string roomName))
            //{
            //    OnTcpRoomMesssageReceived?.Invoke(roomName, message);
            //}elseif
            if(peersInRooms.ContainsKey(message.From))
            {
                OnTcpMessageReceived?.Invoke(message);
            }
        }

        private void UpdateRooms(MessageEnvelope message)
        {
            var roomUpdateMessage = KnownTypeSerializer.DeserializeRoomPeerList(message.Payload, message.PayloadOffset);

            Dictionary<Guid, PeerInfo> JoinedList = new Dictionary<Guid, PeerInfo>();
            Dictionary<Guid, PeerInfo> LeftList = new Dictionary<Guid, PeerInfo>();

            var remoteList = roomUpdateMessage.Peers.PeerIds;
            if (!rooms.TryGetValue(roomUpdateMessage.RoomName, out var localRoom))
            {
                return;
            }
            
            if(remoteList.ContainsKey(SessionId))
                remoteList.Remove(SessionId);

            foreach (var remotePeer in remoteList)
            {
                if (!localRoom.ContainsPeer(remotePeer.Key))
                {
                    JoinedList.Add(remotePeer.Key, remotePeer.Value);
                }

            }

            foreach (var localPeerId in localRoom.PeerIds)
            {
                if (!remoteList.ContainsKey(localPeerId))
                {
                    localRoom.TryGetPeerInfo(localPeerId, out PeerInfo peerInfo);// cant fail
                    LeftList[localPeerId] = peerInfo;
                }

            }

            foreach (var peerKV in JoinedList)
            {
                if (rooms.TryGetValue(roomUpdateMessage.RoomName, out var room))
                {
                    room.Add(peerKV.Key, peerKV.Value);
                }

                if(!peersInRooms.TryGetValue(peerKV.Key, out var roomList))
                {
                    peersInRooms.TryAdd(peerKV.Key, new ConcurrentDictionary<string, string>());
                }
                peersInRooms[peerKV.Key].TryAdd(roomUpdateMessage.RoomName, null);

                client.HandleRegistered(peerKV.Key, roomUpdateMessage.Peers.PeerIds);
                OnPeerJoinedRoom?.Invoke(roomUpdateMessage.RoomName, peerKV.Key, peerKV.Value);
            }

            foreach (var peerId in LeftList)
            {
                if (rooms.TryGetValue(roomUpdateMessage.RoomName, out var room))
                {
                    room.Remove(peerId.Key);
                }

                if (peersInRooms.TryGetValue(peerId.Key, out var roomList))
                {
                    roomList.TryRemove(roomUpdateMessage.RoomName, out _);
                    if(roomList.Count == 0)
                    {
                        peersInRooms.TryRemove(peerId.Key, out _);
                        HandlePeerDisconnected_(peerId.Key);
                    }
                }
               
                //client.HandleUnRegistered(peerId);
                OnPeerLeftRoom?.Invoke(roomUpdateMessage.RoomName, peerId.Key, peerId.Value);
            }
        }

        private void HandlePeerDisconnected(MessageEnvelope message)
        {
            HandlePeerDisconnected_(message.From);
        }
        private void HandlePeerDisconnected_(Guid peerId)
        {
            if (client.Peers.TryRemove(peerId, out _))
            {
                client.HandleUnRegistered(peerId);
                OnPeerDisconnected?.Invoke(peerId);
            }
        }
        private void HandleDisconnected()
        {
            foreach (var room in rooms)
            {
                foreach (var peerId in room.Value.PeerIds)
                {
                    OnPeerDisconnected?.Invoke(peerId);
                }
            }
            peersInRooms.Clear();
            rooms.Clear();
            OnDisconnected?.Invoke();
        }
        public bool TryGetRoommateInfo(string roomName, Guid id, out PeerInfo info)
        {
            info = null;
            if (roomName == null) return false;
            if (rooms.TryGetValue(roomName, out Room room))
            {
                if (room.TryGetPeerInfo(id, out info))
                    return true;
            }
            return false;
        }

        public PeerInformation GetPeerInfo(Guid clientId)
        {
            return client.GetPeerInfo(clientId);
        }
        public bool TryGetRoommateIds(string roomName, out ICollection<Guid> peerIds)
        {
            peerIds = null;
            if (roomName == null) return false;
            if (rooms.TryGetValue(roomName, out Room room))
            {
                peerIds = room.PeerIds;
                return true;
            }
            return false;
        }

        public void Disconnect()
        {
            client.Disconnect();
        }

        public void Dispose()
        {
            client.Dispose();
            peersInRooms.Clear();
            rooms.Clear();
        }

        class Room
        {
            public string roomName;
            private ConcurrentDictionary<Guid, PeerInfo> roomMates = new ConcurrentDictionary<Guid, PeerInfo>();

            public Room(string roomName)
            {
                this.roomName = roomName;
            }

            public int PeerCount => roomMates.Count;

            public ICollection<Guid> PeerIds => roomMates.Keys;
            internal bool ContainsPeer(Guid key) => roomMates.ContainsKey(key);


            public void Add(Guid peerId, PeerInfo info)
            {
                roomMates.TryAdd(peerId, info);
            }
            public void Remove(Guid peerId)
            {
                roomMates.TryRemove(peerId, out _);
            }

            internal bool TryGetPeerInfo(Guid id, out PeerInfo info)
            {
                return roomMates.TryGetValue(id, out info);
            }
        }
    }
}
