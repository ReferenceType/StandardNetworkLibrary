using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System.Diagnostics;
using System.Net;

internal class Program
{
    static Stopwatch sw1 = new Stopwatch();
    static Stopwatch sw2 = new Stopwatch();
    static int totMsgCl = 0;
    static int totMsgsw = 0;

    static void Main(string[] args)
    {
        
        UdpTest();

    }
    static void UdpTest()
    {
        MiniLogger.AllLog+=(string log)=>Console.WriteLine(log);
        int clAmount = 100;
        int timeClientsComplete=0;
        int timeServerComplete=0;

        AsyncUdpServer sw = new AsyncUdpServer(2008);
        sw.SocketSendBufferSize = 1280000000;
        sw.SocketReceiveBufferSize = 1280000000;

        sw.StartServer();
        sw.OnBytesRecieved += UdpSWBytsRec;

        var clients = new List<AsyncUdpClient>();

        for (int j = 0; j < clAmount; j++)
        {
            AsyncUdpClient cl = new AsyncUdpClient();

            //cl.SocketSendBufferSize = 128000000;
            //cl.ReceiveBufferSize = 12800000;

            cl.OnBytesRecieved +=(bytes,offset,count)=> ClientBytesRecieved(cl,bytes,offset,count);
            cl.Connect("127.0.0.1", 2008);

            cl.SendAsync(new byte[32]);
            clients.Add(cl);
        }
        
        Thread.Sleep(2222);
        Console.WriteLine("Test Starting");

        var bytes_ = new byte[63000];
        int nummsg = 5000;

        var t = new Thread(() =>
        {
        sw1.Start();
            for (int i = 0; i < nummsg; i++)
            //Parallel.For(0, nummsg, (i) =>
            {
                foreach (var client in clients)
                {
                    client.SendAsync(bytes_);
                }
            }
            //);
            //sw1.Stop();
            
            timeClientsComplete = (int)sw1.ElapsedMilliseconds;
            Console.WriteLine("Done Clients " + timeClientsComplete);
            PrintResults();

        });
        sw2.Start();

        t.Start();
       // t.Join();
        //t2.Join();
        while (Console.ReadLine() != "e")
        {
           PrintResults();
        }
       

        Console.ReadLine();

        void PrintResults()
        {
            timeClientsComplete = (int)sw1.ElapsedMilliseconds;
            timeServerComplete = (int)sw2.ElapsedMilliseconds;

            Console.WriteLine("Total Message Server received: " + totMsgsw);
            Console.WriteLine("Total Message Clients received: " + totMsgCl);

            var totMBytesTransferedByClients = (((double)totMsgsw * (double)bytes_.Length)) / 1000000;
            Console.WriteLine("Total Sucessfull Data Transfer Rate Clients: " + totMBytesTransferedByClients / ((float)timeClientsComplete / 1000));

            var totMBytesTransferedByServer = (((double)totMsgCl * (double)bytes_.Length)) / 1000000;
            Console.WriteLine("Total Sucessfull Data Transfer Rate Server: " + totMBytesTransferedByServer / ((float)timeServerComplete / 1000));
        }


        void UdpSWBytsRec(IPEndPoint endpoint, byte[] bytes,int offset, int count)
        {
            sw.SendBytesToClient(endpoint, bytes,offset,count);

            Interlocked.Increment(ref totMsgsw);
        }

        void ClientBytesRecieved(AsyncUdpClient cl, byte[] bytes, int offset, int count)
        {
            cl.SendAsync(bytes,offset,count);

            Interlocked.Increment(ref totMsgCl);
        }
    }
}