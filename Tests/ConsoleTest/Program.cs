
using NetworkLibrary.TCP.SSL;
using NetworkLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.UDP;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.Utils;
using System.Security.Cryptography;
using NetworkLibrary.TCP.SSL.Custom;
using System.Security.Cryptography.X509Certificates;
using NetworkLibrary.Components;
using Protobuff;
using NetworkLibrary.TCP;
using System.Runtime.InteropServices;
using System.Net.Security;
using Protobuff.P2P;
using System.Security;
using System.Numerics;
using NetworkLibrary.UDP.Secure;
using System.Buffers;
using Microsoft.Win32;
using NetworkLibrary.Components.Statistics;
using ProtoBuf;

namespace ConsoleTest
{

    internal class Program
    {
        static int i = 0;
        static List<AsyncUdpClient> clients = new List<AsyncUdpClient>();
        static HashSet<int> s = new HashSet<int>();

        static Stopwatch sw = new Stopwatch();
        static Stopwatch sw2 = new Stopwatch();
       
        //static AutoResetEvent are = new AutoResetEvent (false);
        static int totMsgCl = 0;
        static int totMsgsw = 0;
        private static ByteMessageTcpServer server;
        static byte[] resp = new byte[32];
        static bool lastSW=false;
        private static int prev=-1;
        static Random randomG = new Random();
        private static bool pause;

        static ArrayPool<byte> pool = ArrayPool<byte>.Shared;

        static void Main(string[] args)
        {
            MiniLogger.AllLog += (log) => Console.WriteLine(log);
            //ExampleProtoSecure();
            //ExampleByteMessage();
            //PoolTest();
            //UdpProtoTest();
            //EnvelopeTest();
            RelayTest();
            //ByteCopyTest();
            //BitConverterTest();
            //ByteCopyTest2();
            //ProtoTest();
            //SecureProtoTest();
            //MessageParserInliningTest();
            //AesTest();
            //SSlTest2();
            //SSlTest();
            //TcpTest();

            //UdpTest();
            //UdpTest2();
            //UdpTestMc();
           
            
            
        }

        private static void ExampleByteMessage()
        {
            var ipHostEntry = Dns.GetHostEntry("REMOTE_HOST");

            ByteMessageTcpServer server = new ByteMessageTcpServer(20008);
            server.OnBytesReceived += ServerBytesReceived;
            server.StartServer();

            ByteMessageTcpClient client = new ByteMessageTcpClient();
            client.OnBytesReceived += ClientBytesReceived;
            client.Connect("127.0.0.1", 20008);

            client.SendAsync(UTF8Encoding.ASCII.GetBytes("Hello I'm a client!"));

            void ServerBytesReceived(in Guid clientId, byte[] bytes, int offset, int count)
            {
                Console.WriteLine(UTF8Encoding.ASCII.GetString(bytes, offset, count));
                server.SendBytesToClient(clientId, UTF8Encoding.ASCII.GetBytes("Hello I'm the server"));
            }

            void ClientBytesReceived(byte[] bytes, int offset, int count)
            {
                Console.WriteLine(UTF8Encoding.ASCII.GetString(bytes, offset, count));
            }
        }

        [ProtoContract]
        class SamplePayload :IProtoMessage
        {
            [ProtoMember(1)]
            public string sample;
        }

        private static async Task ExampleProtoSecure()
        {
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");

            SecureProtoServer server = new SecureProtoServer(20008, scert);
            server.OnMessageReceived += ServerMessageReceived;

            var client = new SecureProtoClient(cert);
            client.OnMessageReceived += ClientMessageReceived;
            client.Connect("127.0.0.1", 20008);

            var Payload = new SamplePayload() { sample = "Hello" };
            var messageEnvelope = new MessageEnvelope();

            // You can just send a message, get replies on ClientMessageReceived.
            client.SendAsyncMessage(messageEnvelope);
            client.SendAsyncMessage(messageEnvelope,Payload);

            // Or you can wait for a reply async.
            MessageEnvelope result = await client.SendMessageAndWaitResponse(messageEnvelope, Payload);
            var payload = result.UnpackPayload<SamplePayload>();

            void ServerMessageReceived(in Guid clientId, MessageEnvelope message)
            {
                server.SendAsyncMessage(in clientId, message);
            }

            void ClientMessageReceived(MessageEnvelope message)
            {
            }
        }


