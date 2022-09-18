using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomNetworkLib;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CustomNetworkLib.Utils;
using System.Collections.Concurrent;

namespace UnitTests
{
    [TestClass]
    public class MessageConsistencyTest
    {
        [TestMethod]
        // Send fix length many small messages and validate their order
        public void MessageRushConsistencyTest()
        {
            const int numMsg = 100000;
            int clAmount = 100;
            int completionCount = clAmount;
            int numErrors = 0;
            ByteMessageTcpServer server = new ByteMessageTcpServer(2008,clAmount*2);
            ConcurrentDictionary<ByteMessageTcpClient,int> clients = new ConcurrentDictionary<ByteMessageTcpClient,int>();

            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);
            int totMsgCl = 0;
            int totMsgsw = 0;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.DropOnBackPressure = false;
            server.OnBytesRecieved += OnServerReceviedMessage;

            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new ByteMessageTcpClient();
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesRecieved += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

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

            void OnServerReceviedMessage(Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);
                var response= new byte[count];
                Buffer.BlockCopy(arg2,offset,response,0,count);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(50000);
            server.StopServer();

            Assert.AreEqual(totMsgCl, numMsg*clAmount);
            Assert.AreEqual(totMsgsw, numMsg*clAmount);
            Assert.AreEqual(0,numErrors);
        }

        [TestMethod]
        // Send messages between 505-1000000 bytes and validate their order.
        public void RandomSizeConsistencyTest()
        {
            const int numMsg = 1000;
            int clAmount = 10;
            int completionCount = clAmount;
            int numErrors = 0;

            ByteMessageTcpServer server = new ByteMessageTcpServer(2008, clAmount * 2);
            ConcurrentDictionary<ByteMessageTcpClient, int> clients = new ConcurrentDictionary<ByteMessageTcpClient, int>();

            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);
            int totMsgCl = 0;
            int totMsgsw = 0;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.DropOnBackPressure = false;
            server.OnBytesRecieved += OnServerReceviedMessage;

            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new ByteMessageTcpClient();
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesRecieved += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);

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

            void OnServerReceviedMessage(Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);

                var response = new byte[count];
                Buffer.BlockCopy(arg2, offset, response, 0, count);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(20000);
            server.StopServer();

            Assert.AreEqual(totMsgCl, numMsg*clAmount);
            Assert.AreEqual(totMsgsw, numMsg*clAmount);
            Assert.AreEqual(0, numErrors);

        }
    }
}
