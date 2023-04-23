using NetworkLibrary.Components.Statistics;
using NetworkLibrary.Utils;
using Protobuff;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

internal class Program
{
    static int port = 20007;
    static bool runAsServer;
    static bool isFixedMessage;
    static int fixedMessageSize;
    static MessageEnvelope fixedMessage;

    static bool runAsClient;
    static int numClients;
    static int numMessages;
    static int messageSize;
    static MessageEnvelope clientMessage;

    private static List<ProtoClient> clients = new List<ProtoClient>();
    private static ProtoServer server;
    private static Stopwatch sw2 = new Stopwatch();
    private static long totMsgClient;
    private static long totMsgServer;
    private static long lastTimeStamp = 1;

    private static ThreadLocal<long> TotalNumMsgClients = new ThreadLocal<long>(true);
    private static ThreadLocal<long> TotalNumMsgServer = new ThreadLocal<long>(true);
    static void Main(string[] args)
    {
        //TcpTest();
        //TcpTest2();
        var config = ConsoleInputHandler.ObtainConfig();
        runAsClient = config.runAsClient;
        runAsServer = config.runAsServer;
        isFixedMessage = config.isFixedMessage;
        fixedMessageSize = config.fixedMessageSize;
        numClients = config.numClients;
        numMessages = config.numMessages;
        messageSize = config.messageSize;

        Prepare();
        if (runAsClient) Benchmark();

        ShowStatus();
        Console.ReadLine();
    }
    private static void InitializeServer()
    {
        fixedMessage = isFixedMessage ? new MessageEnvelope()
        {
            Header = "Test",
            Payload = new byte[fixedMessageSize]

        } : new MessageEnvelope();

        server = new ProtoServer(port);
        server.OnMessageReceived += isFixedMessage ? EchoStatic : EchoDynamic;
        Console.WriteLine("Server Running");

    }

    static void EchoDynamic(in Guid arg1, MessageEnvelope arg2)
    {
        server.SendAsyncMessage(in arg1, arg2);
    }
    static void EchoStatic(in Guid arg1, MessageEnvelope arg2)
    {
        server.SendAsyncMessage(in arg1, fixedMessage);
    }

    private static void InitializeClients()
    {

        clientMessage = new MessageEnvelope()
        {
            Header = "Test",
            Payload = new byte[messageSize]

        };
        clients = new List<ProtoClient>();

        for (int i = 0; i < numClients; i++)
        {
            var client = new ProtoClient();
            client.OnMessageReceived += (reply) => client.SendAsyncMessage(reply);
            clients.Add(client);

        }

        Console.WriteLine("Clients Created");
        Console.WriteLine("Press Enter To Connect");
        Console.ReadLine();

        Task[] toWait = new Task[numClients];
        int j = 0;
        foreach (var client1 in clients)
        {
            client1.Connect("127.0.0.1", port);
            j++;
        }
        Console.WriteLine("All Clients Connected");

    }


    private static void Prepare()
    {
        MiniLogger.AllLog += (string log) => Console.WriteLine(log);

        if (runAsServer)
        {
            InitializeServer();
        }
        if (runAsClient)
        {
            InitializeClients();
        }
    }
    private static void Benchmark()
    {
        Console.WriteLine("Press Enter To Benchmark");
        Console.ReadLine();
        sw2.Start();

        Parallel.ForEach(clients, client =>
        {
            for (int i = 0; i < numMessages; i++)
            {
                client.SendAsyncMessage(clientMessage);
            }

        });
    }

