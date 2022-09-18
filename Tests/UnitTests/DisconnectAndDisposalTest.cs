using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary.TCP.ByteMessage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            for (int i = 0; i < 10; i++)
            {
                DisconnectTest();
                Thread.Sleep(2000);
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

            ByteMessageTcpServer server = new ByteMessageTcpServer(2008,200);
            List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);

            server.MaxIndexedMemoryPerClient = 1280000;
            server.DropOnBackPressure = false;
            server.OnBytesRecieved += OnServerReceviedMessage;

            server.StartServer();

            Task[] toWait = new Task[clAmount];
            for (int i = 0; i < clAmount; i++)
            {
                var client = new ByteMessageTcpClient();
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion = false;
                client.OnBytesRecieved += (byte[] arg2, int offset, int count) => OnClientReceıvedMEssage(client, arg2, offset, count);

                toWait[i]=client.ConnectAsyncAwaitable("127.0.0.1", 2008);
                Console.WriteLine(server.Sessions.Count);

                clients.Add(client);
            }

            Task.WaitAll(toWait);

            // Deploy timed disconnections.
            foreach (var client in clients)
            {
                Task.Run(async () => {
                    await Task.Delay(2000);
                    Interlocked.Increment(ref totDisconnectRequest);
                    Console.WriteLine("-------------------            Disconnect Signalled By Client            --------------");
                    client.Disconnect();
                    if (Volatile.Read(ref totDisconnectRequest) >= clAmount)
                    {
                        client.OnDisconnected += () => testCompletionEvent.Set();
                    }
                });
            }

            // Messages starts here
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg; i++)
                {
                    client.SendAsync(msg);
                   
                }
            });

            void OnClientReceıvedMEssage(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgCl);
                client.SendAsync(response);
            }

            void OnServerReceviedMessage(Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgsw);
                server.SendBytesToClient(id, response);
            }


            testCompletionEvent.WaitOne(12000);

            long waitcount = 0;
            while (server.Sessions.Count != 0|| waitcount<1000000)
            {
                waitcount++;
                Thread.SpinWait(100);
            }

            if (!server.BufferManager.VerifyAvailableRBIndexes())
            {
                server.BufferManager.VerifyAvailableRBIndexes();
            }
            Assert.AreEqual(0, server.Sessions.Count);
            Assert.IsTrue(server.BufferManager.VerifyAvailableRBIndexes());
            Assert.IsTrue(server.BufferManager.VerifyAvailableSBIndexes());
            server.StopServer();
        }

       
    }
}
