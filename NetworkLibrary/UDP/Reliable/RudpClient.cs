///#define UseClient
#define UseServer
using NetworkLibrary.UDP.Reliable.Components;
using System;
using System.Net;
using System.Threading.Tasks;

namespace NetworkLibrary.UDP.Reliable
{
    public class RudpClient
    {
        public Action<byte[], int, int> OnReceived;

        private ReliableModule module;
#if UseServer
        private AsyncUdpServerLite server;
#endif
#if UseClient
        private AsyncUdpClient server;
#endif
        private IPEndPoint serverEp;
        private TaskCompletionSource<bool> connected = new TaskCompletionSource<bool>();
        public void Connect(string ip, int port)
        {
            var a = ConnectAsync(new System.Net.IPEndPoint(IPAddress.Parse(ip), port));
            a.ConfigureAwait(false);
            _ = a.Result;
        }
        public void Connect(IPEndPoint ep)
        {
            var a = ConnectAsync(ep);
            a.ConfigureAwait(false);
            _ = a.Result;
        }
        public Task<bool> ConnectAsync(string ip, int port)
        {
            return ConnectAsync(new System.Net.IPEndPoint(IPAddress.Parse(ip), port));
        }

        public Task<bool> ConnectAsync(IPEndPoint ep)
        {
            serverEp = ep;
#if UseServer
            server = new AsyncUdpServerLite(0);
            server.OnBytesRecieved = SocketBytesReceived;
            server.StartAsClient(ep);
#endif

#if UseClient
            server = new AsyncUdpClient();
            server.OnBytesRecieved = SocketBytesReceived2;
            server.Connect(ep);
#endif
            module = new ReliableModule(ep);
            module.OnSend = SendBytesInternal;
            module.OnReceived += HandleMessageReceived;
            module.Send(new byte[1] { 200 }, 0, 1);
            Task.Delay(10000).ContinueWith((t) =>
            {
                if (!connected.Task.IsCompleted)
                {
                    connected.TrySetException(new TimeoutException());
                    module.Close();
                }
            });
            return connected.Task;

        }

        private void SocketBytesReceived2(byte[] bytes, int offset, int count)
        {
            if (count == 2)
            {
                if (bytes[0] == 3 && bytes[1] == 2)
                {
                    Console.WriteLine("Connected");
                    connected.TrySetResult(true);
                }
            }

            module.HandleBytes(bytes, offset, count);
        }

        public void Send(byte[] buffer, int offset, int count)
        {
            module.Send(buffer, offset, count);
        }

        private void SocketBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            if (count == 2)
            {
                if (bytes[0] == 3 && bytes[1] == 2)
                {
                    Console.WriteLine("Connected");
                    connected.TrySetResult(true);
                }
            }

            module.HandleBytes(bytes, offset, count);
        }

        private void HandleMessageReceived(ReliableModule arg1, byte[] arg2, int arg3, int arg4)
        {
            OnReceived?.Invoke(arg2, arg3, arg4);
        }

        private void SendBytesInternal(ReliableModule ep, byte[] arg1, int arg2, int arg3)
        {
#if UseServer


            server.SendBytesToClient(ep.Endpoint, arg1, arg2, arg3);
#endif
#if UseClient
            server.SendAsync(arg1, arg2, arg3);
#endif
        }

        public void Disconnect()
        {
#if UseServer
            server.SendBytesToClient(serverEp, new byte[0], 0, 0);
#endif
#if UseClient
            server.SendAsync(new byte[0], 0, 0);
#endif
        }
    }
}
