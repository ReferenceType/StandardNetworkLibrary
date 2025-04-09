using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary;
using NetworkLibrary.DistributedP2P.Channels;
using NetworkLibrary.DistributedP2P.Channels.Components;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Server;
using Protobuff.Components.Serialiser;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
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
                UdpPort = 20012,
                DiscoveryServerPort = 20013,
                
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
            ManualResetEvent mre = new ManualResetEvent(false);


            using var server = ArrangeServer();

            var res = distributedLobbyClient.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = distributedLobbyClient2.ConnectAsync("127.0.0.1", 20010).Result;

            distributedLobbyClient2.PeerConnected += PeerConnected;

            var info = new ChannelInfo();
            info.ChannelName = "Test";
            info.ChannelType = ChannelType.Tcp;
            var channel1 = (TcpChannel)distributedLobbyClient.OpenRelayChannel(distributedLobbyClient2.SessionId, info).Result;

            Assert.IsNotNull(channel1);

            byte[] data = new byte[12800000];

            channel1.Start();
            channel1.SendAsync(data, 0, data.Length);
            Thread.Sleep(100);
            mre.Set();

            int received = 0;

            void PeerConnected(IChannel channel_)
            {
                mre.WaitOne();//emulate bad syncronisation
                var channel = (TcpChannel)channel_;
                channel.BytesReceived += Channel_BytesReceived;
                channel.Disconnected += Disconnected;
                channel.Start();
            }

            void Disconnected()
            {
                Console.WriteLine("DC");
                tcs.TrySetResult(false);
            }

            void Channel_BytesReceived(byte[] buff, int offset, int count)
            {
                received = count;
                tcs.TrySetResult(true);
            }

            var ss = tcs.Task.Result;
            Assert.IsTrue(ss);

            Assert.AreEqual(received, data.Length);

        }

        [TestMethod]
        public void SecurePipeTest()
        {
            DistributedLobbyClient<ProtoSerializer> distributedLobbyClient = new DistributedLobbyClient<ProtoSerializer>(new ClientDB(), new ClientAuth());
            DistributedLobbyClient<ProtoSerializer> distributedLobbyClient2 = new DistributedLobbyClient<ProtoSerializer>(new ClientDB(), new ClientAuth());

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ManualResetEvent mre = new ManualResetEvent(false);
            List<int> received = new List<int>();
            int iter = 20;

            using var server = ArrangeServer();

            var res = distributedLobbyClient.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = distributedLobbyClient2.ConnectAsync("127.0.0.1", 20010).Result;

            distributedLobbyClient2.PeerConnected += PeerConnected;

            var info = new ChannelInfo();
            info.ChannelName = "Test";
            info.ChannelType = ChannelType.SecureTcp;
            var channel1 = (SecureTcpChannel)distributedLobbyClient.OpenRelayChannel(distributedLobbyClient2.SessionId, info).Result;

            Assert.IsNotNull(channel1);

            byte[] data = new byte[12800000];

            channel1.Start();

           
            var ping = channel1.Ping().Result;

            for (int i = 0; i < iter; i++)
            {
                data[0] = (byte)i;
                channel1.SendAsync(data, 0, data.Length);
                if(i%2 ==0)
                    Thread.Sleep(1000);
            }


            void PeerConnected(IChannel channel_)
            {
               // mre.WaitOne();//emulate bad syncronisation
                var channel = (SecureTcpChannel)channel_;
                channel.BytesReceived += Channel_BytesReceived;
                channel.Disconnected += Disconnected;
                channel.Start();
            }

            void Disconnected()
            {
                Console.WriteLine("DC");
                tcs.TrySetResult(false);
            }

            void Channel_BytesReceived(byte[] buff, int offset, int count)
            {
                received.Add(buff[offset]);

                if(received.Count == iter)
                    tcs.TrySetResult(true);
            }

            var ss = tcs.Task.Result;
            Assert.IsTrue(ss);

            for (int i = 0; i < received.Count; i++)
            {
                Assert.IsTrue(received[i] == i);
            }

        }

        [TestMethod]
        public void PipeTestUdp()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            int received = 0;

            using var server = ArrangeServer();
            var cl1 = GetClient();
            var cl2 = GetClient();

            var res = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

            cl2.PeerConnected += Cl2_PeerConnected;

            var info = new ChannelInfo();
            info.ChannelName = "Test";
            info.ChannelType = ChannelType.Udp;
            var channel1 = (UdpChannel)cl1.OpenRelayChannel(cl2.SessionId, info).Result;
            Assert.IsNotNull(channel1);
            channel1.Start();

            byte[] data = new byte[12800000];
            channel1.Send(data,0,data.Length);
            Thread.Sleep(100);

            void Cl2_PeerConnected(IChannel obj)
            {
                var udpChannel = (UdpChannel)obj;
                udpChannel.OnMessageReceived += (b,o,c) => 
                { 
                    received = c; tcs.SetResult(true); };
                udpChannel.Start();
            }

            var ss = tcs.Task.Result;
            Assert.AreEqual(received, data.Length);

        }

        [TestMethod]
        public void PipeTestUdpSecure()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            int received = 0;
            int cnt = 0;

            using var server = ArrangeServer();
            var cl1 = GetClient();
            var cl2 = GetClient();

            var res = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

            cl2.PeerConnected += Cl2_PeerConnected;

            var info = new ChannelInfo();
            info.ChannelName = "Test";
            info.ChannelType = ChannelType.SecureUdp;
            var channel1 = (SecureUdpChannel)cl1.OpenRelayChannel(cl2.SessionId, info).Result;
            Assert.IsNotNull(channel1);
            channel1.Start();


            Thread.Sleep(5000);
            var ping = channel1.Ping().Result;
            Console.WriteLine(ping);

            byte[] data = new byte[12800000];
            channel1.SendReliable(data, 0, data.Length);
            Thread.Sleep(5000);
            channel1.SendReliable(data, 0, data.Length);
            Thread.Sleep(100);

            void Cl2_PeerConnected(IChannel obj)
            {
                var udpChannel = (SecureUdpChannel)obj;
                udpChannel.OnMessageReceived += (b, o, c) =>
                {
                    received = c; 
                    if(++cnt == 2)
                        tcs.SetResult(true);
                };
                udpChannel.Start();
            }

            var ss = tcs.Task.Result;
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

            double time1 = cl1.GetTime();
            double time2 = cl2.GetTime();
            double time3 = server.GetTime();
            Assert.IsTrue(Math.Abs(time1 - time2) < 1);
            Assert.IsTrue(Math.Abs(time1 - time3) < 1);
        }

        [TestMethod]
        public void UdpHolepunch()
        {

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ManualResetEvent mre = new ManualResetEvent(false);
            int received = 0;

            var cl1 = GetClient();
            var cl2 = GetClient();
            using var server = ArrangeServer();

            var res1 = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

            cl2.PeerConnected += Cl2_PeerConnected;

            var info = new ChannelInfo();
            info.ChannelType = ChannelType.Udp;
            info.ChannelName = "Test";
            var channel1 = (UdpChannel)cl1.TryHolePunch(cl2.SessionId, info).Result;
            Assert.IsNotNull(channel1);
            channel1.Start();

            var data = new byte[12800000];
            data[0] = 1;
            channel1.SendReliable(data, 0, data.Length);
            Thread.Sleep(100);

            void Cl2_PeerConnected(IChannel obj)
            {
                var ch = (UdpChannel)obj;
                ch.OnMessageReceived += Ch_OnMessageReceived;
                ch.Start();
            }

            void Ch_OnMessageReceived(byte[] arg1, int arg2, int arg3)
            {
                received = arg3;
                tcs.TrySetResult(true);
            }

            var ss = tcs.Task.Result;
            Assert.AreEqual(data.Length, received);



        }

        [TestMethod]
        public void UdpHolepunchSecure()
        {

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ManualResetEvent mre = new ManualResetEvent(false);
            int received = 0;

            var cl1 = GetClient();
            var cl2 = GetClient();
            using var server = ArrangeServer();

            var res1 = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

            cl2.PeerConnected += Cl2_PeerConnected;

            var info = new ChannelInfo();
            info.ChannelType = ChannelType.SecureUdp;
            info.ChannelName = "Test";
            var channel1 = (SecureUdpChannel)cl1.TryHolePunch(cl2.SessionId, info).Result;
            Assert.IsNotNull(channel1);
            channel1.Start();

            var data = new byte[1280000];
            data[0] = 1;
            channel1.SendReliable(data,0,data.Length);
            Thread.Sleep(100);

            void Cl2_PeerConnected(IChannel obj)
            {
                var ch = (SecureUdpChannel)obj;
                ch.OnMessageReceived += Ch_OnMessageReceived;
                ch.Start();
            }

            void Ch_OnMessageReceived(byte[] arg1, int arg2, int arg3)
            {
                received = arg3;
                tcs.TrySetResult(true);
            }

            var ss = tcs.Task.Result;
            Assert.AreEqual(data.Length, received);


            
        }
        [TestMethod]
        public void TcpHolepunch()
        {

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ManualResetEvent mre = new ManualResetEvent(false);
            int received = 0;

            var cl1 = GetClient();
            var cl2 = GetClient();
            using var server = ArrangeServer();

            var res1 = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

            cl2.PeerConnected += Cl2_PeerConnected;

            var info = new ChannelInfo();
            info.ChannelType = ChannelType.Tcp;
            info.ChannelName = "Test";
            var channel1 = (TcpChannel)cl1.TryHolePunch(cl2.SessionId, info, TcpHolePunchStrategy.Sequential).Result;
            Assert.IsNotNull(channel1);
            channel1.Start();

            var data = new byte[1280];
            data[0] = 1;
            channel1.SendAsync(data, 0, data.Length);
            Thread.Sleep(100);

            void Cl2_PeerConnected(IChannel obj)
            {
                var ch = (TcpChannel)obj;
                ch.BytesReceived += Ch_OnMessageReceived;
                ch.Start();
            }

            void Ch_OnMessageReceived(byte[] arg1, int arg2, int arg3)
            {
                received = arg3;
                tcs.TrySetResult(true);
            }

            var ss = tcs.Task.Result;
            Assert.AreEqual(data.Length, received);



        }

        [TestMethod]
        public void TcpHolepunchSecure()
        {

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ManualResetEvent mre = new ManualResetEvent(false);
            int received = 0;

            var cl1 = GetClient();
            var cl2 = GetClient();
            using var server = ArrangeServer();

            var res1 = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

            cl2.PeerConnected += Cl2_PeerConnected;

            var info = new ChannelInfo();
            info.ChannelType = ChannelType.SecureTcp;
            info.ChannelName = "Test";
            var channel1 = (SecureTcpChannel)cl1.TryHolePunch(cl2.SessionId, info).Result;
            Assert.IsNotNull(channel1);
            channel1.Start();

            var data = new byte[12800000];
            data[0] = 1;
            channel1.SendAsync(data, 0, data.Length);
            Thread.Sleep(100);

            void Cl2_PeerConnected(IChannel obj)
            {
                var ch = (SecureTcpChannel)obj;
                ch.BytesReceived += Ch_OnMessageReceived;
                ch.Start();
            }

            void Ch_OnMessageReceived(byte[] arg1, int arg2, int arg3)
            {
                received = arg3;
                tcs.TrySetResult(true);
            }

            var ss = tcs.Task.Result;
            Assert.AreEqual(data.Length, received);



        }

        [TestMethod]
        public void TestTcpChannelParallelSend()
        {

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ManualResetEvent mre = new ManualResetEvent(false);
            int numReceived = 0;

            var cl1 = GetClient();
            var cl2 = GetClient();
            using var server = ArrangeServer();

            var res1 = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

            var data = new byte[1280000];
            data[0] = 1;
            int iter = 100;


            cl2.PeerConnected += Cl2_PeerConnected;

            var info = new ChannelInfo();
            info.ChannelType = ChannelType.Tcp;
            info.ChannelName = "Test";
            var channel1 = (TcpChannel)cl1.TryHolePunch(cl2.SessionId, info, TcpHolePunchStrategy.Sequential).Result;
            Assert.IsNotNull(channel1);
            channel1.Start();

           
            Parallel.For(0, iter, (i) =>
            {
                channel1.SendAsync(data, 0, data.Length);
            });
            Thread.Sleep(100);

            void Cl2_PeerConnected(IChannel obj)
            {
                var ch = (TcpChannel)obj;
                ch.BytesReceived += Ch_OnMessageReceived;
                ch.Start();
            }

            void Ch_OnMessageReceived(byte[] arg1, int arg2, int arg3)
            {
                if (arg3 != data.Length)
                    throw new Exception();

                if(Interlocked.Increment(ref numReceived) == 100)
                    tcs.TrySetResult(true);
            }

            var ss = tcs.Task.Result;
            Assert.AreEqual(iter, numReceived);



        }

        [TestMethod]
        public void TestTcpChannelOrder()
        {

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ManualResetEvent mre = new ManualResetEvent(false);
            int numReceived = 0;

            var cl1 = GetClient();
            var cl2 = GetClient();
            using var server = ArrangeServer();

            var res1 = cl1.ConnectAsync("127.0.0.1", 20010).Result;
            var res2 = cl2.ConnectAsync("127.0.0.1", 20010).Result;

            var data = new byte[1280];
            data[0] = 1;
            int iter = 100;
            List<int> values = new List<int>();


            cl2.PeerConnected += Cl2_PeerConnected;

            var info = new ChannelInfo();
            info.ChannelType = ChannelType.Tcp;
            info.ChannelName = "Test";
            var channel1 = (TcpChannel)cl1.TryHolePunch(cl2.SessionId, info, TcpHolePunchStrategy.Sequential).Result;
            Assert.IsNotNull(channel1);
            channel1.Start();


            for (int i = 0; i < iter; i++)
            {
                data[0] = (byte)i;
                channel1.SendAsync(data, 0, data.Length);
                if(i%10 == 0)
                    Thread.Sleep(1);
            };
            Thread.Sleep(100);

            void Cl2_PeerConnected(IChannel obj)
            {
                var ch = (TcpChannel)obj;
                ch.BytesReceived += Ch_OnMessageReceived;
                ch.Start();
            }
            void Ch_OnMessageReceived(byte[] arg1, int arg2, int arg3)
            {
                if (arg3 != data.Length)
                    throw new Exception();

                values.Add(arg1[arg2]);

                if (Interlocked.Increment(ref numReceived) == 100)
                    tcs.TrySetResult(true);
            }

            var ss = tcs.Task.Result;
            Assert.AreEqual(iter, numReceived);

            for (int i = 1; i < values.Count; i++)
            {
                Assert.IsTrue(values[i - 1] + 1 == values[i]);
            }

        }


    }
}
