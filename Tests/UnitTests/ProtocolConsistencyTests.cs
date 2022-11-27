using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Concurrent;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.Utils;
using System.Security.Cryptography.X509Certificates;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.TCP.SSL.Custom; 
using System.Net.Security;
using NetworkLibrary.TCP;

namespace UnitTests
{
    [TestClass]
    public class ProtocolConsistencyTests
    {
        #region TCP
        [TestMethod]
        // Send fix length many small messages and validate their order
        public void TCPMessageRushConsistencyTest()
        {
            const int numMsg = 100000;
            int clAmount = 100;
            int completionCount = clAmount;
            int numErrors = 0;
            ByteMessageTcpServer server = new ByteMessageTcpServer(2008);
            ConcurrentDictionary<ByteMessageTcpClient,int> clients = new ConcurrentDictionary<ByteMessageTcpClient,int>();

            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);
            int totMsgCl = 0;
            int totMsgsw = 0;

            server.MaxIndexedMemoryPerClient = 128000000;
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
                Console.WriteLine(server.Sessions.Count);

                clients.TryAdd(client,-1);
            }

            Task.WaitAll(toWait);


            sw2.Start();

            // Messages starts here
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg-1; i++)
                {
                    var msg = new byte[32];
                    PrefixWriter.WriteInt32AsBytes(msg, 0, i);
                    client.Key.SendAsync(msg);
                }
            }
            );
            // final message to mark ending.
            foreach (var client in clients)
            {
                client.Key.SendAsync(new byte[502]);
            }

            void OnClientReceivedMessage(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                if (count == 502)
                {
                    Interlocked.Decrement(ref completionCount);
                    if(Interlocked.CompareExchange(ref completionCount,0,0)==0)
                        testCompletionEvent.Set();
                    return;
                }

                int currVal = BitConverter.ToInt32(arg2, offset);
                var prevVal = clients[client];
                if (currVal != prevVal+1)
                {
                    Interlocked.Increment(ref numErrors);
                }
                clients[client] = currVal;
                //client.SendAsync(response);
            }

            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);
                var response= new byte[count];
                Buffer.BlockCopy(arg2,offset,response,0,count);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(50000);
            server.ShutdownServer();

            Assert.AreEqual(totMsgCl, numMsg*clAmount);
            Assert.AreEqual(totMsgsw, numMsg*clAmount);
            Assert.AreEqual(0,numErrors);
        }

        [TestMethod]
        // Send messages between 505-1000000 bytes and validate their order.
        public void TCPRandomSizeConsistencyTest()
        {
            const int numMsg = 1000;
            int clAmount = 10;
            int completionCount = clAmount;
            int numErrors = 0;

            ByteMessageTcpServer server = new ByteMessageTcpServer(2008);
            ConcurrentDictionary<ByteMessageTcpClient, int> clients = new ConcurrentDictionary<ByteMessageTcpClient, int>();

            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);
            int totMsgCl = 0;
            int totMsgsw = 0;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;
            server.GatherConfig = ScatterGatherConfig.UseQueue;
            server.StartServer();
            
            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new ByteMessageTcpClient();
                client.GatherConfig= ScatterGatherConfig.UseQueue;
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", 2008);
                Console.WriteLine(server.Sessions.Count);

                clients.TryAdd(client, -1);
            }

            Task.WaitAll(toWait);

            // Messages starts here

            Random random = new Random(42);
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg-1; i++)
                {
                    var msg = new byte[random.Next(505,1000000)];
                    PrefixWriter.WriteInt32AsBytes(msg, 0, i);
                    client.Key.SendAsync(msg);
                }
            });

            // final message to mark ending.
            foreach (var client in clients)
            {
                client.Key.SendAsync(new byte[502]);
            }

            void OnClientReceivedMessage(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                if (count == 502)
                {
                    Interlocked.Decrement(ref completionCount);
                    if (Interlocked.CompareExchange(ref completionCount, 0, 0) == 0)
                        testCompletionEvent.Set();
                    return;
                }

                int currVal = BitConverter.ToInt32(arg2, offset);
                var prevVal = clients[client];

                if (currVal != prevVal + 1)
                {
                    Interlocked.Increment(ref numErrors);
                }
                clients[client] = currVal;
            }

            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);

                var response = new byte[count];
                Buffer.BlockCopy(arg2, offset, response, 0, count);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(20000);
            server.ShutdownServer();

            Assert.AreEqual(totMsgCl, numMsg*clAmount);
            Assert.AreEqual(totMsgsw, numMsg*clAmount);
            Assert.AreEqual(0, numErrors);

        }

        #endregion

        #region SSL Custom

        [TestMethod]
        public void CustomSSlMessageRushConsistencyTest()
        {
            //CoreAssemblyConfig.UseUnmanaged = false;
            const int numMsg = 10000;
            int clAmount = 100;
            int completionCount = clAmount;
            int numErrors = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var ccert = new X509Certificate2("client.pfx", "greenpass");

            var server = new CustomSslServer(2008, scert);
            ConcurrentDictionary<CustomSslClient, int> clients = new ConcurrentDictionary<CustomSslClient, int>();


            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);
            int totMsgCl = 0;
            int totMsgsw = 0;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;

            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new CustomSslClient(ccert);
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                //toWait[i] = client.ConnectAsync("127.0.0.1", 2008,"example.com");
                client.ConnectAsyncAwaitable("127.0.0.1", 2008).Wait(); ;
                Console.WriteLine(server.Sessions.Count);

                clients.TryAdd(client, -1);
            }

            //Task.WaitAll(toWait);

            Thread.Sleep(1000);
            sw2.Start();

            // Messages starts here
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg - 1; i++)
                {
                    var msg = new byte[32];
                    PrefixWriter.WriteInt32AsBytes(msg, 0, i);
                    client.Key.SendAsync(msg);
                }
            }
            );
            // final message to mark ending.
            foreach (var client in clients)
            {
                client.Key.SendAsync(new byte[502]);
            }

            void OnClientReceivedMessage(CustomSslClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                if (count == 502)
                {
                    Interlocked.Decrement(ref completionCount);
                    if (Interlocked.CompareExchange(ref completionCount, 0, 0) == 0)
                        testCompletionEvent.Set();
                    return;
                }

                int currVal = BitConverter.ToInt32(arg2, offset);
                var prevVal = clients[client];
                if (currVal != prevVal + 1)
                {

                    Interlocked.Increment(ref numErrors);
                    Console.WriteLine("prev: " + prevVal);
                    Console.WriteLine("curr: " + currVal);
                }
                clients[client] = currVal;
                //client.SendAsync(response);
            }

            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);
                var response = new byte[count];
                Buffer.BlockCopy(arg2, offset, response, 0, count);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(50000);
            server.ShutdownServer();

            Assert.AreEqual(totMsgCl, numMsg * clAmount);
            Assert.AreEqual(totMsgsw, numMsg * clAmount);
            Assert.AreEqual(0, numErrors);
        }

        [TestMethod]
        // Send messages between 505-1000000 bytes and validate their order.
        public void CustomSSlRandomSizeConsistencyTest()
        {
            const int numMsg = 1000;
            int clAmount = 10;
            int completionCount = clAmount;
            int numErrors = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var ccert = new X509Certificate2("client.pfx", "greenpass");

            CustomSslServer server = new CustomSslServer(2008, scert);
            ConcurrentDictionary<CustomSslClient, int> clients = new ConcurrentDictionary<CustomSslClient, int>();


            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);
            int totMsgCl = 0;
            int totMsgsw = 0;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;

            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new CustomSslClient(ccert);
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", 2008);
                Console.WriteLine(server.Sessions.Count);

                clients.TryAdd(client, -1);
            }

            Task.WaitAll(toWait);

            // Messages starts here

            Random random = new Random(42);
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg - 1; i++)
                {
                    var msg = new byte[random.Next(505, 1000000)];
                    PrefixWriter.WriteInt32AsBytes(msg, 0, i);
                    client.Key.SendAsync(msg);
                }
            });

            // final message to mark ending.
            foreach (var client in clients)
            {
                client.Key.SendAsync(new byte[502]);
            }

            void OnClientReceivedMessage(CustomSslClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                if (count == 502)
                {
                    Interlocked.Decrement(ref completionCount);
                    if (Interlocked.CompareExchange(ref completionCount, 0, 0) == 0)
                        testCompletionEvent.Set();
                    return;
                }

                int currVal = BitConverter.ToInt32(arg2, offset);
                var prevVal = clients[client];

                if (currVal != prevVal + 1)
                {
                    Interlocked.Increment(ref numErrors);
                }
                clients[client] = currVal;
            }

            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);

                var response = new byte[count];
                Buffer.BlockCopy(arg2, offset, response, 0, count);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(20000);
            server.ShutdownServer();

            Assert.AreEqual(totMsgCl, numMsg * clAmount);
            Assert.AreEqual(totMsgsw, numMsg * clAmount);
            Assert.AreEqual(0, numErrors);

        }

        #endregion

        #region Ssl
        [TestMethod]
        // Send fix length many small messages and validate their order
        public void SSlMessageRushConsistencyTest()
        {
            const int numMsg = 10000;
            int clAmount = 100;
            int completionCount = clAmount;
            int numErrors = 0;

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var ccert = new X509Certificate2("client.pfx", "greenpass");

            SslByteMessageServer server = new SslByteMessageServer(2008, scert);
            ConcurrentDictionary<SslByteMessageClient, int> clients = new ConcurrentDictionary<SslByteMessageClient, int>();


            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);
            int totMsgCl = 0;
            int totMsgsw = 0;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;
            server.RemoteCertificateValidationCallback += ValidateCertAsServer;
            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new SslByteMessageClient(ccert);
                client.RemoteCertificateValidationCallback += ValidateCertAsClient;
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                //toWait[i] = client.ConnectAsync("127.0.0.1", 2008,"example.com");
                client.Connect("127.0.0.1", 2008);
                Console.WriteLine(server.Sessions.Count);

                clients.TryAdd(client, -1);
            }

            bool ValidateCertAsClient(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
            {
                return true;
            }
            bool ValidateCertAsServer(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
            {
                return true;
            }

            Thread.Sleep(1000);
            sw2.Start();

            // Messages starts here
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg - 1; i++)
                {
                    var msg = new byte[32];
                    PrefixWriter.WriteInt32AsBytes(msg, 0, i);
                    client.Key.SendAsync(msg);
                }
            }
            );
            // final message to mark ending.
            foreach (var client in clients)
            {
                client.Key.SendAsync(new byte[502]);
            }

            void OnClientReceivedMessage(SslByteMessageClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                if (count == 502)
                {
                    Interlocked.Decrement(ref completionCount);
                    if (Interlocked.CompareExchange(ref completionCount, 0, 0) == 0)
                        testCompletionEvent.Set();
                    return;
                }
                if (count != 32)
                {

                }
                int currVal = BitConverter.ToInt32(arg2, offset);
                var prevVal = clients[client];
                if (currVal != prevVal + 1)
                {
                    byte[] error = new byte[count];
                    Buffer.BlockCopy(arg2, offset, error, 0, count);
                    Interlocked.Increment(ref numErrors);
                    Console.WriteLine("prev:" + prevVal);
                    Console.WriteLine("curr: " + currVal);
                }
                clients[client] = currVal;
                //client.SendAsync(response);
            }

            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);
                var response = new byte[count];
                Buffer.BlockCopy(arg2, offset, response, 0, count);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(50000);
            server.ShutdownServer();

            Assert.AreEqual(totMsgCl, numMsg * clAmount);
            Assert.AreEqual(totMsgsw, numMsg * clAmount);
            Assert.AreEqual(0, numErrors);
        }
        [TestMethod]
        // Send messages between 505-1000000 bytes and validate their order.
        public void SSlRandomSizeConsistencyTest()
        {

            const int numMsg = 1000;
            int clAmount = 10;
            int completionCount = clAmount;
            int numErrors = 0;
            MiniLogger.AllLog+=(log)=>Console.WriteLine(log);

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var ccert = new X509Certificate2("client.pfx", "greenpass");

            SslByteMessageServer server = new SslByteMessageServer(2008, scert);
            ConcurrentDictionary<SslByteMessageClient, int> clients = new ConcurrentDictionary<SslByteMessageClient, int>();


            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);
            int totMsgCl = 0;
            int totMsgsw = 0;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;
            server.RemoteCertificateValidationCallback += ValidateCertAsServer;
            server.GatherConfig = ScatterGatherConfig.UseQueue;
            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new SslByteMessageClient(ccert);
                client.GatherConfig = ScatterGatherConfig.UseQueue;
                client.RemoteCertificateValidationCallback += ValidateCertAsClient;
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

                toWait[i] = client.ConnectAsyncAwaitable("127.0.0.1", 2008);
                Console.WriteLine(server.Sessions.Count);

                clients.TryAdd(client, -1);
            }


            bool ValidateCertAsClient(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
            {
                return true;
            }
            bool ValidateCertAsServer(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
            {
                return true;
            }
            Task.WaitAll(toWait);

            // Messages starts here

            Random random = new Random(42);
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg - 1; i++)
                {
                    var msg = new byte[random.Next(505, 500000)];
                    PrefixWriter.WriteInt32AsBytes(msg, 0, i);
                    client.Key.SendAsync(msg);
                }
            });

            // final message to mark ending.
            foreach (var client in clients)
            {
                client.Key.SendAsync(new byte[502]);
            }

            void OnClientReceivedMessage(SslByteMessageClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                if (count == 502)
                {
                    Interlocked.Decrement(ref completionCount);
                    if (Interlocked.CompareExchange(ref completionCount, 0, 0) == 0)
                        testCompletionEvent.Set();
                    return;
                }

                int currVal = BitConverter.ToInt32(arg2, offset);
                var prevVal = clients[client];

                if (currVal != prevVal + 1)
                {
                    Interlocked.Increment(ref numErrors);
                }
                clients[client] = currVal;
            }

            void OnServerReceviedMessage(in Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);

                var response = new byte[count];
                Buffer.BlockCopy(arg2, offset, response, 0, count);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(20000);
            server.ShutdownServer();

            Assert.AreEqual(totMsgCl, numMsg * clAmount);
            Assert.AreEqual(totMsgsw, numMsg * clAmount);
            Assert.AreEqual(0, numErrors);

        }
        #endregion

    }
}
