using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary;
using NetworkLibrary.P2P;
using ProtoBuf;
using Protobuff.P2P;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests
{
    [ProtoContract]
    class Payload
    {
        [ProtoMember(1)]
        public byte[] data;
    }
    [TestClass]
    public class LobbyServerTest
    {

        [TestMethod]
        public void TestRoomBroadcast()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureProtoRoomClient, string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureProtoRoomClient, string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 22222;
            int numClients = 10;
            var server = new RoomServer(port, scert);
            server.StartServer();

            List<SecureProtoRoomClient> clients = new List<SecureProtoRoomClient>();

            for (int i = 0; i < numClients; i++)
            {
                var cl = new SecureProtoRoomClient(cert);
                cl.OnTcpMessageReceived += ( m) => TcpReceived(cl, m);
                cl.OnUdpMessageReceived += ( m) => UdpReceived(cl, m);
                cl.Connect(ip, port);
                cl.CreateOrJoinRoom("WA");
                clients.Add(cl);
            }

            Thread.Sleep(2000);
            foreach (var client in clients)
            {
                foreach (var client2 in clients)
                {
                    if (client.SessionId.CompareTo(client2.SessionId) > 0)
                    {
                        var r = client.RequestHolePunchAsync(client2.SessionId).Result;

                    }
                }
                break;
            }
            clients[0].BroadcastMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].BroadcastUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].GetAvailableRooms().ContinueWith((m) => Console.WriteLine(m.Result[0]));


            void UdpReceived(SecureProtoRoomClient cl, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl, null);
                Interlocked.Increment(ref totalUdp);
            }

            void TcpReceived(SecureProtoRoomClient cl, MessageEnvelope m)
            {
                expectedClientsTcp.TryAdd(cl, null);
                Interlocked.Increment(ref totalTcp);

            }
            Thread.Sleep(2000);
            Assert.IsTrue(expectedClientsTcp.Count == clients.Count - 1);
            Assert.AreEqual(clients.Count - 1, expectedClientsUdp.Count);
            Assert.AreEqual(numClients - 1, totalTcp);
            Assert.AreEqual(numClients - 1, totalUdp);
            server.ShutdownServer();
        }

        [TestMethod]
        public void TestBatchRoomBroadcast()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureProtoRoomClient, string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureProtoRoomClient, string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 22222;
            int numClients = 10;
            int numMsgs = 1000;
            var server = new RoomServer(port, scert);
            server.StartServer();

            List<SecureProtoRoomClient> clients = new List<SecureProtoRoomClient>();

            for (int i = 0; i < numClients; i++)
            {
                var cl = new SecureProtoRoomClient(cert);
                cl.OnTcpMessageReceived += (m) => TcpReceived(cl, m);
                cl.OnUdpMessageReceived += (m) => UdpReceived(cl, m);
                cl.Connect(ip, port);
                cl.CreateOrJoinRoom("WA");
                clients.Add(cl);
            }

            Thread.Sleep(1000);
            for (int j = 0; j < numMsgs; j++)
            {
                for (int i = 0; i < numClients; i++)
                {
                    clients[i].BroadcastMessageToRoomBatched("WA", new MessageEnvelope() { Header = "Tcp Yo" });
                }
            }
           
         
            


            void UdpReceived(SecureProtoRoomClient cl, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl, null);
                Interlocked.Increment(ref totalUdp);
            }

            void TcpReceived(SecureProtoRoomClient cl, MessageEnvelope m)
            {
                expectedClientsTcp.TryAdd(cl, null);
                Interlocked.Increment(ref totalTcp);

            }
            Thread.Sleep(2000);
            Assert.AreEqual(clients.Count, expectedClientsTcp.Count);
            Assert.AreEqual(totalTcp, numClients*numMsgs*(clients.Count - 1));
          
            server.ShutdownServer();
        }

        [TestMethod]
        public void TestTimeSync()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureProtoRoomClient, string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureProtoRoomClient, string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 22222;
            int numClients = 10;
            var server = new RoomServer(port, scert);
            server.StartServer();
            Thread.Sleep(1000);

            List<SecureProtoRoomClient> clients = new List<SecureProtoRoomClient>();

            for (int i = 0; i < numClients; i++)
            {
                var cl = new SecureProtoRoomClient(cert);
               
                cl.Connect(ip, port);
                cl.CreateOrJoinRoom("WA");
                clients.Add(cl);
                Thread.Sleep(1);
            }

            Thread.Sleep(1000);

            for (int j = 0; j < 10; j++)
            {
                for (int i = 0; i < numClients; i++)
                {
                    var suc = clients[i].SyncTime().Result;
                }
                Thread.Sleep(100);
            }
            
          


            Thread.Sleep(1000);

            var t1 = clients[0].GetTime();
            var tn = clients[numClients-1].GetTime();

            Trace.WriteLine(t1);
            Trace.WriteLine(tn);
            Assert.IsTrue(Math.Abs(t1-tn)<1);
           
          
           

            server.ShutdownServer();
        }
        [TestMethod]
        public void TestTimeSync2()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureProtoRoomClient, string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureProtoRoomClient, string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 22222;
            int numClients = 10;
            var server = new RoomServer(port, scert);
            server.StartServer();
            Thread.Sleep(1000);

            List<SecureProtoRoomClient> clients = new List<SecureProtoRoomClient>();

            for (int i = 0; i < numClients; i++)
            {
                var cl = new SecureProtoRoomClient(cert);
                cl.StartAutoTimeSync(1000, false);
                cl.Connect(ip, port);
              
                cl.CreateOrJoinRoom("MA");
                clients.Add(cl);
                Thread.Sleep(1);
            }

            Thread.Sleep(2400);


            var t1 = clients[0].GetTime();
            var tn = clients[numClients - 1].GetTime();

            Trace.WriteLine(t1);
            Trace.WriteLine(tn);
            Assert.IsTrue(Math.Abs(t1 - tn) < 1);

            server.ShutdownServer();
        }

        [TestMethod]
        public void TestRoomList()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureProtoRoomClient, string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureProtoRoomClient, string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 22222;
            int numClients = 10;
            var server = new RoomServer(port, scert);
            server.StartServer();
            Thread.Sleep(1000);

            ConcurrentDictionary<Guid, SecureProtoRoomClient> clients = new ConcurrentDictionary<Guid, SecureProtoRoomClient>();

            Parallel.For(0, numClients, i =>
            {
                var cl = new SecureProtoRoomClient(cert);

                cl.Connect(ip, port);
                clients.TryAdd(cl.SessionId, cl);

                var list = cl.CreateOrJoinRoom("WA");

                Assert.IsTrue(list != null);
                Assert.IsTrue(list.Peers != null);
                Assert.IsTrue(list.Peers.PeerIds != null);
                Assert.IsTrue(list.Peers.PeerIds.Count > 0);

                foreach (var kv in list.Peers.PeerIds)
                {
                    Assert.IsTrue(clients.ContainsKey(kv.Key));
                }


            });

            Thread.Sleep(1000);

          

            server.ShutdownServer();
        }

        [TestMethod]
        public void TestRoomBroadcastWithLeave()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureProtoRoomClient, string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureProtoRoomClient, string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 22223;
            int numClients = 10;
            var server = new RoomServer(port, scert);
            server.StartServer();

            List<SecureProtoRoomClient> clients = new List<SecureProtoRoomClient>();

            for (int i = 0; i < numClients; i++)
            {
                var cl = new SecureProtoRoomClient(cert);
                cl.OnTcpMessageReceived += (m) => TcpReceived(cl, m);
                cl.OnUdpMessageReceived += (m) => UdpReceived(cl, m);
                cl.Connect(ip, port);
                cl.CreateOrJoinRoom("WA");
                clients.Add(cl);
            }

            Thread.Sleep(2000);
            foreach (var client in clients)
            {
                foreach (var client2 in clients)
                {
                    if (client.SessionId.CompareTo(client2.SessionId) > 0)
                    {
                        var r = client.RequestHolePunchAsync(client2.SessionId).Result;
                     
                    }
                }
                break;
            }
            clients.Last().LeaveRoom("WA");
            Thread.Sleep(1100);

            clients[0].BroadcastMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].BroadcastUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].BroadcastRudpMessageToRoom("WA", new MessageEnvelope() { Header = "RUdp Yo" });

            clients[0].BroadcastMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo", Payload = new byte[128000] });
            clients[0].BroadcastUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo", Payload = new byte[128000] });
            clients[0].BroadcastRudpMessageToRoom("WA", new MessageEnvelope() { Header = "RUdp Yo", Payload = new byte[128000] });

            clients[0].GetAvailableRooms().ContinueWith((m) => Console.WriteLine(m.Result[0]));


            void UdpReceived(SecureProtoRoomClient cl, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl, null);
                Interlocked.Increment(ref totalUdp);
            }

            void TcpReceived(SecureProtoRoomClient cl, MessageEnvelope m)
            {
                expectedClientsTcp.TryAdd(cl, null);
                Interlocked.Increment(ref totalTcp);

            }
            Thread.Sleep(2000);
            Assert.IsTrue(expectedClientsTcp.Count == clients.Count - 2);
            int totalExpectedTcpMSgCount = (numClients - 2) * 2;
            int totalExpectedUdpMSgCount = (numClients - 2) * 4;

            Assert.AreEqual(totalExpectedUdpMSgCount, totalUdp);
            Assert.AreEqual(totalExpectedTcpMSgCount, totalTcp);
            server.ShutdownServer();
        }

        [TestMethod]
        public void TestRoomBroadcastMultiRoom()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureProtoRoomClient, string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureProtoRoomClient, string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 22224;
            int numClients = 10;
            var server = new RoomServer(port, scert);
            server.StartServer();

            List<SecureProtoRoomClient> clients = new List<SecureProtoRoomClient>();

            for (int i = 0; i < numClients; i++)
            {
                var cl = new SecureProtoRoomClient(cert);
                cl.OnTcpMessageReceived += (m) => TcpReceived(cl, m);
                cl.OnUdpMessageReceived += (m) => UdpReceived(cl, m);
                cl.Connect(ip, port);
                cl.CreateOrJoinRoom("WA");
                if (i <5)
                    cl.CreateOrJoinRoom("SA");
                clients.Add(cl);
            }

            Thread.Sleep(2000);
            for (int i = 1; i < clients.Count; i++)
            {
                _ = clients[0].RequestHolePunchAsync(clients[i].SessionId).Result;
                _ = clients[0].RequestTcpHolePunchAsync(clients[i].SessionId).Result;
            }
            //foreach (var client in clients)
            //{
            //    foreach (var client2 in clients)
            //    {
            //        if (client.SessionId.CompareTo(client2.SessionId) > 0)
            //        {
            //           var r =  client.RequestHolePunchAsync(client2.SessionId).Result;
            //           var r1 =  client.RequestTcpHolePunchAsync(client2.SessionId).Result;
            //            if (r != true)
            //            {

            //            }
            //        }
            //    }
            //    break;
            //}
            int largeMsgLen = 128000;
            int defaulMmsgLen = 12800;
            clients[0].BroadcastMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].BroadcastMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo" }, new Payload() { data =  new byte[largeMsgLen] });

            clients[0].BroadcastUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo",Payload =  new byte[largeMsgLen] });

            clients[0].BroadcastRudpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastRudpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastRudpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[largeMsgLen] });
            clients[0].BroadcastRudpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo", Payload = new byte[largeMsgLen] });

            clients[0].BroadcastMessageToRoom("SA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].BroadcastMessageToRoom("SA", new MessageEnvelope() { Header = "Tcp Yo" }, new Payload() { data = new byte[largeMsgLen] });

            clients[0].BroadcastUdpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastUdpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastUdpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastUdpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo", Payload = new byte[largeMsgLen] });

            clients[0].BroadcastRudpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastRudpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[defaulMmsgLen] });
            clients[0].BroadcastRudpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[largeMsgLen] });
            clients[0].BroadcastRudpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo", Payload = new byte[largeMsgLen] });

            clients[0].GetAvailableRooms().ContinueWith((m) => Console.WriteLine(m.Result[0]));


            void UdpReceived(SecureProtoRoomClient cl, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl, null);
                Interlocked.Increment(ref totalUdp);
                //if (m.PayloadCount < 128000)
                //{

                //}
                
            }

            void TcpReceived(SecureProtoRoomClient cl, MessageEnvelope m)
            {
                expectedClientsTcp.TryAdd(cl, null);
                Interlocked.Increment(ref totalTcp);

            }
            Thread.Sleep(2000);
            Assert.IsTrue(expectedClientsTcp.Count == clients.Count - 1);
            Assert.AreEqual(clients.Count - 1, expectedClientsUdp.Count);

            //Assert.AreEqual(13*2, totalTcp);
            //Assert.AreEqual(13*8, totalUdp);
            int expectedTcp = (2 * 9) + (2 * 4);
            int expectedUdp = (8 * 9) + (8 * 4);
            //int expectedTcp = (16 * 9);
            //int expectedUdp = (4 * 9);
            Assert.AreEqual(expectedTcp, totalTcp);
            Assert.AreEqual(expectedUdp, totalUdp);
            server.ShutdownServer();
        }

    }
}
