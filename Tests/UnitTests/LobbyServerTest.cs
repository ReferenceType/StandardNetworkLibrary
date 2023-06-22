using Microsoft.VisualStudio.TestTools.UnitTesting;
using Protobuff.P2P.Room;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkLibrary;
using System.Collections.Concurrent;

namespace UnitTests
{
    [TestClass]
    public class LobbyServerTest
    {
         
        [TestMethod]
        public void TestRoomBroadcast()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureLobbyClient,string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureLobbyClient,string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 2222;
            int numClients = 10;
            SecureLobbyServer server = new SecureLobbyServer(port, scert);
            server.StartServer();

            List<SecureLobbyClient> clients = new List<SecureLobbyClient>();

            for (int i = 0; i < numClients; i++)
            {
                var cl = new SecureLobbyClient(cert);
                cl.OnTcpRoomMesssageReceived += (r, m) => TcpReceived(cl, r, m);
                cl.OnUdpRoomMesssageReceived += (r, m) => UdpReceived(cl,r,m);
                cl.Connect(ip, port);
                cl.CreateOrJoinRoom("WA");
                clients.Add(cl);
            }

            Thread.Sleep(2000);
            foreach (var client in clients)
            {
                foreach (var client2 in clients)
                {
                    if (client.SessionId.CompareTo( client2.SessionId)>0)
                    {
                        var r=client.RequestHolePunchAsync(client2.SessionId).Result;

                    }
                }
                break;
            }
            clients[0].SendMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].SendUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].GetAvailableRooms().ContinueWith((m) => Console.WriteLine(m.Result[0]));


            void UdpReceived(SecureLobbyClient cl, string r, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl,null);
                Interlocked.Increment(ref totalUdp);
            }

            void TcpReceived(SecureLobbyClient cl, string r, MessageEnvelope m)
            {
                expectedClientsTcp.TryAdd(cl, null);
                Interlocked.Increment(ref totalTcp);

            }
            Thread.Sleep(2000);
            Assert.IsTrue(expectedClientsTcp.Count == clients.Count - 1);
            Assert.AreEqual(clients.Count - 1,expectedClientsUdp.Count);
            Assert.AreEqual(numClients - 1, totalTcp);
            Assert.AreEqual(numClients - 1, totalUdp);
        }
        [TestMethod]
        public void TestRoomBroadcastWithLeave()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureLobbyClient, string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureLobbyClient, string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 2222;
            int numClients = 10;
            SecureLobbyServer server = new SecureLobbyServer(port, scert);
            server.StartServer();

            List<SecureLobbyClient> clients = new List<SecureLobbyClient>();

            for (int i = 0; i < numClients; i++)
            {
                var cl = new SecureLobbyClient(cert);
                cl.OnTcpRoomMesssageReceived += (r, m) => TcpReceived(cl, r, m);
                cl.OnUdpRoomMesssageReceived += (r, m) => UdpReceived(cl, r, m);
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

            clients[0].SendMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].SendUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].GetAvailableRooms().ContinueWith((m) => Console.WriteLine(m.Result[0]));


            void UdpReceived(SecureLobbyClient cl, string r, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl, null);
                Interlocked.Increment(ref totalUdp);
            }

            void TcpReceived(SecureLobbyClient cl, string r, MessageEnvelope m)
            {
                expectedClientsTcp.TryAdd(cl, null);
                Interlocked.Increment(ref totalTcp);

            }
            Thread.Sleep(2000);
            Assert.IsTrue(expectedClientsTcp.Count == clients.Count - 2);
            Assert.AreEqual(clients.Count - 2, expectedClientsUdp.Count);
            Assert.AreEqual(numClients - 2, totalTcp);
            Assert.AreEqual(numClients - 2, totalUdp);
        }

        [TestMethod]
        public void TestRoomBroadcastMultiRoom()
        {
            var expectedClientsTcp = new ConcurrentDictionary<SecureLobbyClient, string>();
            var expectedClientsUdp = new ConcurrentDictionary<SecureLobbyClient, string>();

            int totalUdp = 0;
            int totalTcp = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            string ip = "127.0.0.1";
            int port = 2222;
            int numClients = 10;
            SecureLobbyServer server = new SecureLobbyServer(port, scert);
            server.StartServer();

            List<SecureLobbyClient> clients = new List<SecureLobbyClient>();

            for (int i = 0; i < numClients; i++)
            {
                var cl = new SecureLobbyClient(cert);
                cl.OnTcpRoomMesssageReceived += (r, m) => TcpReceived(cl, r, m);
                cl.OnUdpRoomMesssageReceived += (r, m) => UdpReceived(cl, r, m);
                cl.Connect(ip, port);
                cl.CreateOrJoinRoom("WA");
                if(i%2 == 0)
                    cl.CreateOrJoinRoom("SA");
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
            clients[0].SendMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].SendUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].SendMessageToRoom("SA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].SendUdpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].GetAvailableRooms().ContinueWith((m) => Console.WriteLine(m.Result[0]));


            void UdpReceived(SecureLobbyClient cl, string r, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl, null);
                Interlocked.Increment(ref totalUdp);
            }

            void TcpReceived(SecureLobbyClient cl, string r, MessageEnvelope m)
            {
                expectedClientsTcp.TryAdd(cl, null);
                Interlocked.Increment(ref totalTcp);

            }
            Thread.Sleep(2000);
            Assert.IsTrue(expectedClientsTcp.Count == clients.Count - 1);
            Assert.AreEqual(clients.Count - 1, expectedClientsUdp.Count);
            Assert.AreEqual(13, totalTcp);
            Assert.AreEqual(13, totalUdp);
        }

    }
}
