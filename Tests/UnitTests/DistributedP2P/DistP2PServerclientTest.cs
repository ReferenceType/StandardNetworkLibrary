using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Server;
using Protobuff.Components.Serialiser;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests.DistributedP2P
{
    class ClientAuthToken : IClientAuthenticationToken
    {
        public string Token { get; } = "1234";

        public string AuthenticationMethod { get; } = "1234";

        public string AdditionalData { get; } = "1234";
    }
    class ClientAuth : IClientAuthenticationProvider
    {
        public IClientAuthenticationToken Authenticate()
        {
            return new ClientAuthToken();
        }
    }
    class DbInfo : IClientDbInfo
    {
        public bool IsValid { get; } = true;

        public string Error { get; }

        public Guid ClientId { get; } = Guid.NewGuid();
    }
    class ServerDb : IServerDbConnector
    {
        public Task<IClientDbInfo> GetClientData(IAuthenticationResult result)
        {
            IClientDbInfo dbInfo = new DbInfo();
            return Task.FromResult(dbInfo);
        }

        public Task<IClientDbInfo> RegisterClient(IAuthenticationResult tokenResult, byte[] payload)
        {
            IClientDbInfo dbInfo = new DbInfo();
            return Task.FromResult(dbInfo);
        }
    }
    class AuthResult : IAuthenticationResult
    {
        public bool IsValid { get; } = true;

        public string UserId { get; } = "1234";

        public string Error { get; }

        public IReadOnlyDictionary<string, string> Claims { get; } = new Dictionary<string, string>();
    }

    class ServerAuth : IAuthenticator
    {
        public Task<IAuthenticationResult> Authenticate(string AuthenticationToken, string AuthenticationMethod, string Cookies)
        {
            IAuthenticationResult result = new AuthResult();
            return Task.FromResult(result);
        }
    }


    class ClientDB : IClientDbConnection
    {
        public byte[] GetClientPublicData()
        {
            return new byte[80];
        }
    }
    [TestClass]
    public class DistP2PServerclientTest
    {

        private static DistributedLobbyServerBase<ProtoSerializer> ArrangeServer()
        {
            var dep = new Dependencies()
            {
                Authenticator = new ServerAuth(),
                DbConnector = new ServerDb()
            };
            var param = new ServerParameters()
            {
                certificate = null,
                SSlPort = 20010,
                TcpPort = 20011,
                UdpPort = 20012
            };
            var server = new DistributedLobbyServerBase<ProtoSerializer>(dep, param);
            server.StartServer();
            return server;
        }

        [TestMethod]
        public void ConnectTest()
        {
            DistributedLobbyClient<ProtoSerializer> distributedLobbyClient = new DistributedLobbyClient<ProtoSerializer>(new ClientDB(), new ClientAuth());
            using var server = ArrangeServer();
            var res = distributedLobbyClient.ConnectAsync("127.0.0.1", 20010).Result;
        }




        [TestMethod]
        public void PipeTest()
        {
            DistributedLobbyClient<ProtoSerializer> distributedLobbyClient = new DistributedLobbyClient<ProtoSerializer>(new ClientDB(), new ClientAuth());
            DistributedLobbyClient<ProtoSerializer> distributedLobbyClient2 = new DistributedLobbyClient<ProtoSerializer>(new ClientDB(), new ClientAuth());

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();


            using var server = ArrangeServer();

            var res = distributedLobbyClient.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = distributedLobbyClient2.ConnectAsync("127.0.0.1", 20010).Result;

            distributedLobbyClient2.PeerConnected += PeerConnected;

            var info = new ChannelInfo();
            var channel1 = distributedLobbyClient.OpenTcpChannel(distributedLobbyClient2.SessionId, info).Result;

            Assert.IsNotNull(channel1);

            byte[] data = new byte[12800000];

            channel1.Start();
            channel1.SendAsync(data, 0, data.Length);

            int received = 0;

            void PeerConnected(ITcpChannel channel)
            {
                channel.BytesReceived += Channel_BytesReceived;
                channel.Disconnected += Disconnected;
                channel.Start();
            }

            void Disconnected()
            {
                Console.WriteLine("DC");
                tcs.SetResult(false);
            }

            void Channel_BytesReceived(byte[] buff, int offset, int count)
            {
                received = count;
                tcs.SetResult(true);
            }

            var ss = tcs.Task.Result;
            Assert.IsTrue(ss);

            Assert.AreEqual(received, data.Length);

        }



        [TestMethod]
        public void MessageTest()
        {
            DistributedLobbyClient<ProtoSerializer> distributedLobbyClient = new DistributedLobbyClient<ProtoSerializer>(new ClientDB(), new ClientAuth());
            DistributedLobbyClient<ProtoSerializer> distributedLobbyClient2 = new DistributedLobbyClient<ProtoSerializer>(new ClientDB(), new ClientAuth());

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            int received = 0;

            using var server = ArrangeServer();

            var res = distributedLobbyClient.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = distributedLobbyClient2.ConnectAsync("127.0.0.1", 20010).Result;

            distributedLobbyClient2.MessageReceived += MsgRec;
            MessageEnvelope msg = new MessageEnvelope();
            msg.Header = "Greetings";
            msg.Payload = new byte[12800000];
            msg.To = distributedLobbyClient2.SessionId;
            distributedLobbyClient.SendAsyncMessage(msg);

            void MsgRec(NetworkLibrary.MessageEnvelope msg)
            {
                tcs.SetResult(true);
                received = msg.PayloadCount;
            }

            var ss = tcs.Task.Result;
            Assert.IsTrue(ss);

            Assert.AreEqual(received, msg.Payload.Length);


        }


        private static DistributedLobbyClient<ProtoSerializer> GetClient()
        {
            return new DistributedLobbyClient<ProtoSerializer>(new ClientDB(), new ClientAuth());
        }

        [TestMethod]
        public void StatusPublishTest()
        {
            using var server = ArrangeServer();
            List<DistributedLobbyClient<ProtoSerializer>> clients = new List<DistributedLobbyClient<ProtoSerializer>>();
            HashSet<Guid> ids = new HashSet<Guid>();

            List<Task> pending = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                var cl = GetClient();
                Task task = cl.ConnectAsync("127.0.0.1", 20010).ContinueWith(t => 
                {
                    clients.Add(cl);
                    ids.Add(cl.SessionId);
                });
                task.ConfigureAwait(false);

                pending.Add(task);
              
            }

            Task.WhenAll(pending).Wait();
            pending.Clear();
          
            Thread.Sleep(1500);
            VerifyPeerList(clients, ids);

            for (int i = 0; i < 5; i++)//dc 5
            {
                ids.Remove(clients[i].SessionId);
                clients[i].Disconnect();
                clients.RemoveAt(i);
            }

            Thread.Sleep(1500);
            VerifyPeerList(clients, ids);

            for (int i = 0; i < 15; i++) //+ 15
            {
                var cl = GetClient();
                Task task = cl.ConnectAsync("127.0.0.1", 20010).ContinueWith(t =>
                {
                    clients.Add(cl);
                    ids.Add(cl.SessionId);
                });
                task.ConfigureAwait(false);

                pending.Add(task);

            }

            Task.WhenAll(pending).Wait();
            pending.Clear();

            Thread.Sleep(1500);
            VerifyPeerList(clients, ids);


            for (int i = 0; i < 15; i++) //+- 15
            {
                var cl = GetClient();
                var res = cl.ConnectAsync("127.0.0.1", 20010).Result;
                clients.Add(cl);
                ids.Add(cl.SessionId);

                ids.Remove(clients[i].SessionId);
                clients[i].Disconnect();
                clients.RemoveAt(i);

            }

            Thread.Sleep(1500);
            VerifyPeerList(clients, ids);
        }

        private static void VerifyPeerList(List<DistributedLobbyClient<ProtoSerializer>> clients, HashSet<Guid> ids)
        {
            foreach (var client in clients)
            {
                var pl = client.GetPeerList();
                Assert.IsNotNull(pl);
                Assert.IsTrue(pl.Count == clients.Count - 1);

                foreach (var peerKv in pl)
                {
                    Assert.IsTrue(ids.Contains(peerKv.Key));
                    Assert.IsTrue(peerKv.Key != client.SessionId);
                }
            }
        }

        [TestMethod]
        public void SendAndWait()
        {
            using var server = ArrangeServer();
            var cl1 = GetClient();
            var cl2 = GetClient();

            var res = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

           

            cl1.MessageReceived += Cl1_MessageReceived;
            void Cl1_MessageReceived(MessageEnvelope obj)
            {
                obj.To = cl2.SessionId;
               cl1.SendAsyncMessage(obj);
            }

            var msg = new MessageEnvelope();
            msg.Header = "Hello";
            msg.To = cl1.SessionId;
            var response = cl2.SendMessageAndWaitResponse(msg).Result;

            Assert.IsTrue (response.Header != MessageEnvelope.RequestTimeout);
        }

        [TestMethod]
        public void Timesync()
        {
            using var server = ArrangeServer();
            var cl1 = GetClient();
            Thread.Sleep(1337);
            var cl2 = GetClient();

            var res = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

            double time1 =cl1.GetTime();
            double time2 = cl2.GetTime();
            Assert.IsTrue(Math.Abs(time1 - time2) < 1);
        }

    }
}
