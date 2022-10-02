
using NetworkLibrary.TCP.SSL;
using NetworkLibrary;
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
using NetworkLibrary.UDP;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.Utils;
using System.Security.Cryptography;
using NetworkLibrary.TCP.SSL.Custom;
using System.Security.Cryptography.X509Certificates;

namespace ConsoleTest
{

    internal class Program
    {
        static int i = 0;
        static List<AsyncUdpClient> clients = new List<AsyncUdpClient>();
        static HashSet<int> s = new HashSet<int>();

        static Stopwatch sw = new Stopwatch();
        static Stopwatch sw2 = new Stopwatch();
       
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
            //AesTest();
            //SSlTest2();
            //SSlTest();
            TcpTest();
            
            //UdpTest();
            //UdpTest2();
            //UdpTestMc();

           
            
        }

        private static void UdpTest2()
        {
            AsyncUdpClient cl = new AsyncUdpClient(2008);
            cl.OnBytesRecieved += ClientBytesRecieved;
            //cl.Connect("127.0.0.1", 2009);
            cl.SetRemoteEnd("127.0.0.1", 2009);
            cl.SocketSendBufferSize = 64000;
            cl.ReceiveBufferSize = 64000;

            AsyncUdpClient cl2 = new AsyncUdpClient(2009);
            cl2.OnBytesRecieved += ClientBytesRecieved;
            //cl2.Connect("127.0.0.1", 2008);
            cl2.SetRemoteEnd("127.0.0.1", 2008);
           
            cl2.SocketSendBufferSize = 64000;
            cl2.ReceiveBufferSize = 64000;

            cl.SendAsync(new byte[32]);
            cl2.SendAsync(new byte[32]);

            Console.ReadLine();

        }

        private static void SSlTest2()
        {
            Stopwatch sw = new Stopwatch();
            int totMsgClient = 0;
            int totMsgServer = 0;
            byte[] req = new byte[32];
            byte[] resp = new byte[32];
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");
            CustomSslServer server = new CustomSslServer(2008, scert);
            server.OnBytesReceived += ServerReceived;
            server.StartServer();

            CustomSslClient client = new CustomSslClient(cert);
            client.OnBytesReceived += ClientReceived;
            client.ConnectAsyncAwaitable("127.0.0.1", 2008).Wait();
            sw.Start();
            for (int i = 0; i < 1000000; i++)
            {
                client.SendAsync(req);

            }
            client.SendAsync(new byte[502]);

            while (Console.ReadLine() != "e")
            {
                Console.WriteLine("Tot server" + Volatile.Read(ref totMsgServer));
                Console.WriteLine("Tot client" + Volatile.Read(ref totMsgClient));
            }

            void ServerReceived(Guid arg1, byte[] arg2, int arg3, int arg4)
            {
                Interlocked.Increment(ref totMsgServer);
                if(arg4 == 502)
                {
                    server.SendBytesToClient(arg1,new byte[502]);
                    return;
                }
                server.SendBytesToClient(arg1, resp);
            }

            void ClientReceived( byte[] arg2, int arg3, int arg4)
            {
                Interlocked.Increment(ref totMsgClient);
                if(arg4!= 32)
                {

                }
                if(arg4 == 502)
                {
                    sw.Stop();
                    Console.WriteLine(sw.ElapsedMilliseconds);
                }
               // client.SendAsync(req);
            }
        }

       

        private static void SSlTest()
        {
            int totMsgClient=0;
            int totMsgServer=0;
            byte[] req = new byte[32];
            byte[] resp = new byte[32];

            var scert = new X509Certificate2("server.pfx", "greenpass");
            var server = new SslByteMessageServer(2008, 2, scert);
            server.OnBytesReceived += ServerReceived;
            server.StartServer();

            var cert = new X509Certificate2("client.pfx", "greenpass");
            SslByteMessageClient client = new SslByteMessageClient(cert);

            client.OnBytesReceived += ClientReceived;
            client.Connect("127.0.0.1", 2008);

            for (int i = 0; i < 10000000; i++)
            {
                client.SendAsync(req);

            }

            while (Console.ReadLine()!="e")
            {
                Console.WriteLine("Tot server" + Volatile.Read(ref totMsgServer));
                Console.WriteLine("Tot client" + Volatile.Read(ref totMsgClient));
            }

            void ServerReceived(Guid arg1, byte[] arg2, int arg3, int arg4)
            {
                Interlocked.Increment(ref totMsgServer);
                server.SendBytesToClient(arg1, resp);
            }

            void ClientReceived( byte[] arg2, int arg3, int arg4)
            {
                Interlocked.Increment(ref totMsgClient);

                //client.Send(req);
            }
        }


        private static void UdpTestMc()
        {
            string ip = "239.255.0.1";
            AsyncUdpServer sw = new AsyncUdpServer(2008);
            sw.SetMulticastAddress(ip, 2005);
            sw.StartServer();

            AsyncUdpClient cl = new AsyncUdpClient(2005);
            //cl.Connect("127.0.0.1",2008);
            cl.SetRemoteEnd("127.0.0.1",2008);
            cl.SendAsync(new byte[111]);
            cl.JoinMulticastGroup(IPAddress.Parse(ip));

            sw.OnBytesRecieved += UdpSWBytsRec;
            cl.OnBytesRecieved += ClientBytesRecieved;
            Thread.Sleep(1000);

            sw.SendBytesToAllClients(new byte[111]);
            for (int i = 0; i < 2000; i++)
            {
                sw.MulticastMessage(new byte[1000]);

            }
            Console.WriteLine("Done mc");
            Console.ReadLine();

            Console.ReadLine();
            Console.WriteLine("totmeg server rec: " + totMsgsw);
            Console.WriteLine("totmeg client rec: " + totMsgCl);
            Console.WriteLine(i);
            Console.ReadLine();
            Console.WriteLine("totmeg server rec: " + totMsgsw);
            Console.WriteLine("totmeg client rec: " + totMsgCl);
            Console.ReadLine();
        }

