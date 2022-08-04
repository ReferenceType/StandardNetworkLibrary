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
        private static ByteProtocolTcpServer server;

        static void Main(string[] args)
        {
            TcpTest();
            //UdpTest();
        }

        static void UdpTest()
        {
            //// TcpTest();
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

        static void ClientBytesRecieved(byte[] bytes)
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
        private static void ClienyRec(byte[] bytes)
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
             server = new ByteProtocolTcpServer(2008);
            List<ByteProtocolTcpClient> clients = new List<ByteProtocolTcpClient>();
            

            int clAmount = 5;
            bool V2 = false;
            for (int i = 0; i < clAmount; i++)
            {
                var client = new ByteProtocolTcpClient();
                client.ConnectAsyncAwaitable("127.0.0.1", 2008).Wait();
                Console.WriteLine(server.Sessions.Count);

                client.OnBytesRecieved += clientMsgRec;
                client.V2 = V2;
                clients.Add(client);
            }
            //client.SendAsync(new byte[123]);

            server.OnBytesRecieved += SWOnMsgRecieved;
            server.V2 = V2;
            Console.ReadLine();
            Console.WriteLine(server.Sessions.Count);
            Console.ReadLine();

            var msg = new byte[500];
            for (int i = 0; i < msg.Length; i++)
            {
                msg[i] = 11;
            }
            var t1 = new Thread(() =>
             {
                 for (int i = 0; i < 200000; i++)
            {
                     foreach (var client in clients)
                     {
                         client.SendAsync(msg);
                     }
                 }

                 foreach (var client in clients)
                 {
                     client.SendAsync(new byte[502]);
                 }

             });
            t1.Start();
            sw2.Start();

            var t2 = new Thread(() =>{
            for (int i = 0; i < 200000; i++)
            {
                server.BroadcastByteMsg(msg);
            }
            server.BroadcastByteMsg(new byte[502]);
              });
           //   t2.Start();
            sw.Start();

            t1.Join();
            Console.WriteLine(sw2.ElapsedMilliseconds);

           // t2.Join();
            Console.WriteLine("2-- "+sw2.ElapsedMilliseconds);

            Console.ReadLine();
            Console.WriteLine(totMsgsw);
            Console.WriteLine(totMsgCl);
            Console.ReadLine();


            //client.SendAsync(new byte[502]);

            //OnMsgRecieved(Guid.NewGuid(), new byte[501]);
            Thread.Sleep(3000);
            sw.Start();
            for (int i = 0; i < 100000; i++)
            {

                server.BroadcastByteMsg(new byte[500]);

            }
            server.BroadcastByteMsg(new byte[502]);

        }

        private static void clientMsgRec( byte[] arg2)
        {

            
            if (arg2.Length == 502)
            {
                //sw.Stop();
                //Console.WriteLine("Server REc: " + arg2.Length);
                Console.WriteLine("Time client " + sw.ElapsedMilliseconds);
                Console.WriteLine("tot msg client: " + totMsgCl);

                sw.Reset();
                return;
            }
            //for (int i = 0; i < arg2.Length; i++)
            //{
            //    if (arg2[i] != 11)
            //        throw new Exception();
            //}
            Interlocked.Increment(ref totMsgCl);
        }

        private static void SWOnMsgRecieved(Guid arg1, byte[] arg2)
        {
            
            //Console.WriteLine(arg2.Length);
            if (arg2.Length < 5000)
            {
               // throw new Exception();
            }
            if (arg2.Length == 502)
            {
                //sw.Stop();
                //Console.WriteLine("Server REc: " + arg2.Length);
                Console.WriteLine("Time: " +sw2.ElapsedMilliseconds);
                Console.WriteLine("tot msg sw: " + totMsgsw);
                //sw2.Reset();
                return;
            }
            server.SendBytesToClient(arg1, arg2);
            ///* cq.Enqueue*/Task.Run(() => {
            //for (int i = 0; i < arg2.Length; i++)
            //{
            //    if (arg2[i] != 11)
            //        throw new Exception();
            //}
            Interlocked.Increment(ref totMsgsw);
           // });
           // are.Set();
            

        }

        private static void OncOn()
        {
            Console.WriteLine("client connected");
        }

        private static void ClientRec(byte[] bytes)
        {
            Console.WriteLine("client REc");
        }

        private static void AA(int id, byte[] bytes)
        {
            Console.WriteLine("Server REc");
        }
    }
}
