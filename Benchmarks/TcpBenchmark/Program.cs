//#define UseLocalCounter
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

        private static List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();
        private static ByteMessageTcpServer server;
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

        private static void InitializeClients()
        {
            clientMessage = new byte[messageSize];
            clients = new List<ByteMessageTcpClient>();
            for (int i = 0; i < numClients; i++)
            {
                var client = new ByteMessageTcpClient();
                client.GatherConfig = ScatterGatherConfig.UseQueue;
                client.MaxIndexedMemory = 128000000;

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
                toWait[j] = client1.ConnectAsyncAwaitable("127.0.0.1", port);
                j++;
            }
            Task.WaitAll(toWait);
            Console.WriteLine("All Clients Connected");
            Console.WriteLine(server?.SessionCount);

        }

        private static void Echo(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
        {
#if UseLocalCounter
            TotalNumMsgClients.Value++;
#endif
            client.SendAsync(clientMessage);
        }

        private static void InitializeServer()
        {
            fixedMessage = isFixedMessage ? new byte[fixedMessageSize] : new byte[0];

            server = new ByteMessageTcpServer(port);
            server.MaxIndexedMemoryPerClient = 128000000;
            server.ClientSendBufsize = 128000;
            server.ClientReceiveBufsize = 128000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += isFixedMessage ? EchoStatic : EchoDynamic;

            server.GatherConfig = isFixedMessage ? ScatterGatherConfig.UseQueue : ScatterGatherConfig.UseBuffer;
            server.StartServer();

            Console.WriteLine("Server Running");

        }
        static void EchoDynamic(Guid id, byte[] arg2, int offset, int count)
        {
            server.SendBytesToClient(id, arg2, offset, count);
        }
        static void EchoStatic(Guid id, byte[] arg2, int offset, int count)
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
                    List<TcpStatistics> stats = new List<TcpStatistics>();
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
        #region Old

        //private static void TcpTest()
        //{
        //    BufferPool.ForceGCOnCleanup = false;
        //    // dont change.
        //    int NumFinishedClients = 0;
        //    //CoreAssemblyConfig.UseUnmanaged=true;
        //    MiniLogger.AllLog += (string log) => Console.WriteLine(log);

        //    int totMsgClient = 0;
        //    int totMsgServer = 0;
        //    int lastTimeStamp = 1;
        //    int clientAmount = 100;
        //    const int numMsg = 1000000;
        //    var message = new byte[32];
        //    var response = new byte[32];


        //    ByteMessageTcpServer server = new ByteMessageTcpServer(2008);
        //    List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();

        //    Stopwatch sw2 = new Stopwatch();
        //    AutoResetEvent testCompletionEvent = new AutoResetEvent(false);

        //    server.MaxIndexedMemoryPerClient = 12800000;
        //    server.ClientSendBufsize = 128000;
        //    server.ClientReceiveBufsize = 128000;
        //    server.DropOnBackPressure = false;
        //    server.OnBytesReceived += OnServerReceviedMessage;
        //    server.StartServer();

        //    server.GatherConfig = ScatterGatherConfig.UseQueue;

        //    Task[] toWait = new Task[clientAmount];
        //    for (int i = 0; i < clientAmount; i++)
        //    {
        //        var client = new ByteMessageTcpClient();
        //        client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
        //        client.GatherConfig = ScatterGatherConfig.UseQueue;
        //        client.DropOnCongestion = false;
        //        client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

        //        toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", 2008);
        //        clients.Add(client);
        //    }

        //    Task.WaitAll(toWait);

        //    // -----------------------  Bechmark ---------------------------
        //    Console.WriteLine("Press any key to start");
        //    Console.Read();
        //    sw2.Start();

        //    // We parallely send the messages here
        //    Parallel.ForEach(clients, client =>
        //    {
        //        for (int i = 0; i < numMsg; i++)
        //        {
        //            client.SendAsync(message);

        //        }

        //    });

        //    // final msg to get the time elapsed.
        //    foreach (var cl in clients)
        //    {
        //        cl.SendAsync(new byte[502]);
        //    }

        //    // -------- Messages are sent by clients ------ 

        //    Console.WriteLine("All messages are dispatched in :" + sw2.ElapsedMilliseconds +
        //        "ms. Press enter to see status");
        //    Console.ReadLine();

        //    Console.WriteLine("Press e to Exit");
        //    while (Console.ReadLine() != "e")
        //    {
        //        ShowStatus();
        //    }
        //    server.ShutdownServer();
        //    foreach (var client1 in clients)
        //    {
        //        client1.Disconnect();
        //    }
        //    //----- End --- 
        //    void ShowStatus()
        //    {
        //        Console.WriteLine("\nPress E to Exit\n");

        //        Console.WriteLine("Total Messages on server: " + totMsgServer);
        //        Console.WriteLine("Total Messages on clients: " + totMsgClient);

        //        lastTimeStamp = (int)sw2.ElapsedMilliseconds;
        //        Console.WriteLine("Elapsed " + lastTimeStamp);

        //        var elapsedSeconds = (float)lastTimeStamp / 1000;
        //        var messagePerSecond = totMsgClient / elapsedSeconds;

        //        Console.WriteLine(" Request-Response Per second " + totMsgClient / elapsedSeconds);
        //        Console.WriteLine("Data transmissıon rate Inbound " + message.Length * messagePerSecond / 1000000 + " Megabytes/s");
        //        Console.WriteLine("Data transmissıon rate Outbound " + response.Length * messagePerSecond / 1000000 + " Megabytes/s");
        //    }

        //    void OnClientReceivedMessage(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
        //    {
        //        Interlocked.Increment(ref totMsgClient);

        //        if (count == 502)
        //        {
        //            lastTimeStamp = (int)sw2.ElapsedMilliseconds;
        //            Interlocked.Increment(ref NumFinishedClients);

        //            if (Volatile.Read(ref NumFinishedClients) == clientAmount)
        //            {
        //                Console.WriteLine("\n--- All Clients are finished receiving response --- \n");
        //                ShowStatus();
        //                sw2.Stop();
        //                Console.WriteLine("\n--- All Clients are finished receiving response --- \n");

        //            }
        //        }

        //    }

        //    void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
        //    {
        //        Interlocked.Increment(ref totMsgServer);
        //        if (count == 502)
        //        {
        //            server.SendBytesToClient(in id, new byte[502]);
        //            return;
        //        }

        //        // response = new byte[32000];
        //        server.SendBytesToClient(in id, response);
        //    }

        //}

        //private static void TcpTest2()
        //{

        //    // dont change.
        //    int NumFinishedClients = 0;
        //    MiniLogger.AllLog += (string log) => Console.WriteLine(log);

        //    totMsgClient = 0;
        //    totMsgServer = 0;
        //    lastTimeStamp = 1;
        //    int clientAmount = 100;
        //    const int numMsg = 1000;
        //    var message = new byte[32];
        //    var response = new byte[32];

        //    bool done = false;
        //    int port = 20007;
        //    ByteMessageTcpServer server = new ByteMessageTcpServer(port);
        //    List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();

        //    sw2 = new Stopwatch();
        //    AutoResetEvent testCompletionEvent = new AutoResetEvent(false);
        //    ThreadLocal<long> countsClient = new ThreadLocal<long>(true);
        //    ThreadLocal<long> countsServer = new ThreadLocal<long>(true);
        //    server.MaxIndexedMemoryPerClient = 128000000;
        //    server.ClientSendBufsize = 128000;
        //    server.ClientReceiveBufsize = 128000;
        //    server.DropOnBackPressure = false;
        //    server.OnBytesReceived += OnServerReceviedMessage;
        //    server.StartServer();
        //    server.GatherConfig = ScatterGatherConfig.UseQueue;

        //    Task[] toWait = new Task[clientAmount];
        //    for (int i = 0; i < clientAmount; i++)
        //    {
        //        var client = new ByteMessageTcpClient();
        //        client.GatherConfig = ScatterGatherConfig.UseQueue;
        //        client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;

        //        client.DropOnCongestion = false;
        //        client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

        //        toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", port);
        //        clients.Add(client);
        //    }

        //    Task.WaitAll(toWait);

        //    // -----------------------  Bechmark ---------------------------
        //    Console.WriteLine("Press any key to start");
        //    Console.Read();

        //    // We parallely send the messages here
        //    sw2.Start();

        //    Parallel.ForEach(clients, client =>
        //    {
        //        for (int i = 0; i < numMsg; i++)
        //        {
        //            client.SendAsync(message);
        //        }

        //    });




        //    // -------- Messages are sent by clients ------ 

        //    Console.WriteLine("All messages are dispatched in :" + sw2.ElapsedMilliseconds +
        //        "ms. Press enter to see status");
        //    Console.ReadLine();

        //    Console.WriteLine("Press e to Exit");
        //    while (Console.ReadLine() != "e")
        //    {
        //        ShowStatus();
        //    }
        //    done = true;
        //    server.ShutdownServer();

        //    foreach (var client1 in clients)
        //    {
        //        client1.Disconnect();
        //    }


        //    //----- End --- 
        //    void ShowStatus()
        //    {
        //        server.GetStatistics(out TcpStatistics general, out var _);
        //        totMsgClient = 0;
        //        foreach (var client in clients)
        //        {
        //            client.GetStatistics(out TcpStatistics stat);
        //            totMsgClient += stat.TotalMessageReceived;
        //        }
        //        totMsgServer = general.TotalMessageReceived;
        //        //totMsgClient = countsClient.Values.Sum();
        //        //totMsgServer = countsServer.Values.Sum();
        //        Console.WriteLine("Press E to Exit");

        //        Console.WriteLine("Total Messages on server: " + totMsgServer);
        //        Console.WriteLine("Total Messages on clients: " + totMsgClient);
        //        //Console.WriteLine("Total Messages on clients2: " + countsClient.Values.Sum());

        //        lastTimeStamp = sw2.ElapsedMilliseconds;
        //        Console.WriteLine("Elapsed " + lastTimeStamp);

        //        var elapsedSeconds = (double)lastTimeStamp / 1000;
        //        var messagePerSecond = totMsgClient / elapsedSeconds;

        //        Console.WriteLine("Average Request-Response Per second " + (totMsgClient / elapsedSeconds).ToString("N1"));
        //        Console.WriteLine("Data transmissıon rate Inbound " + ((message.Length + 4) * messagePerSecond / 1024000).ToString("N1") + " Megabytes/s");
        //        Console.WriteLine("Data transmissıon rate Outbound " + ((response.Length + 4) * messagePerSecond / 1024000).ToString("N1") + " Megabytes/s");
        //        Console.WriteLine(general.ToString());
        //    }

        //    //--------------- Msg Handlers ---------------------

        //    void OnClientReceivedMessage(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
        //    {
        //        //countsClient.Value++;
        //        //Interlocked.Increment(ref totMsgClient);
        //        client.SendAsync(response);
        //        //if (count == 502)
        //        //{
        //        //    lastTimeStamp = (int)sw2.ElapsedMilliseconds;
        //        //    Interlocked.Increment(ref NumFinishedClients);

        //        //    if (Volatile.Read(ref NumFinishedClients) == clientAmount)
        //        //    {
        //        //        Console.WriteLine("\n--- All Clients are finished receiving response --- \n");
        //        //        ShowStatus();
        //        //        sw2.Stop();
        //        //        Console.WriteLine("\n--- All Clients are finished receiving response --- \n");

        //        //    }
        //        //}

        //    }

        //    void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
        //    {
        //        // countsServer.Value++;

        //        //Interlocked.Increment(ref totMsgServer);
        //        //if (count == 502)
        //        //{
        //        //    server.SendBytesToClient(id, new byte[502]);
        //        //    return;
        //        //}

        //        // response = new byte[32000];
        //        //server.SendBytesToClient(id, arg2,offset,count);
        //        server.SendBytesToClient(id, response);
        //    }
        //}
        #endregion

    }

}

