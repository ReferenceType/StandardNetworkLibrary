using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.Utils;
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


namespace ConsoleTest
{

    internal class Program
    {
        static void Main(string[] args)
        {
            TcpTest();

        }

        private static void TcpTest()
        {
            // dont change.
            int NumFinishedClients = 0;

            MiniLogger.AllLog+=(string log)=>Console.WriteLine(log);

            int totMsgClient = 0;
            int totMsgServer = 0;
            int lastTimeStamp = 1;
            int clientAmount = 100;
            const int numMsg = 1000000;


            ByteMessageTcpServer server = new ByteMessageTcpServer(2008, clientAmount*2);
            List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();

            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);

           

            var message = new byte[32];
            var response = new byte[32];

            server.MaxIndexedMemoryPerClient = 1280000000;
            server.ClientSendBufsize = 128000;
            server.ClientReceiveBufsize = 128000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;
            server.StartServer();

            Task[] toWait = new Task[clientAmount];
            for (int i = 0; i < clientAmount; i++)
            {
                var client = new ByteMessageTcpClient();
                client.BufferManager = server.BufferManager;
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;

                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", 2008);
                clients.Add(client);
            }

            Task.WaitAll(toWait);

            // -----------------------  Bechmark ---------------------------
            Console.WriteLine("Press any key to start");
            Console.Read();
            sw2.Start();

            // We parallely send the messages here
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg; i++)
                {
                    client.SendAsync(message);

                }
                
            });

            // final msg to get the tıme elapsed.
            foreach (var cl in clients)
            {
                cl.SendAsync(new byte[502]);
            }

            // -------- Messages are sent by clients ------ 

            Console.WriteLine("All messages are dispatched in :" + sw2.ElapsedMilliseconds +
                "ms. Press enter to see status");
            Console.ReadLine();

            Console.WriteLine("Press e to Exit");
            while (Console.ReadLine() != "e")
            {
                ShowStatus();
            }
            //----- End --- 
            void ShowStatus()
            {
                Console.WriteLine("Press E to Exit");

                Console.WriteLine("Total Messages on server: " + totMsgServer);
                Console.WriteLine("Total Messages on clients: " + totMsgClient);

                lastTimeStamp = (int)sw2.ElapsedMilliseconds;
                Console.WriteLine("Elapsed " + lastTimeStamp);

                var elapsedSeconds = (float)lastTimeStamp / 1000;
                var messagePerSecond = totMsgClient / elapsedSeconds;

                Console.WriteLine(" Request-Response Per second " + totMsgClient / elapsedSeconds);
                Console.WriteLine("Data transmissıon rate Inbound " + message.Length * messagePerSecond / 1000000 + " Megabytes/s");
                Console.WriteLine("Data transmissıon rate Outbound " + response.Length * messagePerSecond / 1000000 + " Megabytes/s");
            }

            void OnClientReceivedMessage(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgClient);

                if (count == 502)
                {
                    lastTimeStamp = (int)sw2.ElapsedMilliseconds;
                    Interlocked.Increment(ref NumFinishedClients);

                    if (Volatile.Read(ref NumFinishedClients) == clientAmount)
                    {
                        Console.WriteLine("\n--- All Clients are finished receiving response --- \n");
                        ShowStatus();
                        sw2.Stop();
                        Console.WriteLine("\n--- All Clients are finished receiving response --- \n");

                    }
                }
            }

            void OnServerReceviedMessage(Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgServer);
                if (count == 502)
                {
                    server.SendBytesToClient(id, new byte[502]);
                    return;
                }
                // response = new byte[32000];
                server.SendBytesToClient(id, response);
            }

        }

    }

}