        private static void PoolTest()
        {
            Random rng =  new Random(42);
            sw.Start();
            AutoResetEvent a =  new AutoResetEvent(false);
            ConcurrentQueue<byte[]> qq = new ConcurrentQueue<byte[]>();

            Thread t1 = new Thread(() =>
            {
                for (int i = 0; i < 10000000; i++)
                {

                    var buf = BufferPool.RentBuffer(512);
                    qq.Enqueue(buf);
                    a.Set();


                };
            });
            Thread t2 = new Thread(() =>
            {

                while (true)
                {
                    a.WaitOne();
                    while (qq.TryDequeue(out var buf))
                        BufferPool.ReturnBuffer(buf);
                }
            });

            t1.Start();
            t2.Start();
            t1.Join();

          


                sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            //for (int i = 0; i < 10000000; i++)
            //{
                
            //}


            var buf2 = BufferPool.RentBuffer(rng.Next(257, 100000000));

            Console.ReadLine();
        }

        private static void UdpProtoTest()
        {
            var random = new byte[16];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(random);

            ConcurrentAesAlgorithm algo = new ConcurrentAesAlgorithm(random, random);
            ConcurrentAesAlgorithm algo2 = new ConcurrentAesAlgorithm(random, random);

            EncryptedUdpProtoClient client1 = new EncryptedUdpProtoClient(algo);
            EncryptedUdpProtoClient client2 = new EncryptedUdpProtoClient(algo2);

            client1.Bind(20011);
            client2.Bind(20012);

            client1.SetRemoteEnd("192.168.1.13", 20012);
            client2.SetRemoteEnd("95.238.127.208", 20011);



            byte[] fuck = new byte[20880];
            for (int i = 0; i < fuck.Length; i++)
            {
                fuck[i] = (byte)i;
            }
            MessageEnvelope fuckery = new MessageEnvelope();
            fuckery.Payload = fuck;

            client1.OnMessageReceived += c1;
            client2.OnMessageReceived += c2;
            Parallel.For(0, 100, (i) =>
            {
                Task.Run(() =>
                {
                    client1.SendAsyncMessage(fuckery);
                });
            });
            Parallel.For(0, 100, (i) =>
            {
                Task.Run(() =>
                {
                    client2.SendAsyncMessage(fuckery);
                });
            });


            while (Console.ReadLine() !="e")
            {
                Console.WriteLine(totMsgCl);
                Console.WriteLine(totMsgsw);
            }

            void c2(MessageEnvelope obj)
            {
                //Console.WriteLine("2");
                MessageEnvelope fuckery1 = new MessageEnvelope();
                fuckery1.Payload = new byte[randomG.Next(500, 32000)];
                client2.SendAsyncMessage(fuckery1);
                Task.Run(() => client2.SendAsyncMessage(fuckery));

                totMsgCl++;
            }

             void c1(MessageEnvelope obj)
            {
                //Console.WriteLine("1");
                MessageEnvelope fuckery1 = new MessageEnvelope();
                fuckery1.Payload = new byte[randomG.Next(500, 32000)];
                
                client1.SendAsyncMessage(fuckery);
                Task.Run(() => client1.SendAsyncMessage(fuckery));
                totMsgsw++;
            }
        }

       

        //private static void EnvelopeTest()
        //{
        //    ConcurrentProtoSerialiser s = new ConcurrentProtoSerialiser();
        //    MessageEnvelope msg = new MessageEnvelope()
        //    {
        //        Header = "Test",

        //    };
        //    MessageEnvelope msg2 = new MessageEnvelope()
        //    {
        //        Header = "pay",

        //    };

        //    var payload = new byte[1123];
        //    var env = s.EnvelopeAndSerialiseMessage(msg, msg2);
        //    var val = s.DeserialiseEnvelopedMessage(env,0,env.Length);
        //    var val1 = s.DeserialiseOnlyEnvelope(env,0,env.Length);
        //    var val2 = s.DeserialiseOnlyPayload<MessageEnvelope>(env,0,env.Length);

