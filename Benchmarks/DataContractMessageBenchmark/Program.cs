using DataContractNetwork;
using DataContractNetwork.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.Utils;
using System.Diagnostics;

namespace DataContractMessageBenchmark
{
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

        private static List<DataContractMessageClient> clients = new List<DataContractMessageClient>();
        private static DataContractMessageServer server;
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
                Payload = new byte[fixedMessageSize],
                From = Guid.NewGuid(),
                To = Guid.NewGuid(),

            } : new MessageEnvelope();

            server = new DataContractMessageServer(port);
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
                Payload = new byte[messageSize],
                From = Guid.NewGuid(),
                To = Guid.NewGuid(),

            };
            clients = new List<DataContractMessageClient>();

            for (int i = 0; i < numClients; i++)
            {
                var client = new DataContractMessageClient();
                client.OnMessageReceived += (reply)
                    => client.SendAsyncMessage(reply);
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
}