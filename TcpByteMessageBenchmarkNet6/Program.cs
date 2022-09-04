using CustomNetworkLib;
using CustomNetworkLib.SocketEventArgsTests;
using NetworkSystem;
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
        //----------TCP --------------------------------------------------------
        private static void TcpTest()
        {
             
            ByteMessageTcpServer server = new ByteMessageTcpServer(2008);
            List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();

            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);

            int totMsgClient = 0;
            int totMsgServer = 0;
            int lastTimeStamp =1;

            var message = new byte[32];
            var response = new byte[32];

            server.MaxIndexedMemoryPerClient = 12800000;
            server.DropOnBackPressure = false;
            server.OnBytesRecieved += OnServerReceviedMessage;
            server.StartServer();

            int clientAmount = 10000;
            BufferManager.InitContigiousSendBuffers(clientAmount * 2, 19800);
            BufferManager.InitContigiousReceiveBuffers(clientAmount * 2, 19800);

            Task[] toWait = new Task[clientAmount];
            for (int i = 0; i < clientAmount; i++)
            {
                var client = new ByteMessageTcpClient();
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;

                client.DropOnCongestion = false;
                client.OnBytesRecieved += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", 2008);
                Console.WriteLine(server.Sessions.Count);

                clients.Add(client);
            }

            Task.WaitAll(toWait);
            // -----------------------  Bechmark ---------------------------
            Console.WriteLine("Press any key to start");
            Console.Read();
            sw2.Start();

            const int numMsg = 1;
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

            //------------------- 100m message 100 client 8.3 second
            Console.WriteLine("All messages are dispatched in :"+ sw2.ElapsedMilliseconds+ 
                "ms. Press enter to see status");
            Console.ReadLine();

            Console.WriteLine("Press E to Exit");
            while (Console.ReadLine() != "e")
            {
                lastTimeStamp = 10803;
                totMsgClient = 100010000;
                totMsgServer = 100010000;

                Console.WriteLine("Press E to Exit");

                Console.WriteLine("Total Messages on server: " + totMsgServer);
                Console.WriteLine("Total Messages on clients: " + totMsgClient);
                Console.WriteLine("Last Timestamp " + lastTimeStamp);
                var elapsedSeconds = ((float)lastTimeStamp / 1000);
                var messagePerSecond = totMsgClient / elapsedSeconds;

                Console.WriteLine(" Request-Response Per second " + totMsgClient/elapsedSeconds);
                Console.WriteLine("Data transmissıon rate Inbound " + (message.Length*messagePerSecond) / 1000000+ " Megabytes/s");
                Console.WriteLine("Data transmissıon rate Outbound " + (response.Length*messagePerSecond) / 1000000+ " Megabytes/s");
                //Console.WriteLine("Elapsed total MS " + sw2.ElapsedMilliseconds);
            }

            
            void OnClientReceivedMessage(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgClient);
                //client.SendAsync(response);
                if(count == 502)
                {
                    lastTimeStamp = (int)sw2.ElapsedMilliseconds;
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
                server.SendBytesToClient(id, response);
            }

        }

        


    }
}