        //}

        

        private static void BitConverterTest()
        {
            int num = 1234567890;
            byte[] h0 = BitConverter.GetBytes(num);
            sw.Start();
            for (int i = 0; i < 200000000; i++)
            {
                m2(h0,0);
            }
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            Console.ReadLine();
            int m1(byte[] h, int offset)
            {
                return BitConverter.ToInt32(h, offset);
            }
            int m2(byte[] h, int offset)
            {
                if(BitConverter.IsLittleEndian)
                    return (int)(h[offset] | h[offset + 1] << 8 | h[offset + 2] << 16 | h[offset + 3] << 24);
                else
                    return (int)(h[offset+3] | h[offset + 2] << 8 | h[offset + 1] << 16 | h[offset] << 24);


            }
        }

        private static void RelayTest()
        {
            MessageEnvelope testMessage = new MessageEnvelope()
            {
                Header = "Test",
                Payload = new byte[32]
            };
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");

            long TotUdp = 0;
            //var server = new SecureProtoRelayServer(20011, 1000, scert);
            //Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        await Task.Delay(3000);
            //        server.GetTcpStatistics(out SessionStats generalStats, out _);
            //        server.GetUdpStatistics(out UdpStatistics generalStatsUdp, out _);
            //        Console.WriteLine(generalStats.ToString());
            //        Console.WriteLine(generalStatsUdp.ToString());
            //    }

            //});

            var clients = new List<RelayClient>();
            for (int i = 0; i < 100; i++)
            {
                var client = new RelayClient(cert);
                client.OnMessageReceived += (reply) => ClientMsgReceived(client, reply);
                client.OnUdpMessageReceived += (reply) => ClientUdpReceived(client, reply);
                client.OnPeerRegistered += (peerId) => OnPeerRegistered(client,peerId);

                try
                {
                    client.Connect("127.0.0.1", 20011);
                    clients.Add(client);
                    //client.StartPingService();
                }
                catch { }
               
                //Thread.Sleep(1000);
            }
            var client1 = new RelayClient(cert);
            client1.OnMessageReceived += (reply) => ClientMsgReceived(client1, reply);
            client1.OnUdpMessageReceived += (reply) => ClientUdpReceived(client1, reply);
            client1.OnPeerRegistered += (peerId) => OnPeerRegistered(client1, peerId);

            client1.Connect("127.0.0.1", 20011);
            clients.Add(client1);
            Thread.Sleep(3500);
            //clients[0].Disconnect();
            //while (!clients[0].RequestHolePunch(clients[0].Peers.First()))
            //{

            //}
            Task.Run( () =>
            {
                return;
                Parallel.ForEach(clients, async(client) =>
                {
                    foreach (var peer in client.Peers)
                    {
                        while (true)
                        {
                            await client.SendRequestAndWaitResponse(peer, testMessage, 1000);
                        }
                    }
                });
            });
            Parallel.ForEach(clients,  (client) =>
            {
                //return;
                
                for (int i = 0; i < 1000; i++)
                {
                    //return;
                    foreach (var peer in client.Peers)
                    {
                        //await client.SendRequestAndWaitResponse(peer, testMessage,1000);
                        client.SendAsyncMessage(peer, testMessage); 

                        //client.SendUdpMesssage(peer, testMessage);
                    }
                }

            });
            //clients[0].SendUpMesssage(clients[0].Peers.First(), new byte[32], "testData");
            while (Console.ReadLine()!="e")
            {
                Console.WriteLine(totMsgCl);
                Console.WriteLine(TotUdp);
            }

            void OnPeerRegistered(RelayClient client, Guid peerId)
            {
               // Console.WriteLine(peerId);
                return;
                for (int i = 0; i < 1; i++)
                {
                    client.SendAsyncMessage(peerId, testMessage);
                    client.SendUdpMesssage(peerId, testMessage);
                }

            }
            void ClientMsgReceived(RelayClient client, MessageEnvelope reply)
            {
                Interlocked.Increment(ref totMsgCl);
                client.SendAsyncMessage(reply.From, reply);

            }

           
            void ClientUdpReceived(RelayClient client, MessageEnvelope reply)
            {
                Interlocked.Increment(ref TotUdp);

                //client.SendUpMesssage(reply.From, reply);
                //reply.Payload = new byte[randomG.Next(500, 32000)];
                client.SendUdpMesssage(client.Peers.First(), reply);
                //Task.Run(() =>
                //{
                //    client.SendUdpMesssage(client.Peers.First(), reply);

                //});
                //client.SendUpMesssage(reply.From, new byte[32], "testData");

            }
        }

