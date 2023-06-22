﻿using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.UDP.Reliable;
using NetworkLibrary.Utils;
using Protobuff.P2P;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace RelayBenchmark
{
    internal class Program
    {
        private static int totMsgCl;

        static void Main(string[] args)
        {
            //ThreadPool.SetMinThreads(2000, 2000);
            MiniLogger.AllLog += Console.WriteLine;
            //PoolBench();
            RelayTest();
           // TestReliableModules();
           
            Console.ReadLine();
        }
  
        private static void TestReliableModules()
        {
            Console.Clear();
            Console.ReadLine();
            Stopwatch sw = new Stopwatch();
            int count = 10000;
            int completed = count;
            Mockup m = new Mockup();
            m.RemoveNoiseFeedback = true;
            m.RemoveNoiseSend = true;

            byte[] data = new byte[40];
            byte[] data1 = new byte[1];
            ArraySegment<byte> f = new ArraySegment<byte>(data);
            ArraySegment<byte>s = new ArraySegment<byte>(data1,0,data1.Length-0);
            byte[] d4 = new byte[129000+65555];

            int ccc = f.Count + s.Count;

            m.OnReceived += (voff, off, cnt) =>
            {
                if (cnt == ccc)
                {

                    if (Interlocked.Decrement(ref completed) == 0)
                    {
                      
                        Console.WriteLine("########################################");
                        Console.WriteLine(sw.ElapsedMilliseconds);

                    }
                }
                else
                {

                }
            };
            sw.Start();

            for (int i = 0; i < count; i++)
            {
               m.SendTest(f,s);
                //m.SendTest(d4,0,d4.Length);

            }
            while (true)
            {
                Console.ReadLine() ;
                Console.WriteLine("completed: "+completed);
                Console.WriteLine("arrived : "+m.getArrivedCount());
                Console.WriteLine("pending : "+m.getActiveCount());
            }
            
        }

        private static void SerializerTest()
        {
            PooledMemoryStream stream = new PooledMemoryStream();
            MessageEnvelope env = new MessageEnvelope()
            {
                IsInternal = true,
                From = Guid.NewGuid(),
                To = Guid.NewGuid(),
                Header = "rattatta",

                MessageId = Guid.NewGuid(),
                TimeStamp = DateTime.Now,
                KeyValuePairs = new Dictionary<string, string>() {
                    { "K1", "v2" } ,
                    { "K3", "" },
                    { "K2", null } ,
                    { "K4", "%%" } ,
                }
            };
            EnvelopeSerializer.Serialize(stream, env);
            var result = EnvelopeSerializer.Deserialize(stream.GetBuffer(), 0);
            stream.Position = 0;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 50000000; i++)
            {
               // EnvelopeSerializer.Serialize(stream, env);
                var r = EnvelopeSerializer.DeserializeToRouterHeader(stream.GetBuffer(), 0);
                stream.Position = 0;

            }
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
        static MessageEnvelope testMessage => new MessageEnvelope()
        {
            Header = "Test",
            Payload = new byte[32]
        };
        static Stopwatch sw = new Stopwatch();
        private static async void RelayTest()
        {
            string ip = "79.19.128.177";
            //string ip = "127.0.0.1";
            

            var cert = new X509Certificate2("client.pfx", "greenpass");
            var scert = new X509Certificate2("server.pfx", "greenpass");

            //var server = new SecureProtoRelayServer(20011, scert);
           // server.StartServer();
            //Task.Run(async () => { while (true) { await Task.Delay(10000); server.GetTcpStatistics(out var generalStats, out _); Console.WriteLine(generalStats.ToString()); } });
            var clients = new ConcurrentBag<RelayClient>();
            int numclients = 2;
            var pending = new Task[numclients];
            Parallel.For(0, numclients, (i) =>
            //for (int i = 0; i < numclients; i++)

            {
                var client = new RelayClient(cert);
                client.OnMessageReceived += (reply) => ClientMsgReceived(client, reply);
                client.OnUdpMessageReceived += (reply) => ClientUdpReceived(client, reply);
               //client.OnPeerRegistered += (id) => { if (client.sessionId.CompareTo(id) > 0) client.RequestHolePunchAsync(id, 10000, false); };
                try
                {
                    pending[i] = client.ConnectAsync(ip, 20011);
                   // client.Connect(ip, 20011);
                    clients.Add(client);
                  //  client.StartPingService();
                }
                catch { }

                //Thread.Sleep(1000);
            }
            );
            Task.WaitAll(pending);
            Console.WriteLine("All Connected");
           Thread.Sleep(2000);
            int cc = 0;
            List<Task<bool>> pndg = new List<Task<bool>>();
            foreach (var client in clients)
            {
                if (client.sessionId == Guid.Empty)
                    throw new Exception();
               // Console.WriteLine("--- -- - | "+client.sessionId+" count: " + client.Peers.Count);
                foreach (var peer in client.Peers)
                {
                    if (client.sessionId.CompareTo(peer.Key)>0)
                    {
                        if (peer.Key == Guid.Empty)
                            throw new Exception();

                        //var a = client.RequestHolePunchAsync(peer.Key, 10000, false);
                        //pndg.Add(a);
                        //client.TestHP(peer.Key, 10000, false);
                        //  Console.WriteLine(peer.Key+" cnt=> "+ ++cc);
                    }

                }
            }
            Task.WaitAll(pndg.ToArray());
            Console.WriteLine("all good");
           
          
            Thread.Sleep(100);
            // Parallel.ForEach(clients, (client) =>
            foreach (var client in clients)
            {
                var testMessage = new MessageEnvelope()
                {
                    Header = "Test",
                    Payload = new byte[320000000]
                };
                for (int i = 0; i < 1; i++)
                {
                    //return;
                    foreach (var peer in client.Peers.Keys)
                    {
                        //await client.SendRequestAndWaitResponse(peer, testMessage,1000);
                       //client.SendAsyncMessage(peer, testMessage);

                        //client.SendUdpMesssage(peer, testMessage);
                        //  client.BroadcastMessage(testMessage);
                        //client.BroadcastUdpMessage(testMessage);
                      client.SendRudpMessage(peer,testMessage);
                    }
                }
                 testMessage = new MessageEnvelope()
                {
                    Header = "Test",
                    Payload = new byte[320]
                };
                //for (int i = 0; i < 20; i++)
                //{
                //    foreach (var peer in client.Peers.Keys)
                //        client.SendRudpMessage(peer, testMessage);

                //}
                break;

            }
           // );

            sw.Start();
          
            void ClientMsgReceived(RelayClient client, MessageEnvelope reply)
            {
                //Interlocked.Increment(ref totMsgCl);
                client.SendAsyncMessage(reply.From, reply);
               // Console.WriteLine("R     " + sw.ElapsedMilliseconds);
                sw.Restart();
            }

           
            void ClientUdpReceived(RelayClient client, MessageEnvelope reply)
            {
               
                // Interlocked.Increment(ref totMsgCl);
                 //client.SendUdpMesssage(reply.From, reply);
                client.SendRudpMessage(reply.From, reply);

                
               // Console.WriteLine(" ************ R     "+sw.ElapsedMilliseconds);
                sw.Restart();


                //if(Interlocked.Increment(ref am) % 10000 == 0)
                // {
                //     Console.WriteLine("time  " + sw.ElapsedMilliseconds);
                //     sw.Restart();

                // }

                //sw.Restart();
                //Console.WriteLine("R");

            }
            Console.ReadLine();
        }
        static long am = 0;
    }
}