        static void UdpTest()
        {
            int clAmount = 100;
            AsyncUdpServer sw = new AsyncUdpServer(2008);
            sw.StartServer();
            sw.OnBytesRecieved += UdpSWBytsRec;
            sw.SocketSendBufferSize = 128000000;
            sw.SocketReceiveBufferSize = 128000000;

            var clients = new List<AsyncUdpClient>();

            for (int j = 0; j < clAmount; j++)
            {
                AsyncUdpClient cl = new AsyncUdpClient();
                cl.OnBytesRecieved += ClientBytesRecieved;
                cl.Connect("127.0.0.1", 2008);
                cl.SendAsync(new byte[32]);
                cl.SocketSendBufferSize = 64000;
                cl.ReceiveBufferSize = 64000;
                clients.Add(cl);
            }
            
            Thread.Sleep(2222);

            var bytes_ = new byte[1500];
            int i = 0;

            var t = new Thread(() =>
            {
                for (i = 0; i < 5000; i++)
                {
                    Parallel.ForEach(clients, cl =>
                    {
                        cl.SendAsync(bytes_);
                    });
                  

                    //Console.WriteLine("Sending");
                    //Thread.Sleep(1);
                }
                Console.WriteLine("Done 1 " + sw2.ElapsedMilliseconds);

            });

            var t2 = new Thread(() =>
            {
                for (int j = 0; j < 5000; j++)
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
        static void UdpSWBytsRec(IPEndPoint endpoint, byte[] bytes)
        {
            Interlocked.Increment(ref totMsgsw);
        }

        static void ClientBytesRecieved(byte[] bytes, int offset, int count)
        {
            Interlocked.Increment(ref totMsgCl);
           //  Console.WriteLine("udp client recieved");
            // cl.SendBytes(new byte[11111]);
        }
       

        //----------TCP ----------------------------------------------------------------
        private static void TcpTest()
        {
            var msg1 = new byte[32];
            int clAmount = 10;

            server = new ByteMessageTcpServer(2008,clAmount*2);
            List<ByteMessageTcpClient> clients = new List<ByteMessageTcpClient>();
            server.MaxIndexedMemoryPerClient = 1280000000;
            server.DropOnBackPressure = false;
            server.StartServer();
            //BufferManager.InitContigiousSendBuffers(clAmount*2, 128000);
            //BufferManager.InitContigiousReceiveBuffers(clAmount*2, 128000);

            int dep =0;
            for (int i = 0; i < clAmount; i++)
            {

                var client = new ByteMessageTcpClient();
                client.BufferManager = server.BufferManager;
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.DropOnCongestion=false;
                //client.OnConnected += async () =>
                //{
                //    await Task.Delay(10000); Console.WriteLine("-------------------                            --------------"); client.Disconnect();
                //};
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => clientMsgRec2(client, arg2, offset, count);

                client.ConnectAsync("127.0.0.1", 2008);
                Console.WriteLine(server.SessionCount);

                clients.Add(client);
            }
            //client.SendAsync(new byte[123]);

            server.OnBytesReceived += SWOnMsgRecieved;
            Console.ReadLine();
            Console.WriteLine(server.SessionCount);           
            Console.ReadLine();
            Console.ReadLine();
            var msg = new byte[32];
            resp = new byte[32];

            for (int i = 0; i < msg.Length; i++)
            {
                msg[i] = 11;
            }


            const int numMsg = 1000000;
           
            var t1 = new Thread(() =>
            {
                for (int i = 0; i < numMsg; i++)
                {

                    foreach (var client in clients)
                    {
                        msg = new byte[32];
                        PrefixWriter.WriteInt32AsBytes(ref msg, 0, i);

                        client.SendAsync(msg);
                    }

                }

                foreach (var client in clients)
                {

                    client.SendAsync(new byte[502]);
                }

                //Thread.Sleep(111);


            });
            //t1.Start();
            sw2.Start();

            
            sw.Start();

            //-------------------
            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg; i++)
                {
                    client.SendAsync(msg);
                   // if (i == 1000000)client.Disconnect();
                }
            });
            foreach (var client in clients)
            {
                //client.Disconnect();

                client.SendAsync(new byte[502]);
            }
            //-------------------
            //t1.Join();
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
                Console.WriteLine("sw ses count "+server.SessionCount);
                
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
           // Console.WriteLine("tot msg client: " + totMsgCl);

            clientMsgRec(arg2, offset, count);

            if (pause)
                return;
            client.SendAsync(resp);
            //client.SendAsync(resp);
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
            server.SendBytesToClient(arg1, resp);
            lastSW = true;

            //if (BitConverter.ToInt32(arg2, offset) != prev + 1)
            //{
            //    Console.WriteLine("--- Prev " + prev);
            //    Console.WriteLine("---- Curr " + BitConverter.ToInt32(arg2, offset));
            //}
            //prev = BitConverter.ToInt32(arg2, offset);

        }


    }
}
