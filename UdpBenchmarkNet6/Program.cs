using CustomNetworkLib;
using NetworkSystem;
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
        int clAmount = 1000;
        int timeClientsComplete=0;
        int timeServerComplete=0;

        AsyncUdpServer sw = new AsyncUdpServer(2008);
        sw.SocketSendBufferSize = 128000000;
        sw.SocketReceiveBufferSize = 128000000;

        sw.StartServer();
        sw.OnBytesRecieved += UdpSWBytsRec;

        var clients = new List<AsyncUdpClient>();

        for (int j = 0; j < clAmount; j++)
        {
            AsyncUdpClient cl = new AsyncUdpClient();

            cl.SocketSendBufferSize = 64000;
            cl.ReceiveBufferSize = 64000;

            cl.OnBytesRecieved += ClientBytesRecieved;
            cl.Connect("127.0.0.1", 2008);

            cl.SendAsync(new byte[32]);
            clients.Add(cl);
        }
        
        Thread.Sleep(2222);
        Console.WriteLine("Test Starting");

        var bytes_ = new byte[32000];
        int nummsg = 500;

        var t = new Thread(() =>
        {
            sw1.Start();
            foreach (var client in clients)
            {
                for (int i = 0; i < nummsg; i++)
                {
                    client.SendAsync(bytes_);
                }
            }/*);*/
            sw1.Stop();
            timeClientsComplete = (int)sw1.ElapsedMilliseconds;
            Console.WriteLine("Done Clients " + timeClientsComplete);
        });

        var t2 = new Thread(() =>
        {
            sw2.Start();
            for (int j = 0; j < nummsg; j++)
            {
                sw.SendBytesToAllClients(bytes_);
            }
            sw2.Stop();
            timeServerComplete = (int)sw2.ElapsedMilliseconds;
            Console.WriteLine("Done Clients " + timeServerComplete);

        });
       

        t.Start();
        t2.Start();

        t.Join();
        t2.Join();

        
        Console.WriteLine("Done all");
        Console.ReadLine();

        Console.WriteLine("Total Message Server received: " + totMsgsw);
        Console.WriteLine("Total Message Clients received: " + totMsgCl);

        var totMBytesTransferedByClients = (((double)totMsgCl * (double)bytes_.Length)) / 1000000;
        Console.WriteLine("Total Sucessfull Data Transfer Rate Clients: " + totMBytesTransferedByClients/((float)timeClientsComplete/1000));

        var totMBytesTransferedByServer = (((double)totMsgsw * (double)bytes_.Length)) / 1000000;
        Console.WriteLine("Total Sucessfull Data Transfer Rate Server: " + totMBytesTransferedByServer /((float)timeServerComplete/1000));

        Console.ReadLine();


        void UdpSWBytsRec(IPEndPoint endpoint, byte[] bytes)
        {
            Interlocked.Increment(ref totMsgsw);
        }

        void ClientBytesRecieved(byte[] bytes, int offset, int count)
        {
            Interlocked.Increment(ref totMsgCl);
        }
    }
}