        //private static void SecureProtoTest()
        //{
        //    CoreAssemblyConfig.UseUnmanaged = true;
        //    int totMsgCl = 0;
        //    int totMsgsw = 0;
        //    Stopwatch sw = new Stopwatch();
        //    MessageEnvelope msg = new MessageEnvelope()
        //    {
        //        Header = "Test",
        //        Payload = new byte[2]

        //    };

        //    MessageEnvelope end = new MessageEnvelope()
        //    {
        //        Header = "Stop"
        //    };

        //    var scert = new X509Certificate2("server.pfx", "greenpass");
        //    var cert = new X509Certificate2("client.pfx", "greenpass");

        //    var server = new SecureProtoServer(20008, 100, scert);
        //    server.OnStatusMessageReceived += ServerStsReceived;
        //    server.OnRequestReceived += ServerRequestReceived;

        //    var clients = new List<SecureProtoClient>();
        //    for (int i = 0; i < 100; i++)
        //    {
        //        var client = new SecureProtoClient(cert);
        //        client.OnStatusMessageReceived += (reply) => ClientStsReceived(client, reply);
        //        client.OnRequestReceived += (reply) => ClientReqReceived(client, reply);

        //        client.Connect("127.0.0.1", 20008);
        //        clients.Add(client);
        //    }
        //    Task.Run(async () =>
        //    {
        //        while (true)
        //        {
        //            await Task.Delay(10000);
        //            Console.WriteLine("client {0} server {1}", totMsgCl, totMsgsw);

        //        }

        //    });
        //    sw.Start();
        //    Parallel.ForEach(clients, client =>
        //    {
        //        for (int i = 0; i < 50000; i++)
        //        {
        //            client.SendRequestMessage(msg);
        //        }
        //        //client.SendStatusMessage(end);

        //    });

        //    //var resp = clients[0].SendMessageAndWaitResponse(msg).Result;
        //    while (Console.ReadLine() != "e")
        //    {
        //        Console.WriteLine("client {0} server {1}", totMsgCl, totMsgsw);
        //    }
        //    Console.ReadLine();


        //    void ServerStsReceived(in Guid arg1, MessageEnvelope arg2)
        //    {
        //        Interlocked.Increment(ref totMsgsw);
        //        server.SendStatusMessage(arg1, arg2);
        //    }

        //    void ClientStsReceived(SecureProtoClient client, MessageEnvelope reply)
        //    {
        //        //Interlocked.Increment(ref totMsgCl);
        //        //if (reply.Header.Equals("Stop"))
        //        //{
        //        //    Console.WriteLine(sw.ElapsedMilliseconds);
        //        //}
        //    }

        //    void ServerRequestReceived(in Guid arg1, MessageEnvelope arg2)
        //    {
        //        Interlocked.Increment(ref totMsgsw);

        //        //server.SendResponseMessage(arg1, arg2.Id, arg2);
        //        server.SendRequestMessage(arg1, arg2);
        //    }

        //    void ClientReqReceived(SecureProtoClient client, MessageEnvelope message)
        //    {
        //        Interlocked.Increment(ref totMsgCl);

        //        //client.SendResponseMessage( message.Id,message);
        //        client.SendRequestMessage(message);
        //    }
        //}

        private static void ByteCopyTest()
        {
           byte[] bytes = new byte[128000];
            sw.Start();
            for (int i = 0; i < 100000000; i++)
            {
                copyBlock();
                //ByteCopy.ToArray(bytes, 0, 32);
            }
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            Console.ReadLine();

            byte[] copyspan()
            {
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(bytes, 0, 32);
                return span.ToArray();
            }
            byte[] copyBlock()
            {
                var result = new byte[32];
                Buffer.BlockCopy(bytes, 0, result, 0, 32);
                return result;
            }




        }

