using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.Utils;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

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

        private static List<SslByteMessageClient> clients = new List<SslByteMessageClient>();
        private static SslByteMessageServer server;
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
                    client.SendAsync(clientMessage);
                }

            });
        }
        static bool ValidateCertAsClient(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
        private static void InitializeClients()
        {
            clientMessage = new byte[messageSize];
            clients = new List<SslByteMessageClient>();
            var ccert = new X509Certificate2("client.pfx", "greenpass");

            for (int i = 0; i < numClients; i++)
            {
                var client = new SslByteMessageClient(ccert);
                client.GatherConfig = ScatterGatherConfig.UseQueue;
                client.MaxIndexedMemory = 128000000;

                client.RemoteCertificateValidationCallback
                    += ValidateCertAsClient;

                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => Echo(client, arg2, offset, count);
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

        private static void Echo(SslByteMessageClient client, byte[] arg2, int offset, int count)
        {
#if UseLocalCounter
            TotalNumMsgClients.Value++;
#endif
            client.SendAsync(clientMessage);
        }

        private static void InitializeServer()
        {
            fixedMessage = isFixedMessage ? new byte[fixedMessageSize] : new byte[0];
            var scert = new X509Certificate2("server.pfx", "greenpass");

            server = new SslByteMessageServer(port, scert);
            server.RemoteCertificateValidationCallback
                += ValidateCertAsClient;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.ClientSendBufsize = 128000;
            server.ClientReceiveBufsize = 128000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += isFixedMessage ? EchoStatic : EchoDynamic;

            server.GatherConfig = isFixedMessage ? ScatterGatherConfig.UseQueue : ScatterGatherConfig.UseBuffer;
            server.StartServer();

            Console.WriteLine("Server Running");

        }
        static void EchoDynamic(in Guid id, byte[] arg2, int offset, int count)
        {
            server.SendBytesToClient(id, arg2, offset, count);
        }
        static void EchoStatic(in Guid id, byte[] arg2, int offset, int count)
        {
            server.SendBytesToClient(id, fixedMessage);
        }

        private static void ShowStatus()
        {
            while (Console.ReadLine() != "e")
            {
                if (runAsServer)
                {
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
                    Console.WriteLine("# Average Data transmissıon rate Inbound " + ((clientMessage.Length + 4) * messagePerSecond / 1024000).ToString("N1") + " Megabytes/s");
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
            if (runAsServer)
            {
                server.ShutdownServer();
                Thread.Sleep(1000);

            }

        }

    }

}

#region Old
//using NetworkLibrary.TCP.ByteMessage;
//using NetworkLibrary.Utils;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using NetworkLibrary.TCP.SSL.ByteMessage;
//using System.Security.Cryptography.X509Certificates;
//using System.Net.Security;
//using NetworkLibrary.TCP;
//using NetworkLibrary;
//using NetworkLibrary.Components.Statistics;

//namespace SslBenchmark
//{

//    internal class Program
//    {

//        static void Main(string[] args)
//        {
//            Bench();

//        }
//        private static void Bench()
//        {
//            BufferPool.ForceGCOnCleanup = true;
//            Random rng = new Random(42);
//            MiniLogger.AllLog += (log) => Console.WriteLine(log);
//            ThreadLocal<long> countsClient = new ThreadLocal<long>(true);

//            int NumFinishedClients = 0;
//            long totMsgClient = 0;
//            int totMsgServer = 0;
//            int lastTimeStamp = 1;
//            int clientAmount = 100;
//            const int numMsg = 1000;
//            var message = new byte[32];
//            var response = new byte[32];

//            var scert = new X509Certificate2("server.pfx", "greenpass");
//            var ccert = new X509Certificate2("client.pfx", "greenpass");

//            SslByteMessageServer server = new SslByteMessageServer(2008, scert);
//            List<SslByteMessageClient> clients = new List<SslByteMessageClient>();

//            Stopwatch sw2 = new Stopwatch();
//            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);



//            server.MaxIndexedMemoryPerClient = 128000000;
//            server.DropOnBackPressure = false;
//            server.OnBytesReceived += OnServerReceviedMessage;
//            server.RemoteCertificateValidationCallback += ValidateCertAsServer;
//            server.GatherConfig = ScatterGatherConfig.UseQueue;
//            Console.Read();
//            server.StartServer();
//            Console.Read();
//            Task[] toWait = new Task[clientAmount];
//            for (int i = 0; i < clientAmount; i++)
//            {
//                var client = new SslByteMessageClient(ccert);
//                client.RemoteCertificateValidationCallback += ValidateCertAsClient;
//                client.GatherConfig= ScatterGatherConfig.UseQueue;
//                client.OnBytesReceived += (buffer, offset, count) => OnClientReceivedMessage(client, buffer, offset, count);
//                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
//                client.Connect("127.0.0.1", 2008);
//                clients.Add(client);
//            }

//            bool ValidateCertAsClient(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
//            {
//                return true;
//            }
//            bool ValidateCertAsServer(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
//            {
//                return true;
//            }
//            // -----------------------  Bechmark ---------------------------
//            Console.WriteLine("Press enter to start");
//            Console.ReadLine();
//            sw2.Start();

//            Parallel.ForEach(clients, client =>
//            {
//                for (int i = 0; i < numMsg; i++)
//                {
//                    //message = new byte[32];
//                    client.SendAsync(message);
//                    //client.Disconnect();

//                }

//            });
//            // final msg to get the tıme elapsed.
//            foreach (var cl in clients)
//            {
//                cl.SendAsync(new byte[502]);
//            }

//            // -----------------  End of stress test ---------------------
//            Console.WriteLine("All messages are dispatched in :" + sw2.ElapsedMilliseconds +
//                "ms. Press enter to see status");
//            Console.ReadLine();

//            Console.WriteLine("Press e to Exit");
//            while (Console.ReadLine()!= "e")
//            {
//                ShowStatus();
//            }

//            void ShowStatus()
//            {
//                totMsgClient = countsClient.Values.Sum();
//                Console.WriteLine("Press E to Exit");

//                Console.WriteLine("Total Messages on server: " + totMsgServer);
//                Console.WriteLine("Total Messages on clients: " + totMsgClient);

//                lastTimeStamp = (int)sw2.ElapsedMilliseconds;
//                Console.WriteLine("Elapsed " + lastTimeStamp);

//                var elapsedSeconds = (float)lastTimeStamp / 1000;
//                var messagePerSecond = totMsgClient / elapsedSeconds;

//                Console.WriteLine(" Request-Response Per second " + totMsgClient / elapsedSeconds);
//                Console.WriteLine("Data transmissıon rate Inbound " + message.Length * messagePerSecond / 1000000 + " Megabytes/s");
//                Console.WriteLine("Data transmissıon rate Outbound " + response.Length * messagePerSecond / 1000000 + " Megabytes/s");
//                server.GetStatistics(out TcpStatistics general, out var _);
//                Console.WriteLine(general.ToString());
//            }

//            void OnClientReceivedMessage(SslByteMessageClient client, byte[] arg2, int offset, int count)
//            {
//                countsClient.Value++;
//               // Interlocked.Increment(ref totMsgClient);
//                client.SendAsync(message);
//                //if (count == 502)
//                //{
//                //    lastTimeStamp = (int)sw2.ElapsedMilliseconds;
//                //    Interlocked.Increment(ref NumFinishedClients);
//                //    return;
//                //    if(Volatile.Read(ref NumFinishedClients) == clientAmount)
//                //    {
//                //        Console.WriteLine("--- All Clients are finished receiving response --- \n");
//                //        ShowStatus();
//                //        sw2.Stop();
//                //        Console.WriteLine("\n--- All Clients are finished receiving response --- \n");

//                //    }
//                //}
//            }

//            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
//            {
//                //Interlocked.Increment(ref totMsgServer);
//                //if (count == 502)
//                //{
//                //    server.SendBytesToClient(in id, new byte[502]);
//                //    return;
//                //}
//                // response = new byte[32000];
//                server.SendBytesToClient(id, response);
//            }

//        }


//    }
//}

//#define UseLocalCounter
#endregion



