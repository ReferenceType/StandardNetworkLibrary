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
using NetworkLibrary.TCP.SSL.ByteMessage;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace SslBenchmark
{

    internal class Program
    {
        static void Main(string[] args)
        {
            Bench();

        }
        private static void Bench()
        {
            MiniLogger.AllLog += (log) => Console.WriteLine(log);

            int NumFinishedClients = 0;
            int totMsgClient = 0;
            int totMsgServer = 0;
            int lastTimeStamp = 1;
            int clientAmount = 100;
            const int numMsg = 10000;
            var message = new byte[3200];
            var response = new byte[3200];

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var ccert = new X509Certificate2("client.pfx", "greenpass");

            SslByteMessageServer server = new SslByteMessageServer(2008, clientAmount * 2, scert);
            List<SslByteMessageClient> clients = new List<SslByteMessageClient>();

            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);

            

            server.MaxIndexedMemoryPerClient = 1280000000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;
            server.RemoteCertificateValidationCallback += ValidateCertAsServer;
            server.StartServer();

            Task[] toWait = new Task[clientAmount];
            for (int i = 0; i < clientAmount; i++)
            {
                var client = new SslByteMessageClient(ccert);
                client.RemoteCertificateValidationCallback += ValidateCertAsClient;
                client.BufferProvider = server.BufferProvider;
                client.OnBytesReceived += (buffer, offset, count) => OnClientReceivedMessage(client, buffer, offset, count);
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.Connect("127.0.0.1", 2008);
                clients.Add(client);
            }

            bool ValidateCertAsClient(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
            {
                return true;
            }
            bool ValidateCertAsServer(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
            {
                return true;
            }
            // -----------------------  Bechmark ---------------------------
            Console.WriteLine("Press enter to start");
            Console.ReadLine();
            sw2.Start();

            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg; i++)
                {
                    //message = new byte[32000];
                    client.SendAsync(message);

                }

            });
            // final msg to get the tıme elapsed.
            foreach (var cl in clients)
            {
                cl.SendAsync(new byte[502]);
            }

            // -----------------  End of stress test ---------------------
            Console.WriteLine("All messages are dispatched in :" + sw2.ElapsedMilliseconds +
                "ms. Press enter to see status");
            Console.ReadLine();

            Console.WriteLine("Press e to Exit");
            while (Console.ReadLine()!= "e")
            {
                ShowStatus();
            }

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

            void OnClientReceivedMessage(SslByteMessageClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgClient);
                
                if (count == 502)
                {
                    lastTimeStamp = (int)sw2.ElapsedMilliseconds;
                    Interlocked.Increment(ref NumFinishedClients);

                    if(Volatile.Read(ref NumFinishedClients) == clientAmount)
                    {
                        Console.WriteLine("--- All Clients are finished receiving response --- \n");
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