        //private static void ProtoTest()
        //{
        //    CoreAssemblyConfig.UseUnmanaged=false;
        //    int totMsgCl = 0;
        //    int totMsgsw = 0;
        //    Stopwatch sw = new Stopwatch();
        //    MessageEnvelope msg = new MessageEnvelope()
        //    {
        //        Header = "Test",
        //        Payload = new byte[2]
                
        //    };
           
        //    MessageEnvelope end = new MessageEnvelope()
        //    {
        //        Header = "Stop"
        //    };

        //    ProtoServer server = new ProtoServer(20008, 100);
        //    server.OnStatusMessageReceived += ServerStsReceived;
        //    server.OnRequestReceived += ServerRequestReceived;

        //    var clients = new List<ProtoClient>();
        //    for (int i = 0; i < 100; i++)
        //    {
        //        var client = new ProtoClient();
        //        client.OnStatusMessageReceived +=(reply)=> ClientStsReceived(client,reply);
        //        client.OnRequestReceived +=(reply)=> ClientReqReceived(client,reply);

        //        client.Connect("127.0.0.1", 20008);
        //        clients.Add(client);
        //    }
        //    Task.Run(async () =>
        //    {
        //        while (true)
        //        {
        //            await Task.Delay(10000);
        //            Console.WriteLine("client {0} server {1}", totMsgCl, totMsgsw);

        //        }

        //    });
        //    sw.Start();
        //    Parallel.ForEach(clients, client =>
        //    {
        //        for (int i = 0; i < 1000; i++)
        //        {
        //            client.SendRequestMessage(msg);
        //        }
        //        //client.SendStatusMessage(end);

        //    });

        //    //var resp = clients[0].SendMessageAndWaitResponse(msg).Result;
        //    while (Console.ReadLine() != "e")
        //    {
        //        Console.WriteLine("client {0} server {1}",totMsgCl,totMsgsw);
        //    }
        //    Console.ReadLine();


        //    void ServerStsReceived(in Guid arg1, MessageEnvelope arg2)
        //    {
        //        Interlocked.Increment(ref totMsgsw);
        //        server.SendStatusMessage(arg1,arg2);
        //    }

        //    void ClientStsReceived(ProtoClient client, MessageEnvelope reply)
        //    {
        //        //Interlocked.Increment(ref totMsgCl);
        //        //if (reply.Header.Equals("Stop"))
        //        //{
        //        //    Console.WriteLine(sw.ElapsedMilliseconds);
        //        //}
        //    }

        //    void ServerRequestReceived(in Guid arg1, MessageEnvelope arg2)
        //    {
        //        Interlocked.Increment(ref totMsgsw);

        //        //server.SendResponseMessage(arg1, arg2.Id, arg2);
        //        server.SendRequestMessage(arg1, arg2);
        //    }

        //    void ClientReqReceived(ProtoClient client, MessageEnvelope message)
        //    {
        //        Interlocked.Increment(ref totMsgCl);

        //        //client.SendResponseMessage( message.Id,message);
        //        client.SendRequestMessage(message);
        //    }

        //}

       

       

        private static void UdpTest2()
        {
            AsyncUdpClient cl = new AsyncUdpClient(2008);
            cl.OnBytesRecieved += ClientBytesRecieved;
            //cl.Connect("127.0.0.1", 2009);
            cl.SetRemoteEnd("127.0.0.1", 2009);
            cl.SocketSendBufferSize = 64000;
            cl.ReceiveBufferSize = 64000;

            AsyncUdpClient cl2 = new AsyncUdpClient(2009);
            cl2.OnBytesRecieved += ClientBytesRecieved;
            //cl2.Connect("127.0.0.1", 2008);
            cl2.SetRemoteEnd("127.0.0.1", 2008);
           
            cl2.SocketSendBufferSize = 64000;
            cl2.ReceiveBufferSize = 64000;

            cl.SendAsync(new byte[32]);
            cl2.SendAsync(new byte[32]);

            Console.ReadLine();

        }

