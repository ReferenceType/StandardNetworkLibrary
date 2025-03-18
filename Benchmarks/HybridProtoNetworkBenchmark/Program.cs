using NetworkLibrary;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.Utils;
using ProtoBuf;
using Protobuff;
using System.Diagnostics;

namespace HybridProtoNetworkBenchmark
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

        private static List<ProtoMessageClient> clients = new List<ProtoMessageClient>();
        private static ProtoMessageServer server;
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

            server = new ProtoMessageServer(port);
            server.OnMessageReceived += isFixedMessage ? EchoStatic : EchoDynamic;
            server.StartServer();
            Console.WriteLine("Server Running");

        }

        static void EchoDynamic(Guid arg1, MessageEnvelope arg2)
        {
            server.SendAsyncMessage(arg1, arg2);
        }
        static void EchoStatic(Guid arg1, MessageEnvelope arg2)
        {
            server.SendAsyncMessage(arg1, arg2);
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
            clients = new List<ProtoMessageClient>();

            for (int i = 0; i < numClients; i++)
            {
                var client = new ProtoMessageClient();
                client.OnMessageReceived += client.SendAsyncMessage;
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
            var large =  new LargeDataClass();
            sw2.Start();

            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMessages; i++)
                {
                    client.SendAsyncMessage(clientMessage,large);
                }

            });
        }
        [ProtoContract]
        public class LargeDataClass
        {
            [ProtoMember(1)]
            public int Id { get; set; }

            [ProtoMember(2)]
            public string Name { get; set; }

            [ProtoMember(3)]
            public double Value { get; set; }

            [ProtoMember(4)]
            public DateTime Timestamp { get; set; }

            [ProtoMember(5)]
            public NestedData Nested { get; set; }

            [ProtoMember(6)]
            public List<string> Tags { get; set; }

            [ProtoMember(7)]
            public Dictionary<string, int> Metadata { get; set; }

            [ProtoMember(8)]
            public bool IsActive { get; set; }

            [ProtoMember(9)]
            public byte[] RawData { get; set; }

            [ProtoMember(10)]
            public List<NestedData> NestedList { get; set; }

            public LargeDataClass()
            {
                // Initialize with some non-default values
                Id = 1001;
                Name = "Example Name";
                Value = 123.45;
                Timestamp = DateTime.UtcNow;
                Nested = new NestedData
                {
                    NestedId = 42,
                    Description = "Nested description",
                    Data = new byte[] { 1, 2, 3, 4 }
                };
                Tags = new List<string> { "tag1", "tag2", "tag3" };
                Metadata = new Dictionary<string, int>
        {
            { "key1", 1 },
            { "key2", 2 },
            { "key3", 3 }
        };
                IsActive = true;
                RawData = new byte[] { 10, 20, 30, 40 };
                NestedList = new List<NestedData>
        {
            new NestedData { NestedId = 101, Description = "First nested", Data = new byte[] { 5, 6, 7 } },
            new NestedData { NestedId = 102, Description = "Second nested", Data = new byte[] { 8, 9, 10 } }
        };
            }
        }

        [ProtoContract]
        public class NestedData
        {
            [ProtoMember(1)]
            public int NestedId { get; set; }

            [ProtoMember(2)]
            public string Description { get; set; }

            [ProtoMember(3)]
            public byte[] Data { get; set; }

            public NestedData()
            {
                // Initialize with non-default values
                NestedId = 0;
                Description = "Default description";
                Data = Array.Empty<byte>();
            }
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