    private static void ShowStatus()
    {
        while (Console.ReadLine() != "e")
        {
            if (runAsServer)
            {
                GC.Collect();

                server.GetStatistics(out TcpStatistics general, out var _);
                Console.WriteLine("-> Server Statistics Snapshot:");
                Console.WriteLine(general.ToString());

            }

            if (runAsClient)
            {
                totMsgClient = 0;
                var stats = new List<TcpStatistics>();
                foreach (var client in clients)
                {
                    client.GetStatistics(out TcpStatistics stat);
                    totMsgClient += stat.TotalMessageReceived;
                    stats.Add(stat);
                }

#if UseLocalCounter
                    totMsgClient = TotalNumMsgClients.Values.Sum();
#endif

                //Console.WriteLine("-> Total Messages on clients: " + totMsgClient);

                lastTimeStamp = sw2.ElapsedMilliseconds;
                //Console.WriteLine("Elapsed " + lastTimeStamp);
                var elapsedSeconds = (double)lastTimeStamp / 1000;
                var messagePerSecond = totMsgClient / elapsedSeconds;

                Console.WriteLine("-> Client Statistics Snapshot: ");
                Console.WriteLine(TcpStatistics.GetAverageStatistics(stats).ToString());
                Console.WriteLine("# Average Request-Response Per second " + (totMsgClient / elapsedSeconds).ToString("N1"));
                Console.WriteLine("Press Enter to Refresh Statistics...");

            }
        }
        if (runAsClient)
        {
            foreach (var client1 in clients)
            {
                client1.Disconnect();
            }
            Thread.Sleep(1000);
        }

    }

}
#region Old
//internal class Program1
//{
//    static void Main(string[] args)
//    {
//        //BurstBench();
//        //EchoBench();
//        SecureProtoBench();
//    }

//    private static void EchoBench()
//    {
//        long totMsgCl = 0;
//        long totMsgsw = 0;
//        Stopwatch sw = new Stopwatch();
//        MessageEnvelope msg = new MessageEnvelope()
//        {
//            Header = "Test",
//            Payload = new byte[32]

//        };

//        MessageEnvelope end = new MessageEnvelope()
//        {
//            Header = "Stop"
//        };
//        ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();



//        var server = new ProtoServer(20008, 100);
//        server.OnMessageReceived += ServerStsReceived;

//        var clients = new List<ProtoClient>();
//        for (int i = 0; i < 100; i++)
//        {
//            var client = new ProtoClient();
//            client.OnMessageReceived += (reply) => ClientStsReceived(client, reply);

//            client.Connect("127.0.0.1", 20008);
//            clients.Add(client);
//        }

//        Task.Run(async () =>
//        {
//            long elapsedPrev = sw.ElapsedMilliseconds;
//            long totMsgClPrev = 0;
//            long totMsgswPrev = 0;
//            while (true)
//            {
//                await Task.Delay(2000);
//                long elapsedCurr = sw.ElapsedMilliseconds;
//                int deltaT = (int)(elapsedCurr - elapsedPrev);
//                Console.WriteLine("Elapsed: " + elapsedCurr);
//                Console.WriteLine("client total message {0} server total message {1}", totMsgCl, totMsgsw);
//                float throughput = (totMsgCl - totMsgClPrev + totMsgsw - totMsgswPrev) / (deltaT / 1000);

//                Console.WriteLine("Throughput :" + throughput.ToString("N1"));
//                elapsedPrev = elapsedCurr;
//                totMsgswPrev = totMsgsw;
//                totMsgClPrev = totMsgCl;

//            }

//        });
//        sw.Start();
//        Parallel.ForEach(clients, client =>
//        {
//            for (int i = 0; i < 1000; i++)
//            {
//                client.SendAsyncMessage(msg);
//            }
//            //client.SendStatusMessage(end);

//        });

//        //var resp = clients[0].SendMessageAndWaitResponse(msg).Result;
//        while (Console.ReadLine() != "e")
//        {
//            Console.WriteLine("client {0} server {1}", totMsgCl, totMsgsw);
//        }
//        Console.ReadLine();


//        void ServerStsReceived(in Guid arg1, MessageEnvelope arg2)
//        {
//            Interlocked.Increment(ref totMsgsw);
//            server.SendAsyncMessage(in arg1, arg2);
//        }

//        void ClientStsReceived(ProtoClient client, MessageEnvelope reply)
//        {
//            Interlocked.Increment(ref totMsgCl);
//            client.SendAsyncMessage(reply);

//            if (reply.Header.Equals("Stop"))
//            {
//                Console.WriteLine(sw.ElapsedMilliseconds);
//            }
//        }


//    }

//    static void BurstBench()
//    {
//        int totMsgCl = 0;
//        int totMsgsw = 0;
//        Stopwatch sw = new Stopwatch();
//        MessageEnvelope msg = new MessageEnvelope()
//        {
//            Header = "Test",
//            Payload = new byte[32]

//        };

//        MessageEnvelope end = new MessageEnvelope()
//        {
//            Header = "Stop"
//        };

//        ProtoServer server = new ProtoServer(20008, 100);
//        server.OnMessageReceived += ServerStsReceived;

