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
    private static int totMsgClprev;
    private static int totMsgSwprev;

    static void Main(string[] args)
    {

        UdpTest();

    }
    static void UdpTest()
    {
        MiniLogger.AllLog += (string log) => Console.WriteLine(log);
        int clAmount = 16;
        int timeStamp = 0;

        AsyncUdpServerLite sw = new AsyncUdpServerLite(2008);
        sw.SocketSendBufferSize = 1280000000;
        sw.SocketReceiveBufferSize = 1280000000;

        sw.StartServer();
        sw.OnBytesRecieved += UdpSWBytsRec;
        IPEndPoint sEp = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2008);
        var clients = new List<AsyncUdpServerLite>();

        for (int j = 0; j < clAmount; j++)
        {
            AsyncUdpServerLite cl = new AsyncUdpServerLite(0);

            cl.OnBytesRecieved += (ep, bytes, offset, count) => ClientBytesRecieved(cl, bytes, offset, count);
            // cl.Connect("127.0.0.1", 2008);

            cl.StartAsClient(sEp);
            //cl.SendBytesToClient(new byte[32]);
            clients.Add(cl);
        }

        Thread.Sleep(2222);
        Console.WriteLine("Test Starting");

        var bytes_ = new byte[32];
        int nummsg = 500;


        sw1.Start();
        sw2.Start();
        for (int i = 0; i < nummsg; i++)
        {
            foreach (var client in clients)
            {
                client.SendBytesToClient(sEp, bytes_, 0, bytes_.Length);
            }
        }

        PrintResults();


        // t.Join();
        //t2.Join();
        while (Console.ReadLine() != "e")
        {
            PrintResults();
        }


        Console.ReadLine();

        void PrintResults()
        {
            timeStamp = (int)sw2.ElapsedMilliseconds;
            sw2.Restart();
            totMsgClprev = Interlocked.Exchange(ref totMsgCl, 0);
            totMsgSwprev = Interlocked.Exchange(ref totMsgsw, 0);
            Console.WriteLine("server msg/s: " + 1000 * totMsgClprev / timeStamp);
            Console.WriteLine("client msg/s: " + 1000 * totMsgClprev / timeStamp);

            var totMBytesTransferedByClients = ((totMsgSwprev * bytes_.Length)) / 1000000d;
            Console.WriteLine("Total Sucessfull Data Transfer Rate Clients: " + totMBytesTransferedByClients / ((float)timeStamp / 1000));

            var totMBytesTransferedByServer = ((totMsgClprev * bytes_.Length)) / 1000000d;
            Console.WriteLine("Total Sucessfull Data Transfer Rate Server: " + totMBytesTransferedByServer / ((float)timeStamp / 1000));
        }


        void UdpSWBytsRec(IPEndPoint endpoint, byte[] bytes, int offset, int count)
        {
            sw.SendBytesToClient(endpoint, bytes, offset, count);

            Interlocked.Increment(ref totMsgsw);
        }

        void ClientBytesRecieved(AsyncUdpServerLite cl, byte[] bytes, int offset, int count)
        {
            cl.SendBytesToClient(sEp, bytes, offset, count);

            Interlocked.Increment(ref totMsgCl);
        }
    }
}

//internal class Program
//{
//    static Stopwatch sw1 = new Stopwatch();
//    static Stopwatch sw2 = new Stopwatch();
//    static int totMsgCl = 0;
//    static int totMsgsw = 0;
//    private static int totMsgClprev;
//    private static int totMsgSwprev;
//    static byte[] B = new byte[64000];
//    static void Main(string[] args)
//    {

//        UdpTest();

//    }
//    static void UdpTest()
//    {
//        MiniLogger.AllLog += (string log) => Console.WriteLine(log);
//        int clAmount = 16;
//        int timeStamp = 0;

//        AsyncUdpServer sw = new AsyncUdpServer(2008);
//        sw.SocketSendBufferSize = 1280000000;
//        sw.SocketReceiveBufferSize = 1280000000;

//        sw.StartServer();
//        sw.OnBytesRecieved += UdpSWBytsRec;

//        var clients = new List<AsyncUdpClient>();

//        for (int j = 0; j < clAmount; j++)
//        {
//            AsyncUdpClient cl = new AsyncUdpClient();

//            cl.OnBytesRecieved += (bytes, offset, count) => ClientBytesRecieved(cl, bytes, offset, count);
//            cl.Connect("127.0.0.1", 2008);

//            cl.SendAsync(new byte[32]);
//            clients.Add(cl);
//        }

//        Thread.Sleep(2222);
//        Console.WriteLine("Test Starting");

//        var bytes_ = new byte[32];
//        int nummsg = 500;


//        sw1.Start();
//        sw2.Start();
//        for (int i = 0; i < nummsg; i++)
//        {
//            foreach (var client in clients)
//            {
//                client.SendAsync(bytes_);
//            }
//        }

//        PrintResults();


//        // t.Join();
//        //t2.Join();
//        while (Console.ReadLine() != "e")
//        {
//            PrintResults();
//        }


//        Console.ReadLine();

//        void PrintResults()
//        {
//            timeStamp = (int)sw2.ElapsedMilliseconds;
//            sw2.Restart();
//            totMsgClprev = Interlocked.Exchange(ref totMsgCl,0);
//            totMsgSwprev = Interlocked.Exchange(ref totMsgsw, 0);
//            Console.WriteLine("server msg/s: " + 1000*totMsgClprev/ timeStamp);
//            Console.WriteLine("client msg/s: " + 1000* totMsgClprev / timeStamp);

//            var totMBytesTransferedByClients = (((long)totMsgSwprev * B.Length)) / 1000000d;
//            Console.WriteLine("Total Sucessfull Data Transfer Rate Clients: " + totMBytesTransferedByClients / ((float)timeStamp / 1000));

//            var totMBytesTransferedByServer = ((totMsgClprev * bytes_.Length)) / 1000000d;
//            Console.WriteLine("Total Sucessfull Data Transfer Rate Server: " + totMBytesTransferedByServer / ((float)timeStamp / 1000));
//        }


//        void UdpSWBytsRec(IPEndPoint endpoint, byte[] bytes, int offset, int count)
//        {
//            sw.SendBytesToClient(endpoint, bytes, offset, count);

//            Interlocked.Increment(ref totMsgsw);
//        }

//        void ClientBytesRecieved(AsyncUdpClient cl, byte[] bytes, int offset, int count)
//        {
//            cl.SendAsync(bytes, offset, count);

//            Interlocked.Increment(ref totMsgCl);
//        }
//    }
//}