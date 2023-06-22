using NetworkLibrary;
using Protobuff.Components.Serialiser;
using Protobuff.P2P.HolePunch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Protobuff.P2P.Room
{
    public class SecureLobbyClient
    {
        public Guid SessionId => client.SessionId;
        public Action<string,Guid> OnPeerJoinedRoom;
        public Action<string,Guid> OnPeerLeftRoom;
        public Action<Guid> OnPeerDisconnected;
        public Action<string,MessageEnvelope> OnTcpRoomMesssageReceived;
        public Action<string,MessageEnvelope> OnUdpRoomMesssageReceived;
        public Action<MessageEnvelope> OnTcpMessageReceived;
        public Action<MessageEnvelope> OnUdpMessageReceived;
        public Action OnDisconnected;
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;

        private RelayClientBase<ProtoSerializer> client;

        private ConcurrentDictionary<string, Room>
            rooms = new ConcurrentDictionary<string, Room>();
        private ConcurrentDictionary<Guid,ConcurrentDictionary<string, string>>
            peerToRoomsMap =  new ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>();

        public SecureLobbyClient(X509Certificate2 clientCert) 
        {
            client = new RelayClientBase<ProtoSerializer>(clientCert);
            client.OnMessageReceived += HandleMessage;
            client.OnUdpMessageReceived+= HandleUdpMessage;
            client.OnDisconnected += HandleDisconnected;
            client.RemoteCertificateValidationCallback += CertificateValidation;
        }

        private bool CertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (RemoteCertificateValidationCallback == null)
                return true;
            return RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
        }

        public Task<bool> ConnectAsync(string ip, int port)
        {
            return client.ConnectAsync(ip, port);
        }

        public void Connect(string ip, int port)
        {
             client.Connect(ip, port);
        }

        public Task<bool> RequestHolePunchAsync(in Guid destinationId, int timeot = 10000)
        {
            return client.RequestHolePunchAsync(destinationId, timeot);
        }

        public void CreateOrJoinRoom(string roomName)
        {
            _ =CreateOrJoinRoomAsync(roomName).Result;
        }

        public Task<bool> CreateOrJoinRoomAsync(string roomName)
        {
            var returnVal =  new TaskCompletionSource<bool>();

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
                        returnVal.TrySetResult(true);
                    }
                    else
                    {
                        rooms.TryRemove(roomName, out _);
                        returnVal.TrySetResult(false);
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

        private void PrepareEnvelope(string roomName, ref MessageEnvelope messageEnvelope)
        {
            if(messageEnvelope.KeyValuePairs == null)
                messageEnvelope.KeyValuePairs= new Dictionary<string, string>();

            messageEnvelope.KeyValuePairs[Constants.RoomName] = roomName;
            messageEnvelope.To = Guid.Empty;
            messageEnvelope.From = client.SessionId;
        }

        public void SendMessageToRoom(string roomName, MessageEnvelope message)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelope(roomName, ref message);
                client.tcpMessageClient.SendAsyncMessage(message);
            }
        }

        public void SendMessageToRoom<T>(string roomName, MessageEnvelope message, T innerMessage)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelope(roomName, ref message);
                client.tcpMessageClient.SendAsyncMessage(message);
            }
        }
        public void SendUdpMessageToRoom(string roomName, MessageEnvelope message)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelope(roomName, ref message);
                if(rooms.TryGetValue(roomName, out var room))
                    client.MulticastUdpMessage(message, room.PeerIds);
            }
        }

        public void SendUdpMessageToRoom<T>(string roomName, MessageEnvelope message, T innerMessage)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelope(roomName, ref message);
                if (rooms.TryGetValue(roomName, out var dict))
                    client.MulticastUdpMessage(message, dict.PeerIds,innerMessage);
            }
        }
        public void SendMessageToPeer(Guid peerId, MessageEnvelope message)
        {
            client.SendAsyncMessage(peerId,message);
        }
        public void SendMessageToPeer<T>(Guid peerId, MessageEnvelope message, T innerMessage)
        {
            client.SendAsyncMessage(peerId,message,innerMessage);
        }
        public void SendUdpMessageToPeer(Guid peerId, MessageEnvelope message)
        {
          
            client.SendUdpMesssage(peerId,message);
        }
        public void SendUdpMessageToPeer<T>(Guid peerId, MessageEnvelope message, T innerMessage)
        {
            client.SendUdpMesssage(peerId,message,innerMessage);
        }
        #endregion

        private void HandleUdpMessage(MessageEnvelope message)
        {
            if (message.KeyValuePairs != null && message.KeyValuePairs.TryGetValue(Constants.RoomName, out string roomName))
            {
                OnUdpRoomMesssageReceived?.Invoke(roomName, message);
            }
            else
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
            else if(message.Header == Constants.PeerDisconnected)
            {
                HandlePeerDisconnected(message);
            }
            else
                HandleTcpReceived(message);
        }

       

        private void HandleTcpReceived(MessageEnvelope message)
        {
            if(message.KeyValuePairs!=null && message.KeyValuePairs.TryGetValue(Constants.RoomName, out string roomName))
            {
                OnTcpRoomMesssageReceived?.Invoke(roomName, message);
            }
            else
            {
                OnTcpMessageReceived?.Invoke(message);
            }
        }

        private void UpdateRooms(MessageEnvelope message)
        {
            Console.WriteLine("UpdateRooms");
            var roomUpdateMessage = KnownTypeSerializer.DeserializeRoomPeerList(message.Payload, message.PayloadOffset);

            Dictionary<Guid,PeerInfo> JoinedList = new Dictionary<Guid,PeerInfo>();
            List<Guid> LeftList = new List<Guid>();

            var remoteList = roomUpdateMessage.Peers.PeerIds;
            if (!rooms.TryGetValue(roomUpdateMessage.RoomName, out var localRoom))
            {
                return;
            }

            foreach (var remotePeer in remoteList)
            {
                if (!localRoom.ContainsPeer(remotePeer.Key))
                {
                    JoinedList.Add(remotePeer.Key,remotePeer.Value);
                }

            }

            foreach (var localPeerId in localRoom.PeerIds)
            {
                if (!remoteList.ContainsKey(localPeerId))
                {
                    LeftList.Add(localPeerId);
                }

            }
           
            foreach (var peerKV in JoinedList)
            {
                
                if (rooms.TryGetValue(roomUpdateMessage.RoomName, out var room))
                {
                    room.Add(peerKV.Key, peerKV.Value);
                }
                if (!peerToRoomsMap.ContainsKey(peerKV.Key))
                {

                }
                client.Peers.TryAdd(peerKV.Key, false);
                OnPeerJoinedRoom?.Invoke(roomUpdateMessage.RoomName, peerKV.Key);
            }

            foreach (var peerId in LeftList)
            {
                if (rooms.TryGetValue(roomUpdateMessage.RoomName, out var room))
                {
                    room.Remove(peerId);
                }
                OnPeerLeftRoom?.Invoke(roomUpdateMessage.RoomName, peerId);
            }
        }
        private void HandlePeerDisconnected(MessageEnvelope message)
        {
            client.Peers.TryAdd(message.From, false);
            OnPeerDisconnected?.Invoke(message.From);

        }
        private void HandleDisconnected()
        {
           rooms.Clear();
           OnDisconnected?.Invoke();
        }
        public bool TryGetRoommateInfo(string roomName, Guid id, out PeerInfo info)
        {
            info = null;
            if (roomName == null) return false;
            if(rooms.TryGetValue(roomName, out Room room))
            {
                if (room.TryGetPeerInfo(id, out info))
                    return true;
            }
            return false;
        }
        public bool TryGetRoommateIds(string roomName,out ICollection<Guid> peerIds)
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
        class Room
        {
            public string roomName;
            private ConcurrentDictionary<Guid, PeerInfo> roomMates = new ConcurrentDictionary<Guid, PeerInfo>();

            public Room(string roomName)
            {
                this.roomName = roomName;
            }

            public int PeerCount=>roomMates.Count;

            public ICollection<Guid> PeerIds => roomMates.Keys;
            internal bool ContainsPeer(Guid key) => roomMates.ContainsKey(key);


            public void Add(in Guid peerId, PeerInfo info)
            {
                roomMates.TryAdd(peerId, info);
            }
            public void Remove(in Guid peerId)
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
