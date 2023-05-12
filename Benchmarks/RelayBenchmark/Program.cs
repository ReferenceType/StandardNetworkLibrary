using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using Protobuff.P2P;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace RelayBenchmark
{
    internal class Program
    {
        private static int totMsgCl;

        static void Main(string[] args)
        {
            MiniLogger.AllLog += (l) => Console.WriteLine(l);
             RelayTest();
           
            Console.ReadLine();
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
        private static void RelayTest()
        {
            string ip = "127.0.0.1";
            MessageEnvelope testMessage = new MessageEnvelope()
            {
                Header = "Test",
               // Payload = new byte[32]
            };

            var cert = new X509Certificate2("client.pfx", "greenpass");
            var scert = new X509Certificate2("server.pfx", "greenpass");

            // var server = new SecureProtoRelayServer(20011, scert);
            var clients = new ConcurrentBag<RelayClient>();
            //for (int i = 0; i < 200; i++)
            int numclients = 20;
            Task[] pending = new Task[numclients];
            // Parallel.For(0, numclients, (i) =>
            for (int i = 0; i < numclients; i++)

            {
                var client = new RelayClient(cert);
                client.OnMessageReceived += (reply) => ClientMsgReceived(client, reply);
                client.OnUdpMessageReceived += (reply) => ClientUdpReceived(client, reply);
                //client.OnPeerRegistered+=(id)=> client.RequestHolePunchAsync(id, 10000, false);
                try
                {
                    pending[i]= client.ConnectAsync(ip, 20011);

                    clients.Add(client);
                client.StartPingService();
                }
                catch { }

                //Thread.Sleep(1000);
            }
            //);
            Task.WaitAll(pending);
           Thread.Sleep(5000);
            int cc = 0;
            List<Task<bool>> pndg = new List<Task<bool>>();
            foreach (var client in clients)
            {
                if (client.sessionId == Guid.Empty)
                    throw new Exception();
               // Console.WriteLine("--- -- - | "+client.sessionId+" count: " + client.Peers.Count);
                foreach (var peer in client.Peers)
                {
                    if (client.sessionId>peer.Key)
                    {
                        if (peer.Key == Guid.Empty)
                            throw new Exception();

                        var a = client.RequestHolePunchAsync(peer.Key, 10000, false);
                        pndg.Add(a);
                       
                        //  Console.WriteLine(peer.Key+" cnt=> "+ ++cc);
                    }

                }
            }
            Task.WaitAll(pndg.ToArray());
            int kk = 0;
            foreach (var item in pndg)
            {
                kk++;

                if (item.Result == false)
                {
                    Console.WriteLine("            +++++++++-------***************---------------- Fucked");
                }
                else
                {
                    Console.WriteLine("All good"+kk);
                }
            }

            Task.Run(async () =>
            {
                while(true)
                {
                    await Task.Delay(3000);
                    Console.WriteLine(totMsgCl);
                }
              
            });
            Thread.Sleep(5000);
            Parallel.ForEach(clients, (client) =>
            {
                for (int i = 0; i < 10; i++)
                {
                    //return;
                    foreach (var peer in client.Peers.Keys)
                    {
                        //await client.SendRequestAndWaitResponse(peer, testMessage,1000);
                        //client.SendAsyncMessage(peer, testMessage);

                      //  client.SendUdpMesssage(peer, testMessage);
                    }
                }

            });

           
          
            void ClientMsgReceived(RelayClient client, MessageEnvelope reply)
            {
                //Interlocked.Increment(ref totMsgCl);
                client.SendAsyncMessage(reply.From, reply);

            }


            void ClientUdpReceived(RelayClient client, MessageEnvelope reply)
            {
                Interlocked.Increment(ref totMsgCl);
                client.SendUdpMesssage(reply.From, reply);
               
            }
        }

    }
}