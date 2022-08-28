using CustomNetworkLib;
using CustomNetworkLib.SocketEventArgsTests;
using NetworkSystem;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleTest
{

    internal class Program
    {
        static int i = 0;
        static List<AsyncUdpClient> clients = new List<AsyncUdpClient>();
        static HashSet<int> s = new HashSet<int>();

        static Stopwatch sw = new Stopwatch();
        static Stopwatch sw2 = new Stopwatch();
        static ConcurrentQueue<Action> cq = new ConcurrentQueue<Action> ();
        //static AutoResetEvent are = new AutoResetEvent (false);
        static int totMsgCl = 0;
        static int totMsgsw = 0;
        private static ByteMessageTcpServer server;
        static byte[] resp = new byte[32];
        static bool lastSW=false;
        private static int prev=-1;
        private static bool pause;

        static void Main(string[] args)
        {
             TcpTest();
            
            //UdpTest();

            return;
            byte[] buffer = new byte[1024];
            ArraySegment<byte> seg = new ArraySegment<byte>(buffer);
            Queue<byte[]> qq = new Queue<byte[]>();
            Queue<ArraySegment<byte>> qq1 = new Queue<ArraySegment<byte>>();
            Console.ReadLine();

            for (int i = 0; i < 50000000; i++)
            {
                qq.Enqueue(buffer);
            }
            Console.ReadLine();
            while (qq.Count>1)
            {
                var a  = qq.Dequeue();
                a = null;
            }
            Console.WriteLine("dq");
            Console.ReadLine();


            Console.WriteLine("eqdq");

            Console.ReadLine();
            Console.WriteLine("gc");
            GC.Collect();
            Console.ReadLine();
            return;
            for (int i = 0; i < 55000000; i++)
            {
                qq.Enqueue(buffer);
            }
            Console.ReadLine();
        }

        static void UdpTest()
        {
            // TcpTest();
            //test();
            //return;
            AsyncUdpServer sw = new AsyncUdpServer(2008);
            AsyncUdpClient cl = new AsyncUdpClient();

            sw.OnBytesRecieved += UdpSWBytsRec;
            cl.ConnectAsync("127.0.0.1", 2008);
            cl.OnBytesRecieved += ClientBytesRecieved;
            Thread.Sleep(1000);
            var bytes_ = new byte[1000];
            cl.SendBytes(new byte[11111]);
            cl.SendBytes(new byte[11111]);
            cl.SendBytes(new byte[11111]);
            cl.SendBytes(new byte[11111]);
            int i = 0;

            var t = new Thread(() =>
            {
                for (i = 0; i < 200000; i++)
                {

                    cl.SendBytes(bytes_);

                    //Console.WriteLine("Sending");
                    //Thread.Sleep(1);
                }
                Console.WriteLine("Done 1 " + sw2.ElapsedMilliseconds);

            });

            var t2 = new Thread(() =>
            {
                for (int j = 0; j < 200000; j++)
                {
                    sw.SendBytesToAllClients(bytes_);
                }
                Console.WriteLine("Done 2 " + sw2.ElapsedMilliseconds);

            });
            sw2.Reset();

            t.Start();
            t2.Start();
            sw2.Start();

            t.Join();
            t2.Join();

           //t.Wait();
            //t2.Wait();
            Console.WriteLine("Done all " + sw2.ElapsedMilliseconds);



            Console.ReadLine();
            Console.WriteLine("totmeg server rec: " + totMsgsw);
            Console.WriteLine("totmeg client rec: " + totMsgCl);
            Console.WriteLine(i);
            Console.ReadLine();
            Console.WriteLine("totmeg server rec: " + totMsgsw);
            Console.WriteLine("totmeg client rec: " + totMsgCl);
            Console.ReadLine();
        }
        static void UdpSWBytsRec(int id, byte[] bytes)
        {
            Interlocked.Increment(ref totMsgsw);
            
           // Console.WriteLine("udp sw recieved");
            // sw.SendBytesToAllClients(bytes);

        }

        static void ClientBytesRecieved(byte[] bytes, int offset, int count)
        {
            Interlocked.Increment(ref totMsgCl);
           //  Console.WriteLine("udp client recieved");
            // cl.SendBytes(new byte[11111]);
        }
        private static void UdpTest2()
        {
            AsyncUdpServer srw = new AsyncUdpServer();
            srw.OnBytesRecieved += REc;

            
            
            for (int ii = 0; ii < 10; ii++)
            {
                AsyncUdpClient cl = new AsyncUdpClient();
                cl.ConnectAsync("127.0.0.1", 20008);
                cl.OnBytesRecieved += ClienyRec;
                clients.Add(cl);
            }
            var btyea = new byte[20000];

            Task.Run(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    foreach (var clnt in clients)
                    {
                        clnt.SendBytes(btyea);

                    }
                }
            });


            Task.Run(() => { 
                sw2.Start();
                for (int j = 0; j < 10000; j++)
                {
                    // cl.SendBytes(new byte[2000]);

                    srw.SendBytesToAllClients(btyea);

                    //Task.Delay(1).Wait();
                }
                sw2.Stop();
                Console.WriteLine(sw2.ElapsedMilliseconds);
            });






            Console.ReadLine();
            Console.WriteLine(totMsgCl);
            Console.WriteLine(totMsgsw);

            Console.ReadLine();
        }
        private static void ClienyRec(byte[] bytes, int offset, int count)
        {
            //Console.WriteLine(i);
            //s.Add(Thread.CurrentThread.ManagedThreadId);
            Interlocked.Increment(ref totMsgCl);
            i++;
        }

        private static void REc(int id, byte[] bytes)
        {
            //Console.WriteLine(i);
            //s.Add(Thread.CurrentThread.ManagedThreadId);
            Interlocked.Increment(ref totMsgsw);
            i++;
        }

        //----------TCP ----------------------------------------------------------------
        private static void TcpTest()
        {
            server = new ByteMessageTcpServer(2008);
            List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();
            

            int clAmount = 1;
            BufferManager.InitContigiousBuffers(clAmount*8, 1280000);

            bool V2 = true;
            server.Lite = V2;

            for (int i = 0; i < clAmount; i++)
            {

                var client = new ByteMessageTcpClient();
                client.V2 = V2;

                client.ConnectAsyncAwaitable("127.0.0.1", 2008);
                Console.WriteLine(server.Sessions.Count);
            
                client.OnBytesRecieved += (byte[] arg2, int offset, int count)=>clientMsgRec2(client, arg2, offset, count);
                //client.OnBytesRecieved +=clientMsgRec;
                //client.OnConnected += () => Console.WriteLine("aaaaaaa");
                clients.Add(client);
            }
            //client.SendAsync(new byte[123]);

            server.OnBytesRecieved += SWOnMsgRecieved;
            Console.ReadLine();
            Console.WriteLine(server.Sessions.Count);
            Console.ReadLine();
            var msg = new byte[32];
            resp = msg;

            for (int i = 0; i < msg.Length; i++)
            {
                msg[i] = 11;
            }


            const int numMsg = 100000000;
           
            var t1 = new Thread(() =>
            {
                for (int i = 0; i < numMsg; i++)
                {

                    foreach (var client in clients)
                    {
                        //msg = new byte[130000];
                        //BufferManager.WriteInt32AsBytes(ref msg, 0, i);

                        client.SendAsync(msg);
                        

                    }

                }

                foreach (var client in clients)
                {

                    client.SendAsync(new byte[502]);
                }

                //Thread.Sleep(111);


            });
            t1.Start();
            sw2.Start();

            
            sw.Start();

            //-------------------
            //Parallel.For(0, numMsg, i =>
            //{
            //    foreach (var client in clients)
            //    {
            //        var msg1 = new byte[32];
            //        ByteMessageSessionV2.FillHeader(ref msg1, 0, i);
            //        client.SendAsync(msg1);

            //    }

            //});
            //foreach (var client in clients)
            //{
            //    client.SendAsync(new byte[502]);
            //}
            //-------------------
            t1.Join();
            Console.WriteLine(sw2.ElapsedMilliseconds);

           // t2.Join();
            Console.WriteLine("2-- "+sw2.ElapsedMilliseconds);

            Console.ReadLine();
            GC.Collect();
            for (int i = 0; i < 6; i++)
            {
                Console.WriteLine("Total on server: "+totMsgsw);
                Console.WriteLine("Total on clients: "+totMsgCl);
                Console.WriteLine("2-- " + sw2.ElapsedMilliseconds);
                Console.WriteLine("last was sw "+lastSW);
                
                Console.ReadLine();
                if (i == 2)
                {
                    Console.WriteLine("will pause server");
                    pause = true;
                }
            }

            Console.ReadLine();


            Parallel.For(0, clients.Count, i =>
            {

                clients[i].Disconnect();

            });
            Console.WriteLine("DC");
            Console.ReadLine();
           
            Console.Read();
        }

        private static void clientMsgRec2(ByteMessageTcpClient client, byte[] arg2, int offset, int count)
        {
            
            clientMsgRec(arg2, offset, count);

            if (pause)
                return;
            //Task.Run(() =>client.SendAsync(resp));
            //Task.Run(() =>client.SendAsync(resp));
            //Task.Run(() =>client.SendAsync(resp));
            //client.SendAsync(resp);
            lastSW = false;
        }

        private static void clientMsgRec(/*ByteProtocolTcpClient client,*/ byte[] arg2, int offset,int count)
        {
            if (count == 502)
            {
                Console.WriteLine("Time client " + sw.ElapsedMilliseconds);
                Console.WriteLine("tot msg client: " + totMsgCl);

                //sw.Reset();
                //return;
            }
            
            Interlocked.Increment(ref totMsgCl);
            //Console.WriteLine("Sending");
            //client.SendAsync(resp);
            //if (totMsgCl % 1000000 == 0)
            //{
            //    Console.WriteLine(totMsgCl);
            //    Console.WriteLine("Time client " + sw.ElapsedMilliseconds);

            //}

        }

        private static void SWOnMsgRecieved(Guid arg1, byte[] arg2, int offset, int count)
        {
           
            //server.SendBytesToClient(arg1, resp);
           // server.SendBytesToClient(arg1, resp);
            Interlocked.Increment(ref totMsgsw);


            if (count == 502)
            {
                Console.WriteLine("Time: " +sw2.ElapsedMilliseconds);
                Console.WriteLine("tot msg sw: " + totMsgsw);
                server.SendBytesToClient(arg1,new byte[502]);

                //return;
            }
            //server.SendBytesToClient(arg1, resp);
            lastSW = true;

            //if (BitConverter.ToInt32(arg2, offset) != prev + 1)
            //{
            //    Console.WriteLine("--- Prev " + prev);
            //    Console.WriteLine("---- Curr " + BitConverter.ToInt32(arg2, offset));
            //}
            //prev = BitConverter.ToInt32(arg2, offset);

            server.SendBytesToClient(arg1, resp);
           

            



        }


    }
}