//        var clients = new List<ProtoClient>();
//        for (int i = 0; i < 100; i++)
//        {
//            var client = new ProtoClient();
//            client.OnMessageReceived += (reply) => ClientStsReceived(client, reply);

//            client.Connect("127.0.0.1", 20008);
//            clients.Add(client);
//        }

//        sw.Start();
//        Parallel.ForEach(clients, client =>
//        {
//            for (int i = 0; i < 100000; i++)
//            {
//                client.SendAsyncMessage(msg);
//            }
//            client.SendAsyncMessage(end);

//        });

//        //var resp = clients[0].SendMessageAndWaitResponse(msg).Result;
//        while (Console.ReadLine() != "e")
//        {
//            Console.WriteLine("client {0} server {1}", totMsgCl, totMsgsw);
//        }
//        Console.ReadLine();


//        void ServerStsReceived(in Guid arg1, MessageEnvelope arg2)
//        {
//            Interlocked.Increment(ref totMsgsw);
//            server.SendAsyncMessage(arg1, arg2);
//        }

//        void ClientStsReceived(ProtoClient client, MessageEnvelope reply)
//        {
//            Interlocked.Increment(ref totMsgCl);
//            if (reply.Header.Equals("Stop"))
//            {
//                Console.WriteLine(sw.ElapsedMilliseconds);
//            }
//        }


//    }
//    private static void SecureProtoBench()
//    {
//        int totMsgCl = 0;
//        int totMsgsw = 0;
//        Stopwatch sw = new Stopwatch();
//        MessageEnvelope msg = new MessageEnvelope()
//        {
//            Header = "Test",
//            Payload = new byte[32]

//        };

//        MessageEnvelope end = new MessageEnvelope()
//        {
//            Header = "Stop"
//        };
//        ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();

//        var scert = new X509Certificate2("server.pfx", "greenpass");
//        var cert = new X509Certificate2("client.pfx", "greenpass");

//        SecureProtoServer server = new SecureProtoServer(20008, 100,scert);
//        server.OnMessageReceived += ServerStsReceived;

//        var clients = new List<ProtoClient>();
//        for (int i = 0; i < 100; i++)
//        {
//            var client = new ProtoClient(cert);
//            client.OnMessageReceived += (reply) => ClientStsReceived(client, reply);

//            client.Connect("127.0.0.1", 20008);
//            clients.Add(client);
//        }

//        Task.Run(async () =>
//        {
//            long elapsedPrev = sw.ElapsedMilliseconds;
//            int totMsgClPrev = 0;
//            int totMsgswPrev = 0;
//            while (true)
//            {
//                await Task.Delay(2000);
//                long elapsedCurr = sw.ElapsedMilliseconds;
//                int deltaT = (int)(elapsedCurr - elapsedPrev);
//                Console.WriteLine("Elapsed: " + elapsedCurr);
//                Console.WriteLine("client total message {0} server total message {1}", totMsgCl, totMsgsw);
//                float throughput = (totMsgCl - totMsgClPrev + totMsgsw - totMsgswPrev) / (deltaT / 1000);

//                Console.WriteLine("Throughput :" + throughput.ToString("N1"));
//                elapsedPrev = elapsedCurr;
//                totMsgswPrev = totMsgsw;
//                totMsgClPrev = totMsgCl;

//            }

//        });
//        sw.Start();
//        Parallel.ForEach(clients, client =>
//        {
//            for (int i = 0; i < 1000; i++)
//            {
//                client.SendAsyncMessage(msg);
//            }
//            //client.SendStatusMessage(end);

//        });

//        //var resp = clients[0].SendMessageAndWaitResponse(msg).Result;
//        while (Console.ReadLine() != "e")
//        {
//            Console.WriteLine("client {0} server {1}", totMsgCl, totMsgsw);
//        }
//        Console.ReadLine();


//        void ServerStsReceived(in Guid arg1, MessageEnvelope arg2)
//        {
//            Interlocked.Increment(ref totMsgsw);
//            server.SendAsyncMessage(in arg1, arg2);
//        }

//        void ClientStsReceived(ProtoClient client, MessageEnvelope reply)
//        {
//            Interlocked.Increment(ref totMsgCl);
//            client.SendAsyncMessage(reply);

//            if (reply.Header.Equals("Stop"))
//            {
//                Console.WriteLine(sw.ElapsedMilliseconds);
//            }
//        }


//    }
//}
#endregion



