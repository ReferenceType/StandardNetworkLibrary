using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary;
using NetworkLibrary.P2P;
using ProtoBuf;
using Protobuff.P2P;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

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
            clients[0].SendMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].SendUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].GetAvailableRooms().ContinueWith((m) => Console.WriteLine(m.Result[0]));


            void UdpReceived(SecureProtoRoomClient cl, string r, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl, null);
                Interlocked.Increment(ref totalUdp);
            }

            void TcpReceived(SecureProtoRoomClient cl, string r, MessageEnvelope m)
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
            clients[0].SendRudpMessageToRoom("WA", new MessageEnvelope() { Header = "RUdp Yo" });

            clients[0].SendMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo", Payload = new byte[128000] });
            clients[0].SendUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo", Payload = new byte[128000] });
            clients[0].SendRudpMessageToRoom("WA", new MessageEnvelope() { Header = "RUdp Yo", Payload = new byte[128000] });

            clients[0].GetAvailableRooms().ContinueWith((m) => Console.WriteLine(m.Result[0]));


            void UdpReceived(SecureProtoRoomClient cl, string r, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl, null);
                Interlocked.Increment(ref totalUdp);
            }

            void TcpReceived(SecureProtoRoomClient cl, string r, MessageEnvelope m)
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
                cl.OnTcpRoomMesssageReceived += (r, m) => TcpReceived(cl, r, m);
                cl.OnUdpRoomMesssageReceived += (r, m) => UdpReceived(cl, r, m);
                cl.Connect(ip, port);
                cl.CreateOrJoinRoom("WA");
                if (i % 2 == 0)
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
            clients[0].SendMessageToRoom("WA", new MessageEnvelope() { Header = "Tcp Yo" }, new Payload() { data =  new byte[128000] });

            clients[0].SendUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].SendUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[12800] });
            clients[0].SendUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[128000] });
            clients[0].SendUdpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo",Payload =  new byte[128000] });

            clients[0].SendRudpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].SendRudpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[12800] });
            clients[0].SendRudpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[128000] });
            clients[0].SendRudpMessageToRoom("WA", new MessageEnvelope() { Header = "Udp Yo", Payload = new byte[128000] });

            clients[0].SendMessageToRoom("SA", new MessageEnvelope() { Header = "Tcp Yo" });
            clients[0].SendMessageToRoom("SA", new MessageEnvelope() { Header = "Tcp Yo" }, new Payload() { data = new byte[128000] });

            clients[0].SendUdpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].SendUdpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[12800] });
            clients[0].SendUdpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[128000] });
            clients[0].SendUdpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo", Payload = new byte[128000] });

            clients[0].SendRudpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" });
            clients[0].SendRudpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[12800] });
            clients[0].SendRudpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo" }, new Payload() { data = new byte[128000] });
            clients[0].SendRudpMessageToRoom("SA", new MessageEnvelope() { Header = "Udp Yo", Payload = new byte[128000] });

            clients[0].GetAvailableRooms().ContinueWith((m) => Console.WriteLine(m.Result[0]));


            void UdpReceived(SecureProtoRoomClient cl, string r, MessageEnvelope m)
            {
                expectedClientsUdp.TryAdd(cl, null);
                Interlocked.Increment(ref totalUdp);
            }

            void TcpReceived(SecureProtoRoomClient cl, string r, MessageEnvelope m)
            {
                expectedClientsTcp.TryAdd(cl, null);
                Interlocked.Increment(ref totalTcp);

            }
            Thread.Sleep(2000);
            Assert.IsTrue(expectedClientsTcp.Count == clients.Count - 1);
            Assert.AreEqual(clients.Count - 1, expectedClientsUdp.Count);

            Assert.AreEqual(13*2, totalTcp);
            Assert.AreEqual(13*8, totalUdp);
            server.ShutdownServer();
        }

    }
}
