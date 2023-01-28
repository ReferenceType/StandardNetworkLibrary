using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary;
using NetworkLibrary.TCP;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class DisconnectAndDisposalTest
    {
        [TestMethod]
        public void SeriesOfDisconnectTest()
        {
            try
            {
                MiniLogger.AllLog += (log) => Console.WriteLine(log);
                for (int i = 0; i < 5; i++)
                {
                    DisconnectTest();
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.ToString());
                //Console.WriteLine(ex.StackTrace);
                //Debug.Assert(false);
            }
           
        }
        [TestMethod]
        public void SeriesOfDisconnectTestSSl()
        {
            MiniLogger.AllLog += (log) => Console.WriteLine(log);
            for (int i = 0; i < 2; i++)
            {
                DisconnectTestSSl();
            }
        }

        [TestMethod]
        public void DisconnectTest()
        {
            int totMsgCl = 0;
            int totMsgsw = 0;
            int totDisconnectRequest = 0;
            var msg = new byte[32];
            var response = new byte[32];

            const int numMsg = 100;
            int clAmount = 100;

            ByteMessageTcpServer server = new ByteMessageTcpServer(2008);
            List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);

            server.MaxIndexedMemoryPerClient = 1280000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;

            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new ByteMessageTcpClient();
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", 2008);

                clients.Add(client);
            }

            Task.WaitAll(toWait);

            // Messages starts here
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg; i++)
                {
                    client.SendAsync(msg);

                }
            });
            // Deploy timed disconnections.
            //foreach (var client in clients)
            Thread.Sleep(2000);
            Parallel.ForEach(clients, (client) =>
            {
                Interlocked.Increment(ref totDisconnectRequest);
                Console.WriteLine("-------------------            Disconnect Signalled By Client            --------------");
                client.Disconnect();
                if (Interlocked.CompareExchange(ref totDisconnectRequest, 0, 0) >= clAmount)
                {
                    client.OnDisconnected += () => testCompletionEvent.Set();
                }


            });



            void OnClientReceivedMessage(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                client.SendAsync(response);
            }

            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(12000);
            Thread.Sleep(100);

            long waitcount = 0;
            while (server.SessionCount != 0 && waitcount < 1000000)
            {
                waitcount++;
                Thread.SpinWait(100);
            }

            Console.WriteLine("shutting down");
            server.ShutdownServer();
            Thread.Sleep(100);

        }
        [TestMethod]

        public void DisconnectTestSSl()
        {
            int totMsgCl = 0;
            int totMsgsw = 0;
            int totDisconnectRequest = 0;
            var msg = new byte[32];
            var response = new byte[32];

            const int numMsg = 100;
            int clAmount = 100;
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var ccert = new X509Certificate2("client.pfx", "greenpass");

            SslByteMessageServer server = new SslByteMessageServer(2008,scert);
            List<SslByteMessageClient> clients = new List<SslByteMessageClient>();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);

            server.MaxIndexedMemoryPerClient = 1280000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;
            server.RemoteCertificateValidationCallback += (a,b,c,d)=> true; 

            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new SslByteMessageClient(ccert);
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);
                client.RemoteCertificateValidationCallback += (a, b, c, d) => true;

                // toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", 2008);
                client.Connect("127.0.0.1", 2008);
                clients.Add(client);
            }

           // Task.WaitAll(toWait);

            // Messages starts here
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg; i++)
                {
                    client.SendAsync(msg);

                }
            });
            // Deploy timed disconnections.
            //foreach (var client in clients)
            Thread.Sleep(2000);
            Parallel.ForEach(clients, (client) =>
            {
                Interlocked.Increment(ref totDisconnectRequest);
                Console.WriteLine("-------------------            Disconnect Signalled By Client            --------------");
                client.Disconnect();
                if (Interlocked.CompareExchange(ref totDisconnectRequest, 0, 0) >= clAmount)
                {
                    client.OnDisconnected += () => testCompletionEvent.Set();
                }


            });



            void OnClientReceivedMessage(SslByteMessageClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                client.SendAsync(response);
            }

            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(12000);
            Thread.Sleep(100);

            long waitcount = 0;
            while (server.SessionCount != 0 && waitcount < 1000000)
            {
                waitcount++;
                Thread.SpinWait(100);
            }

            Console.WriteLine("shutting down");
            server.ShutdownServer();
            Thread.Sleep(100);

        }


        [TestMethod]
        public void RandomConnectDisconnectTest()
        {
            MiniLogger.AllLog += (log) => Console.WriteLine(log);

            int totMsgCl = 0;
            int totMsgsw = 0;
            int totDisconnectRequest = 0;
            var msg = new byte[32];
            var response = new byte[32];

            const int numMsg = 10;
            int clAmount = 1000;

            ByteMessageTcpServer server = new ByteMessageTcpServer(2008);
            List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);

            server.MaxIndexedMemoryPerClient = 1280000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;

            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new ByteMessageTcpClient();
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", 2008);

                clients.Add(client);
            }

            Task.WaitAll(toWait);

            // Messages starts here
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg; i++)
                {
                    client.SendAsync(msg);

                }
            });
            // Deploy timed disconnections.
            //foreach (var client in clients)
            Thread.Sleep(2000);
            List<Task> pending  =  new List<Task>();
            Parallel.ForEach(clients, (client) =>
            {
                Interlocked.Increment(ref totDisconnectRequest);
                Console.WriteLine("-------------------            Disconnect Signalled By Client            --------------");
                client.Disconnect();
                client = new ByteMessageTcpClient();
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                pending.Add(client.ConnectAsyncAwaitable("127.0.0.1", 2008).ContinueWith((c) => { client.Disconnect(); }) );

                if (Interlocked.CompareExchange(ref totDisconnectRequest, 0, 0) >= clAmount)
                {
                    client.OnDisconnected += async()  => { await Task.WhenAll(pending); testCompletionEvent.Set(); };
                }


            });



            void OnClientReceivedMessage(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                client.SendAsync(response);
            }

            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(12000);
            Console.WriteLine("££££££££££££££££££   Completed £££££££££££££££££££££");
            Thread.Sleep(100);

            long waitcount = 0;
            while (server.SessionCount != 0 && waitcount < 1000000)
            {
                waitcount++;
                Thread.SpinWait(100);
            }
            Assert.IsTrue(server.SessionCount == 0);
            Console.WriteLine("shutting down");
            server.ShutdownServer();
            Thread.Sleep(100);
        }

    }
}
