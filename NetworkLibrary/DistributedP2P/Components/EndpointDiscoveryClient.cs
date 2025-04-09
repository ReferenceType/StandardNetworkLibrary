using NetworkLibrary.P2P.Components.HolePunch;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class EndpointDiscoveryClient
    {
        static byte[] dummy = new byte[1];

        public static async Task<EndpointData> GetTcpPublicEndpoint(Socket socket, IPEndPoint whereToAsk, int timeoutMs)
        {
            var retrieveTask = GetPublicEndpointTcp(socket, whereToAsk);
            var result = await Task.WhenAny(retrieveTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (result == retrieveTask)
            {
                if (!retrieveTask.IsFaulted)
                    return await retrieveTask;
                return null;
            }
            else
            {
                return null;
            }
        }

        public static async Task<EndpointData> GetUdpPublicEndpoint(Socket socket, IPEndPoint whereToAsk, int timeoutMs)
        {
            var retrieveTask = GetPublicEndpointUdp(socket, whereToAsk);
            var result = await Task.WhenAny(retrieveTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (result == retrieveTask)
            {
                if (!retrieveTask.IsFaulted)
                    return await retrieveTask;
                return null;
            }
            else
            {
                return null;
            }
        }


        private static async Task<EndpointData> GetPublicEndpointTcp(Socket socket, IPEndPoint whereToAsk)
        {
         
                await socket.ConnectAsync(whereToAsk).ConfigureAwait(false);
                var buff = BufferPool.RentBuffer(1024);
                int received = await socket.ReceiveAsync(new ArraySegment<byte>(buff), SocketFlags.None).ConfigureAwait(false);


                if (received == 0)
                {
                    throw new Exception("No data received");
                }

                BufferPool.ReturnBuffer(buff);

                return KnownTypeSerializer.DeserializeEndpointData(buff, 0);
            
        }

        private static async Task<EndpointData> GetPublicEndpointUdp(Socket socket, IPEndPoint whereToAsk)
        {
            await socket.SendToAsync(new ArraySegment<byte>(dummy), SocketFlags.None, whereToAsk).ConfigureAwait(false);
            var buff = BufferPool.RentBuffer(1024);

            await socket.ReceiveFromAsync(new ArraySegment<byte>(buff), SocketFlags.None, whereToAsk).ConfigureAwait(false);
            BufferPool.ReturnBuffer(buff);

            return KnownTypeSerializer.DeserializeEndpointData(buff, 0);
            
        }
    }
}
