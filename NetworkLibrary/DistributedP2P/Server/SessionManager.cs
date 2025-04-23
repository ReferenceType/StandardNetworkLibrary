using NetworkLibrary.DistributedP2P.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server
{



    internal class SessionManager
    {
        internal ConcurrentDictionary<Guid, ServerSession> serverSessions = new ConcurrentDictionary<Guid, ServerSession>();

        internal HashSet<Guid> lastSnapshot = new HashSet<Guid>();
        private TaskCompletionSource<bool> publishTrigger = new TaskCompletionSource<bool>();

        public event Action<List<PeerStatusList>> PeerListPublish;

        IServerConnection serverConnection;
        private bool shutdown;
        private object publishMutex = new object();
        public SessionManager(IServerConnection serverConnection)
        {
            this.serverConnection = serverConnection;
            PublishRoutine();
            KeepAliveRoutine();
        }

        private async void KeepAliveRoutine()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(10000).ConfigureAwait(false);
                    var now = DateTime.Now;
                    foreach (var ses in serverSessions)
                    {
                        if ((now - ses.Value.lastKeepAlive).TotalMilliseconds > 10000)
                        {
                            serverConnection.EndSession(ses.Key);
                            DestroySession(ses.Key);
                        }
                    }
                }
               
            }
            catch { }
        }

        internal bool RouteMessage(Guid from, MessageEnvelope envelope)
        {
            Guid to = envelope.To;
            envelope.To = Guid.Empty;
            envelope.From = from;
            serverConnection.SendAsyncMessage(to, envelope);

            return true;
        }
        internal bool HandleMessage(Guid from, MessageEnvelope envelope)
        {
            switch (envelope.Header)
            {
                case InternalConstants.KeepAlive:
                    if (serverSessions.TryGetValue(from, out var session))
                    {
                        session.KeepAliveMark();
                        var msg = new MessageEnvelope();
                        msg.Header = InternalConstants.KeepAlive;
                        msg.IsInternal = true;
                        serverConnection.SendAsyncMessage(from,msg);
                    }
                    break;
            }
            return true;
        }

        public PeerStatusList CreateSession(IClientDbInfo clientInfo, Guid ephemeralClientId, IPEndPoint clientPublicIp, List<string> clientLocalIps)
        {
            var newSession = new ServerSession(clientInfo, ephemeralClientId, clientPublicIp, clientLocalIps);
            lock (publishMutex)
            {
                if (!serverSessions.ContainsKey(ephemeralClientId))
                {
                    foreach (var existingSessionKV in serverSessions)
                    {
                        if (existingSessionKV.Value.Knows(newSession.PeerId))
                        {
                            existingSessionKV.Value.AddNewPeer(ephemeralClientId, newSession.GetPeerStatus());
                            newSession.AddNewPeer(existingSessionKV.Value.EphemeralId, existingSessionKV.Value.GetPeerStatus());
                        }
                    }
                    serverSessions.TryAdd(ephemeralClientId, newSession);

                }
                // for instant sync of new peer
                var pubInfo = newSession.GetPublishInfo();
                //newSession.ResetPublishInfo();

                publishTrigger.TrySetResult(true);
                return pubInfo;
            }

        }

        internal void DestroySession(Guid ephemeralClientId)
        {
            lock (publishMutex)
            {
                if (serverSessions.TryRemove(ephemeralClientId, out ServerSession session))
                {
                    foreach (var existingSessionKV in serverSessions)
                    {
                        existingSessionKV.Value.RemovePeer(ephemeralClientId);
                    }
                    publishTrigger.TrySetResult(true);
                }
            }
        }

        internal bool IsSessionActive(Guid ephemeralClientId)
        {
            return serverSessions.ContainsKey(ephemeralClientId);
        }


        internal bool GetSessionData(Guid guid, out ServerSession sesData)
        {
            return serverSessions.TryGetValue(guid, out sesData);

        }

        internal async void PublishRoutine()
        {
            List<PeerStatusList> PubList = new List<PeerStatusList>();
            while (!shutdown)
            {
                await publishTrigger.Task;
                Interlocked.Exchange(ref publishTrigger, new TaskCompletionSource<bool>());

                PubList.Clear();

                lock (publishMutex)
                {
                    foreach (var sessionKV in serverSessions)
                    {
                        PeerStatusList toPublish = sessionKV.Value.GetPublishInfo();
                        if (toPublish != null)
                        {
                            sessionKV.Value.ResetPublishInfo();
                            PubList.Add(toPublish);
                        }
                    }
                }


                PeerListPublish?.Invoke(PubList);
                await Task.Delay(1000);
            }
        }
    }
}
