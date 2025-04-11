using System;
using System.Threading.Tasks;
using NetworkLibrary.DistributedP2P.Client;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{

    public interface IChannel
    {
        event Action<byte[], int, int> OnBytesReceived;

        event Action OnDisconnected;

        /// <summary>
        /// Information about channel type
        /// </summary>
        ChannelInfo Info { get; }

        /// <summary>
        /// Starts data receiver.
        /// Start must be called after all event subscription is made
        /// </summary>
        void Start();

        /// <summary>
        /// Sends buytes to destionation asyncronusly.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        void Send(byte[] buffer, int offset, int count);

        /// <summary>
        /// Pings the channel, -1 means ping failed.
        /// </summary>
        /// <returns></returns>
        Task<double> Ping();

        /// <summary>
        /// Closes the channel and releases resources.
        /// </summary>
        void CloseChannel();

       

    }
}