        private static void SSlTest2()
        {
            Stopwatch sw = new Stopwatch();
            int totMsgClient = 0;
            int totMsgServer = 0;
            byte[] req = new byte[32];
            byte[] resp = new byte[32];
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            CustomSslServer server = new CustomSslServer(2008, scert);
            server.OnBytesReceived += ServerReceived;
            server.StartServer();

            CustomSslClient client = new CustomSslClient(cert);
            client.OnBytesReceived += ClientReceived;
            client.ConnectAsyncAwaitable("127.0.0.1", 2008).Wait();
            sw.Start();
            for (int i = 0; i < 1000000; i++)
            {
                client.SendAsync(req);

            }
            client.SendAsync(new byte[502]);

            while (Console.ReadLine() != "e")
            {
                Console.WriteLine("Tot server" + Volatile.Read(ref totMsgServer));
                Console.WriteLine("Tot client" + Volatile.Read(ref totMsgClient));
            }

            void ServerReceived(in Guid arg1, byte[] arg2, int arg3, int arg4)
            {
                Interlocked.Increment(ref totMsgServer);
                if(arg4 == 502)
                {
                    server.SendBytesToClient(arg1,new byte[502]);
                    return;
                }
                server.SendBytesToClient(arg1, resp);
            }

            void ClientReceived( byte[] arg2, int arg3, int arg4)
            {
                Interlocked.Increment(ref totMsgClient);
                if(arg4!= 32)
                {

                }
                if(arg4 == 502)
                {
                    sw.Stop();
                    Console.WriteLine(sw.ElapsedMilliseconds);
                }
               // client.SendAsync(req);
            }
        }

       

        private static void SSlTest()
        {
            int totMsgClient=0;
            int totMsgServer=0;
            byte[] req = new byte[32];
            byte[] resp = new byte[32];

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var server = new SslByteMessageServer(2008, scert);
            server.OnBytesReceived += ServerReceived;
            server.StartServer();

            var cert = new X509Certificate2("client.pfx", "greenpass");
            SslByteMessageClient client = new SslByteMessageClient(cert);

            client.OnBytesReceived += ClientReceived;
            client.RemoteCertificateValidationCallback += A;

            client.Connect("127.0.0.1", 2008);
            for (int i = 0; i < 10000000; i++)
            {
                client.SendAsync(req);

            }

            while (Console.ReadLine()!="e")
            {
                Console.WriteLine("Tot server" + Volatile.Read(ref totMsgServer));
                Console.WriteLine("Tot client" + Volatile.Read(ref totMsgClient));
            }

            void ServerReceived(in Guid arg1, byte[] arg2, int arg3, int arg4)
            {
                Interlocked.Increment(ref totMsgServer);
                server.SendBytesToClient(arg1, resp);
            }

            void ClientReceived( byte[] arg2, int arg3, int arg4)
            {
                Interlocked.Increment(ref totMsgClient);

                //client.Send(req);
            }
        }

        private static bool A(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
           return true;
        }

        private static void UdpTestMc()
        {
            string ip = "239.255.0.1";
            AsyncUdpServer sw = new AsyncUdpServer(2008);
            sw.SetMulticastAddress(ip, 2005);
            sw.StartServer();

            AsyncUdpClient cl = new AsyncUdpClient(2005);
            //cl.Connect("127.0.0.1",2008);
            cl.SetRemoteEnd("127.0.0.1",2008);
            cl.SendAsync(new byte[111]);
            cl.JoinMulticastGroup(IPAddress.Parse(ip));

            sw.OnBytesRecieved += UdpSWBytsRec;
            cl.OnBytesRecieved += ClientBytesRecieved;
            Thread.Sleep(1000);

            sw.SendBytesToAllClients(new byte[111]);
            for (int i = 0; i < 2000; i++)
            {
                sw.MulticastMessage(new byte[1000]);

            }
            Console.WriteLine("Done mc");
            Console.ReadLine();

            Console.ReadLine();
            Console.WriteLine("totmeg server rec: " + totMsgsw);
            Console.WriteLine("totmeg client rec: " + totMsgCl);
            Console.WriteLine(i);
            Console.ReadLine();
            Console.WriteLine("totmeg server rec: " + totMsgsw);
            Console.WriteLine("totmeg client rec: " + totMsgCl);
            Console.ReadLine();
        }

