using NetworkLibrary.Utils;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Protobuff.Components
{
    class PingData
    {
        private object locker =  new object();
        internal enum State
        {
            NotReady,
            PingDispatched,
            PongReceived
        }

        private State PingState = State.NotReady;
        private DateTime dispatchTimeStamp;
        private double latency;
        public void Update(DateTime timeStamp)
        {
            lock (locker)
            {
                PingState = PingData.State.PongReceived;
                latency = (DateTime.Now - timeStamp).TotalMilliseconds;
            }
       
        }

        internal void PingDispatched(DateTime timeStamp)
        {
            lock (locker)
            {
                if (PingState != State.PingDispatched)
                {
                    dispatchTimeStamp = timeStamp;
                    PingState = State.PingDispatched;
                }
            }
           
          
        }

        public double GetLatency()
        {
            lock (locker)
            {
                switch (PingState)
                {
                    case State.NotReady:
                        return 0;

                    case State.PingDispatched:
                        return Math.Max((DateTime.Now - dispatchTimeStamp).TotalMilliseconds, latency);

                    case State.PongReceived:
                        return latency;

                    default: return 0;

                }

            }
        }
    }
    internal class PingHandler
    {
        public const string Ping = "Ping";
        public const string Pong = "Pong";
        private readonly ConcurrentDictionary<Guid,PingData> tcpPingDatas =  new ConcurrentDictionary<Guid, PingData>();
        private readonly ConcurrentDictionary<Guid,PingData> udpPingDatas =  new ConcurrentDictionary<Guid, PingData>();
        
        internal void HandleTcpPongMessage(MessageEnvelope message)
        {
            if(tcpPingDatas.TryGetValue(message.From, out var data))
            {
                data.Update(message.TimeStamp);
            }
        }

        internal void HandleUdpPongMessage(MessageEnvelope message)
        {
            if (udpPingDatas.TryGetValue(message.From, out var data))
            {
                data.Update(message.TimeStamp);
            }
        }

        internal void PeerRegistered(Guid peerId)
        {
            tcpPingDatas.TryAdd(peerId, new PingData());
            udpPingDatas.TryAdd(peerId, new PingData());
        }

        internal void PeerUnregistered(Guid peerId) 
        {
            tcpPingDatas.TryRemove(peerId, out _);
            udpPingDatas.TryRemove(peerId, out _);

        }

        internal void NotifyTcpPingSent(Guid to, DateTime timeStamp)
        {
            if (tcpPingDatas.TryGetValue(to, out var data))
            {
                data.PingDispatched(timeStamp);
            }
        }
        internal void NotifyUdpPingSent(Guid to, DateTime timeStamp)
        {
            if (udpPingDatas.TryGetValue(to, out var data))
            {
                data.PingDispatched(timeStamp);
            }
            
        }

       

        internal Dictionary<Guid, double> GetTcpLatencies() 
        {
            var ret= new Dictionary<Guid, double>();    
            foreach (var item in tcpPingDatas)
            {
                ret[item.Key] = item.Value.GetLatency();
            }
            return ret;
        }

        internal Dictionary<Guid, double> GetUdpLatencies()
        {
            var ret = new Dictionary<Guid, double>();
            foreach (var item in udpPingDatas)
            {
                ret[item.Key] = item.Value.GetLatency();
            }
            return ret;
        }

    }
}
