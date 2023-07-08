#define UseLocalCounter
using NetworkLibrary.UDP.Reliable;
using NetworkLibrary.Utils;
using System.Diagnostics;
using System.Net;
using TcpMessageBenchmark;

namespace ConsoleTest
{

    internal class Program
    {
        static int port = 20007;
        static bool runAsServer;
        static bool isFixedMessage;
        static int fixedMessageSize;
        static byte[] fixedMessage;

        static bool runAsClient;
        static int numClients;
        static int numMessages;
        static int messageSize;
        static byte[] clientMessage;

        private static List<RudpClient> clients = new List<RudpClient>();
        private static RudpServer server;
        private static Stopwatch sw2 = new Stopwatch();
        private static long totMsgClient;
        private static long totMsgServer;
        private static long lastTimeStamp = 1;

        private static ThreadLocal<long> TotalNumMsgClients = new ThreadLocal<long>(true);
        private static ThreadLocal<long> TotalNumMsgServer = new ThreadLocal<long>(true);
        static void Main(string[] args)
        {
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
            Environment.Exit(0);
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
                    client.Send(clientMessage, 0, clientMessage.Length);
                }

            });
        }

        private static void InitializeClients()
        {
            clientMessage = new byte[messageSize];
            clients = new List<RudpClient>();
            for (int i = 0; i < numClients; i++)
            {
                var client = new RudpClient();

                client.OnReceived += (byte[] arg2, int offset, int count) => EchoClient(client, arg2, offset, count);
                clients.Add(client);

            }

            Console.WriteLine("Clients Created");
            Console.WriteLine("Press Enter To Connect");
            Console.ReadLine();

            Task[] toWait = new Task[numClients];
            int j = 0;
            foreach (var client1 in clients)
            {
                toWait[j] = client1.ConnectAsync("127.0.0.1", port);
                j++;
            }
            Task.WaitAll(toWait);
            Console.WriteLine("All Clients Connected");

        }
        static int clientReciveLen = 0;
        private static void EchoClient(RudpClient client, byte[] arg2, int offset, int count)
        {
#if UseLocalCounter
            TotalNumMsgClients.Value++;
            clientReciveLen = count;
#endif
            client.Send(clientMessage, 0, clientMessage.Length);
        }

        private static void InitializeServer()
        {
            fixedMessage = isFixedMessage ? new byte[fixedMessageSize] : new byte[0];

            server = new RudpServer(port);

            server.OnReceived += isFixedMessage ? EchoServerFixed : EchoServerDynamic;

            server.StartServer();

            Console.WriteLine("Server Running");

        }
        static int serverReceiveLen = 0;
        static void EchoServerDynamic(IPEndPoint ep, byte[] arg2, int offset, int count)
        {
            server.Send(ep, arg2, offset, count);
            serverReceiveLen = count;

        }
        static void EchoServerFixed(IPEndPoint ep, byte[] arg2, int offset, int count)
        {
            server.Send(ep, fixedMessage, 0, fixedMessage.Length);
            serverReceiveLen = count;

        }
        static long totPrev = 0;
        private static void ShowStatus()
        {
            while (Console.ReadLine() != "e")
            {

#if UseLocalCounter
                var t = TotalNumMsgClients.Values.Sum();
                totMsgClient = t - totPrev;
                totPrev = t;
                Console.WriteLine("total delta Sum = " + totMsgClient);
#endif
                double elapsedSeconds = (sw2.ElapsedMilliseconds - lastTimeStamp) / 1000d;
                Console.WriteLine(elapsedSeconds);
                var messagePerSecond = totMsgClient / (elapsedSeconds);
                lastTimeStamp = sw2.ElapsedMilliseconds;

                if (runAsServer)
                {
                    Console.WriteLine("# Request-Response Per second " + messagePerSecond.ToString("N1"));
                    Console.WriteLine("# Data transmission rate Server " + ((((serverReceiveLen) * messagePerSecond) / (1024 * 1024))).ToString("N1") + " Megabytes/s");
                }

                if (runAsClient)
                {

                    Console.WriteLine("# Request-Response Per second " + messagePerSecond.ToString("N1"));
                    Console.WriteLine("# Data transmissıon rate Client " + ((((clientReciveLen) * messagePerSecond) / (1024 * 1024))).ToString("N1") + " Megabytes/s");
                }
                Console.WriteLine("Press Enter to Refresh Statistics...");

            }

            if (runAsClient)
            {
                foreach (var client1 in clients)
                {
                    client1.Disconnect();
                }
                Thread.Sleep(1000);
            }
            if (runAsServer)
            {
                //server.ShutdownServer();
                Thread.Sleep(1000);

            }

        }


    }

}