        static void UdpTest()
        {
            int clAmount = 100;
            AsyncUdpServer sw = new AsyncUdpServer(2008);
            sw.StartServer();
            sw.OnBytesRecieved += UdpSWBytsRec;
            sw.SocketSendBufferSize = 128000000;
            sw.SocketReceiveBufferSize = 128000000;

            var clients = new List<AsyncUdpClient>();

            for (int j = 0; j < clAmount; j++)
            {
                AsyncUdpClient cl = new AsyncUdpClient();
                cl.OnBytesRecieved += ClientBytesRecieved;
                cl.Connect("127.0.0.1", 2008);
                cl.SendAsync(new byte[32]);
                cl.SocketSendBufferSize = 64000;
                cl.ReceiveBufferSize = 64000;
                clients.Add(cl);
            }
            
            Thread.Sleep(2222);

            var bytes_ = new byte[1500];
            int i = 0;

            var t = new Thread(() =>
            {
                for (i = 0; i < 5000; i++)
                {
                    Parallel.ForEach(clients, cl =>
                    {
                        cl.SendAsync(bytes_);
                    });
                  

                    //Console.WriteLine("Sending");
                    //Thread.Sleep(1);
                }
                Console.WriteLine("Done 1 " + sw2.ElapsedMilliseconds);

            });

            var t2 = new Thread(() =>
            {
                for (int j = 0; j < 5000; j++)
                {
                    sw.SendBytesToAllClients(bytes_);
                }
                Console.WriteLine("Done 2 " + sw2.ElapsedMilliseconds);

            });
            sw2.Reset();

            t.Start();
            t2.Start();
            sw2.Start();

            t.Join();
            t2.Join();

           //t.Wait();
            //t2.Wait();
            Console.WriteLine("Done all " + sw2.ElapsedMilliseconds);



            Console.ReadLine();
            Console.WriteLine("totmeg server rec: " + totMsgsw);
            Console.WriteLine("totmeg client rec: " + totMsgCl);
            Console.WriteLine(i);
            Console.ReadLine();
            Console.WriteLine("totmeg server rec: " + totMsgsw);
            Console.WriteLine("totmeg client rec: " + totMsgCl);
            Console.ReadLine();
        }
        static void UdpSWBytsRec(IPEndPoint endpoint, byte[] bytes, int offset, int count)
        {
            Interlocked.Increment(ref totMsgsw);
        }

        static void ClientBytesRecieved(byte[] bytes, int offset, int count)
        {
            Interlocked.Increment(ref totMsgCl);
           //  Console.WriteLine("udp client recieved");
            // cl.SendBytes(new byte[11111]);
        }
       

