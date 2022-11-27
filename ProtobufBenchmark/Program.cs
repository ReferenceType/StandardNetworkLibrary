using NetworkLibrary.TCP;
using Protobuff;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

internal class Program
{
    static void Main(string[] args)
    {
        //BurstBench();
        //EchoBench();
        SecureProtoBench();
    }

    private static void EchoBench()
    {
        long totMsgCl = 0;
        long totMsgsw = 0;
        Stopwatch sw = new Stopwatch();
        MessageEnvelope msg = new MessageEnvelope()
        {
            Header = "Test",
            Payload = new byte[32]

        };

        MessageEnvelope end = new MessageEnvelope()
        {
            Header = "Stop"
        };
        ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();

       

        var server = new ProtoServer(20008, 100);
        server.OnMessageReceived += ServerStsReceived;

        var clients = new List<ProtoClient>();
        for (int i = 0; i < 100; i++)
        {
            var client = new ProtoClient();
            client.OnMessageReceived += (reply) => ClientStsReceived(client, reply);

            client.Connect("127.0.0.1", 20008);
            clients.Add(client);
        }

        Task.Run(async () =>
        {
            long elapsedPrev = sw.ElapsedMilliseconds;
            long totMsgClPrev = 0;
            long totMsgswPrev = 0;
            while (true)
            {
                await Task.Delay(2000);
                long elapsedCurr = sw.ElapsedMilliseconds;
                int deltaT = (int)(elapsedCurr - elapsedPrev);
                Console.WriteLine("Elapsed: " + elapsedCurr);
                Console.WriteLine("client total message {0} server total message {1}", totMsgCl, totMsgsw);
                float throughput = (totMsgCl - totMsgClPrev + totMsgsw - totMsgswPrev) / (deltaT / 1000);

                Console.WriteLine("Throughput :" + throughput.ToString("N1"));
                elapsedPrev = elapsedCurr;
                totMsgswPrev = totMsgsw;
                totMsgClPrev = totMsgCl;

            }

        });
        sw.Start();
        Parallel.ForEach(clients, client =>
        {
            for (int i = 0; i < 1000; i++)
            {
                client.SendAsyncMessage(msg);
            }
            //client.SendStatusMessage(end);

        });

        //var resp = clients[0].SendMessageAndWaitResponse(msg).Result;
        while (Console.ReadLine() != "e")
        {
            Console.WriteLine("client {0} server {1}", totMsgCl, totMsgsw);
        }
        Console.ReadLine();


        void ServerStsReceived(in Guid arg1, MessageEnvelope arg2)
        {
            Interlocked.Increment(ref totMsgsw);
            server.SendAsyncMessage(in arg1, arg2);
        }

        void ClientStsReceived(ProtoClient client, MessageEnvelope reply)
        {
            Interlocked.Increment(ref totMsgCl);
            client.SendAsyncMessage(reply);

            if (reply.Header.Equals("Stop"))
            {
                Console.WriteLine(sw.ElapsedMilliseconds);
            }
        }


    }

    static void BurstBench()
    {
        int totMsgCl = 0;
        int totMsgsw = 0;
        Stopwatch sw = new Stopwatch();
        MessageEnvelope msg = new MessageEnvelope()
        {
            Header = "Test",
            Payload = new byte[32]

        };

        MessageEnvelope end = new MessageEnvelope()
        {
            Header = "Stop"
        };

        ProtoServer server = new ProtoServer(20008, 100);
        server.OnMessageReceived += ServerStsReceived;

        var clients = new List<ProtoClient>();
        for (int i = 0; i < 100; i++)
        {
            var client = new ProtoClient();
            client.OnMessageReceived += (reply) => ClientStsReceived(client, reply);

            client.Connect("127.0.0.1", 20008);
            clients.Add(client);
        }

        sw.Start();
        Parallel.ForEach(clients, client =>
        {
            for (int i = 0; i < 100000; i++)
            {
                client.SendAsyncMessage(msg);
            }
            client.SendAsyncMessage(end);

        });

        //var resp = clients[0].SendMessageAndWaitResponse(msg).Result;
        while (Console.ReadLine() != "e")
        {
            Console.WriteLine("client {0} server {1}", totMsgCl, totMsgsw);
        }
        Console.ReadLine();


        void ServerStsReceived(in Guid arg1, MessageEnvelope arg2)
        {
            Interlocked.Increment(ref totMsgsw);
            server.SendAsyncMessage(arg1, arg2);
        }

        void ClientStsReceived(ProtoClient client, MessageEnvelope reply)
        {
            Interlocked.Increment(ref totMsgCl);
            if (reply.Header.Equals("Stop"))
            {
                Console.WriteLine(sw.ElapsedMilliseconds);
            }
        }

      
    }
    private static void SecureProtoBench()
    {
        long totMsgCl = 0;
        long totMsgsw = 0;
        Stopwatch sw = new Stopwatch();
        MessageEnvelope msg = new MessageEnvelope()
        {
            Header = "Test",
            Payload = new byte[32]

        };

        MessageEnvelope end = new MessageEnvelope()
        {
            Header = "Stop"
        };
        ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();

        var scert = new X509Certificate2("server.pfx", "greenpass");
        var cert = new X509Certificate2("client.pfx", "greenpass");

        SecureProtoServer server = new SecureProtoServer(20008, 100,scert);
        server.OnMessageReceived += ServerStsReceived;

        var clients = new List<SecureProtoClient>();
        for (int i = 0; i < 100; i++)
        {
            var client = new SecureProtoClient(cert);
            client.OnMessageReceived += (reply) => ClientStsReceived(client, reply);

            client.Connect("127.0.0.1", 20008);
            clients.Add(client);
        }

        Task.Run(async () =>
        {
            long elapsedPrev = sw.ElapsedMilliseconds;
            long totMsgClPrev = 0;
            long totMsgswPrev = 0;
            while (true)
            {
                await Task.Delay(2000);
                long elapsedCurr = sw.ElapsedMilliseconds;
                int deltaT = (int)(elapsedCurr - elapsedPrev);
                Console.WriteLine("Elapsed: " + elapsedCurr);
                Console.WriteLine("client total message {0} server total message {1}", totMsgCl, totMsgsw);
                float throughput = (totMsgCl - totMsgClPrev + totMsgsw - totMsgswPrev) / (deltaT / 1000);

                Console.WriteLine("Throughput :" + throughput.ToString("N1"));
                elapsedPrev = elapsedCurr;
                totMsgswPrev = totMsgsw;
                totMsgClPrev = totMsgCl;

            }

        });
        sw.Start();
        Parallel.ForEach(clients, client =>
        {
            for (int i = 0; i < 1000; i++)
            {
                client.SendAsyncMessage(msg);
            }
            //client.SendStatusMessage(end);

        });

        //var resp = clients[0].SendMessageAndWaitResponse(msg).Result;
        while (Console.ReadLine() != "e")
        {
            Console.WriteLine("client {0} server {1}", totMsgCl, totMsgsw);
        }
        Console.ReadLine();


        void ServerStsReceived(in Guid arg1, MessageEnvelope arg2)
        {
            Interlocked.Increment(ref totMsgsw);
            server.SendAsyncMessage(in arg1, arg2);
        }

        void ClientStsReceived(SecureProtoClient client, MessageEnvelope reply)
        {
            Interlocked.Increment(ref totMsgCl);
            client.SendAsyncMessage(reply);

            if (reply.Header.Equals("Stop"))
            {
                Console.WriteLine(sw.ElapsedMilliseconds);
            }
        }

       
    }
}




