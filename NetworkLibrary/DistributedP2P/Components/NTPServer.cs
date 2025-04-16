using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class NTPServer : IDisposable
    {
        Socket udpListener;
        Stopwatch sw;

        [ThreadStatic]
        static byte[] buff = new byte[9];

        int IsDisposed = 0;

        public NTPServer(int port, Stopwatch clock)
        {
            udpListener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpListener.Bind(new IPEndPoint(IPAddress.Any, port));
            sw = clock;
        }



        internal void Start()
        {
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                Receive(GetReceiveArgs());
            }
        }

        private void Receive(SocketAsyncEventArgs sea)
        {
            if (!udpListener.ReceiveFromAsync(sea))
            {
                ThreadPool.UnsafeQueueUserWorkItem(_ => Received(null, sea), null);
            }
        }

        private void Received(object _, SocketAsyncEventArgs e)
        {
            while (true) 
            {
                try
                {

                    if (e.SocketError != SocketError.Success)
                    {
                        e.Dispose();
                        Receive(GetReceiveArgs());
                        return;
                    }

                    var time = sw.Elapsed.TotalMilliseconds;
                    var buff = GetBuffer();
                    buff[0] = e.Buffer[0];
                    PrimitiveEncoder.WriteFixedDouble(buff, 1, time);

                    udpListener.SendTo(buff, 0, buff.Length, SocketFlags.None, e.RemoteEndPoint);

                    var ep = (IPEndPoint)e.RemoteEndPoint;
                    ep.Address = IPAddress.Any;
                    ep.Port = 0;
                    if (udpListener.ReceiveFromAsync(e))
                    {
                        return; 
                    }
                }
                catch (Exception ex)
                {
                    if (Interlocked.CompareExchange(ref IsDisposed, 0, 0) == 0)
                    {
                        Log(ex.Message + "\n" + ex.StackTrace);
                        e.Dispose();
                        Receive(GetReceiveArgs());
                        return;
                    }
                    else
                    {
                        e.Dispose();
                        return;
                    }
                }

            }


        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte[] GetBuffer()
        {
            if (buff == null)
            {
                buff = new byte[9];
            }
            return buff;
        }

        private SocketAsyncEventArgs GetReceiveArgs()
        {

            var recArgs = new SocketAsyncEventArgs();
            recArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            recArgs.Completed += Received;
            recArgs.SetBuffer(new byte[65555], 0, 65555);


            return recArgs;
        }

        private void Log(string v)
        {
            Console.WriteLine(v);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref IsDisposed, 1, 0) == 0)
            {
                udpListener.Dispose();
            }
        }
    }
}