        //----------TCP ----------------------------------------------------------------
        private static void TcpTest()
        {
            var msg1 = new byte[32];
            int clAmount = 10;

            server = new ByteMessageTcpServer(2008);
            List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();
            server.MaxIndexedMemoryPerClient = 1280000000;
            server.DropOnBackPressure = false;
            server.StartServer();
            //BufferManager.InitContigiousSendBuffers(clAmount*2, 128000);
            //BufferManager.InitContigiousReceiveBuffers(clAmount*2, 128000);

            int dep =0;
            for (int i = 0; i < clAmount; i++)
            {

                var client = new ByteMessageTcpClient();
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion=false;
                //client.OnConnected += async () =>
                //{
                //    await Task.Delay(10000); Console.WriteLine("-------------------                            --------------"); client.Disconnect();
                //};
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => clientMsgRec2(client, arg2, offset, count);

                client.ConnectAsync("127.0.0.1", 2008);
                Console.WriteLine(server.SessionCount);

                clients.Add(client);
            }
            //client.SendAsync(new byte[123]);

            server.OnBytesReceived += SWOnMsgRecieved;
            Console.ReadLine();
            Console.WriteLine(server.SessionCount);           
            Console.ReadLine();
            Console.ReadLine();
            var msg = new byte[32];
            resp = new byte[32];

            for (int i = 0; i < msg.Length; i++)
            {
                msg[i] = 11;
            }


            const int numMsg = 1000000;
           
            var t1 = new Thread(() =>
            {
                for (int i = 0; i < numMsg; i++)
                {

                    foreach (var client in clients)
                    {
                        msg = new byte[32];
                        PrefixWriter.WriteInt32AsBytes(ref msg, 0, i);

                        client.SendAsync(msg);
                    }

                }

                foreach (var client in clients)
                {

                    client.SendAsync(new byte[502]);
                }

                //Thread.Sleep(111);


            });
            //t1.Start();
            sw2.Start();

            
            sw.Start();

            //-------------------
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg; i++)
                {
                    client.SendAsync(msg);
                   // if (i == 1000000)client.Disconnect();
                }
            });
            foreach (var client in clients)
            {
                //client.Disconnect();

                client.SendAsync(new byte[502]);
            }
            //-------------------
            //t1.Join();
            Console.WriteLine(sw2.ElapsedMilliseconds);

           // t2.Join();
            Console.WriteLine("2-- "+sw2.ElapsedMilliseconds);

            Console.ReadLine();
            GC.Collect();
            for (int i = 0; i < 6; i++)
            {
                Console.WriteLine("Total on server: "+totMsgsw);
                Console.WriteLine("Total on clients: "+totMsgCl);
                Console.WriteLine("2-- " + sw2.ElapsedMilliseconds);
                Console.WriteLine("last was sw "+lastSW);
                Console.WriteLine("sw ses count "+server.SessionCount);
                
                Console.ReadLine();
                if (i == 2)
                {
                    Console.WriteLine("will pause server");
                    pause = true;
                }
            }

            Console.ReadLine();


            Parallel.For(0, clients.Count, i =>
            {

                clients[i].Disconnect();

            });
            Console.WriteLine("DC");
            Console.ReadLine();
           
            Console.Read();
        }

        private static void clientMsgRec2(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
        {
           // Console.WriteLine("tot msg client: " + totMsgCl);

            clientMsgRec(arg2, offset, count);

            if (pause)
                return;
            client.SendAsync(resp);
            //client.SendAsync(resp);
            //Task.Run(() =>client.SendAsync(resp));
            //Task.Run(() =>client.SendAsync(resp));
            //client.SendAsync(resp);
            lastSW = false;
        }

        private static void clientMsgRec(/*ByteProtocolTcpClient client,*/ byte[] arg2, int offset,int count)
        {
            if (count == 502)
            {
                Console.WriteLine("Time client " + sw.ElapsedMilliseconds);
                Console.WriteLine("tot msg client: " + totMsgCl);

                //sw.Reset();
                //return;
            }
            
            Interlocked.Increment(ref totMsgCl);
            //Console.WriteLine("Sending");
            //client.SendAsync(resp);
            //if (totMsgCl % 1000000 == 0)
            //{
            //    Console.WriteLine(totMsgCl);
            //    Console.WriteLine("Time client " + sw.ElapsedMilliseconds);

            //}

        }

        private static void SWOnMsgRecieved(in Guid arg1, byte[] arg2, int offset, int count)
        {
           
            //server.SendBytesToClient(arg1, resp);
           // server.SendBytesToClient(arg1, resp);
            Interlocked.Increment(ref totMsgsw);


            if (count == 502)
            {
                Console.WriteLine("Time: " +sw2.ElapsedMilliseconds);
                Console.WriteLine("tot msg sw: " + totMsgsw);
                server.SendBytesToClient(arg1,new byte[502]);

                //return;
            }
            server.SendBytesToClient(arg1, resp);
            lastSW = true;

            //if (BitConverter.ToInt32(arg2, offset) != prev + 1)
            //{
            //    Console.WriteLine("--- Prev " + prev);
            //    Console.WriteLine("---- Curr " + BitConverter.ToInt32(arg2, offset));
            //}
            //prev = BitConverter.ToInt32(arg2, offset);

        }


    }
}
