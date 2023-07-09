
/* Unmerged change from project 'NetworkLibrary (net6.0)'
Before:
using System;
After:
using NetworkLibrary.UDP.Reliable.Components;
using System;
*/

/* Unmerged change from project 'NetworkLibrary (net7.0)'
Before:
using System;
After:
using NetworkLibrary.UDP.Reliable.Components;
using System;
*/

/* Unmerged change from project 'NetworkLibrary (netstandard2.0)'
Before:
using System;
After:
using NetworkLibrary.UDP.Reliable.Components;
using System;
*/
using NetworkLibrary.UDP.Reliable.Components;
using System;

/* Unmerged change from project 'NetworkLibrary (net6.0)'
Before:
using System.Runtime.Serialization;
using NetworkLibrary.UDP.Reliable.Components;
After:
using System.Runtime.Serialization;
*/

/* Unmerged change from project 'NetworkLibrary (net7.0)'
Before:
using System.Runtime.Serialization;
using NetworkLibrary.UDP.Reliable.Components;
After:
using System.Runtime.Serialization;
*/

/* Unmerged change from project 'NetworkLibrary (netstandard2.0)'
Before:
using System.Runtime.Serialization;
using NetworkLibrary.UDP.Reliable.Components;
After:
using System.Runtime.Serialization;
*/
using System.Collections.Concurrent;
using System.Net;

namespace NetworkLibrary.UDP.Reliable
{
    public class RudpServer
    {
        public Action<IPEndPoint> OnAccept;
        public Action<IPEndPoint> OnDisconnect;
        public Action<IPEndPoint, byte[], int, int> OnReceived;

        private AsyncUdpServerLite server;

        ConcurrentDictionary<IPEndPoint, ReliableModule> clients = new ConcurrentDictionary<IPEndPoint, ReliableModule>();
        public RudpServer(AsyncUdpServerLite server)
        {
            this.server = server;
            Init(server);
        }
        public RudpServer(int port)
        {
            server = new AsyncUdpServerLite(port);
            Init(server);
        }

        private void Init(AsyncUdpServerLite server)
        {
            server.OnBytesRecieved = BytesReceived;
        }
        public void StartServer()
        {
            server.StartServer();
        }

        public void Send(IPEndPoint ep, byte[] data, int offset, int count)
        {
            if (clients.TryGetValue(ep, out var rm))
            {
                rm.Send(data, offset, count);
            }
        }
        public void Send(IPEndPoint ep, Segment first, Segment second)
        {
            if (clients.TryGetValue(ep, out var rm))
            {
                rm.Send(first, second);
            }
        }

        private void SendBytesInternal(ReliableModule rm, byte[] arg1, int arg2, int arg3)
        {
            server.SendBytesToClient(rm.Endpoint, arg1, arg2, arg3);
        }

        private void BytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            if (!clients.TryGetValue(adress, out ReliableModule module))
            {
                if (count == 4 && bytes[3] == 200)
                    Register(adress, bytes, offset, count);
            }
            else
            {
                if (count == 0)
                {
                    HandleDisconnect(module);
                    return;
                }

                module.HandleBytes(bytes, offset, count);
            }

        }

        private void Register(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            var rm = new ReliableModule(adress);

            if (clients.TryAdd(adress, rm))
            {
                rm.OnSend = SendBytesInternal;
                rm.HandleBytes(bytes, offset, count);
                rm.OnReceived += HandleMessageReceived;

                OnAccept?.Invoke(adress);
            }

        }

        private void HandleMessageReceived(ReliableModule rm, byte[] bytes, int offset, int count)
        {
            if (count == 0)
            {
                HandleDisconnect(rm);
                return;
            }
            else if (count == 1 && bytes[offset] == 200)
            {
                return;
            }
            else if (clients.ContainsKey(rm.Endpoint))
                OnReceived?.Invoke(rm.Endpoint, bytes, offset, count);

        }

        private void HandleDisconnect(ReliableModule rm)
        {
            if (clients.TryRemove(rm.Endpoint, out _))
            {
                rm.Close();
                Console.WriteLine("Client disconnected");
            }
        }


    }
}
