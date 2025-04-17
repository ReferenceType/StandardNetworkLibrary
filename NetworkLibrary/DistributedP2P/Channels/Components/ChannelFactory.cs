using NetworkLibrary.DistributedP2P.Client.StateManagement;
using NetworkLibrary.DistributedP2P.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using NetworkLibrary.Components.Crypto.KeyDerivation;
using NetworkLibrary.Components.Crypto;
using NetworkLibrary.TCP.AES;
using NetworkLibrary.DistributedP2P.Components;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{
    internal class ChannelFactory
    {
        public static IChannel CreateChannel(ClientSimultaneousTcpHolepunchState TcpHpstate, bool isInitiator, ILogger logger)
        {
            ChannelInfo info = TcpHpstate.ChannelInfo;
            Socket connectedSocket = TcpHpstate.Socket;
            byte[] sharedSecret = TcpHpstate.SharedSecret;
            IPEndPoint endpoint = TcpHpstate.SuccesfulEndpoint;
            ChannelType channelType = TcpHpstate.ChannelInfo.ChannelType;

            return CreateChannel(info, connectedSocket, sharedSecret, endpoint, channelType, isInitiator, logger);
        }

        public static IChannel CreateChannel(ClientSequentialTcpHolepunchState TcpHpstate, bool isInitiator, ILogger logger)
        {
            ChannelInfo info = TcpHpstate.ChannelInfo;
            Socket connectedSocket = TcpHpstate.Socket;
            byte[] sharedSecret = TcpHpstate.SharedSecret;
            IPEndPoint endpoint = TcpHpstate.SuccesfulEndpoint;
            ChannelType channelType = TcpHpstate.ChannelInfo.ChannelType;

            return CreateChannel(info, connectedSocket, sharedSecret, endpoint, channelType, isInitiator, logger);
        }

        public static IChannel CreateChannel(ClientUdpHolepunchState udpHpstate, bool isInitiator, ILogger logger)
        {
            ChannelInfo info = udpHpstate.ChannelInfo;
            Socket connectedSocket = udpHpstate.Socket;
            byte[] sharedSecret = udpHpstate.SharedSecret;
            IPEndPoint endpoint = udpHpstate.SuccesfulEndpoint;
            ChannelType channelType = udpHpstate.ChannelInfo.ChannelType;

            return CreateChannel(info, connectedSocket, sharedSecret, endpoint, channelType, isInitiator, logger);
        }

        public static IChannel CreateChannel(ClientPipeState pipeState, bool isInitiator, ILogger logger)
        {
            ChannelInfo info = pipeState.ChannelInfo;
            Socket connectedSocket = pipeState.ConnectedSocket;
            byte[] sharedSecret = pipeState.sharedSecret;
            IPEndPoint endpoint = pipeState.SuccesfullEndpoint.ToIpEndpoint();
            ChannelType channelType = pipeState.ChannelInfo.ChannelType;

            return CreateChannel(info, connectedSocket, sharedSecret, endpoint, channelType, isInitiator, logger);
        }

        public static IChannel CreateChannel(ChannelInfo info, Socket connectedSocket, byte[] sharedSecret, IPEndPoint endpoint, ChannelType channelType, bool isInitiator, ILogger logger)
        {
            IChannel channel = null;

            switch (channelType)
            {

                case ChannelType.Tcp:
                    channel = new TcpChannel(info, connectedSocket, logger);
                    break;
                case ChannelType.SecureTcp:
                    var symetricKey = HKDFLite.DeriveKey(sharedSecret, outputLength: 16);
                    var algo = AesManager.Create(AesMode.GCM, symetricKey, HKDFLite.DeriveKey(info.ChannelName,outputLength:16) );
                    channel = new SecureTcpChannel(algo, info, connectedSocket, isInitiator, logger);
                    break;
                case ChannelType.Udp:
                    channel = new UdpChannel(connectedSocket, endpoint, info, logger);

                    break;
                case ChannelType.SecureUdp:
                    var symetricKey2 = HKDFLite.DeriveKey(sharedSecret, outputLength: 16);
                    var algo2 = AesManager.Create(AesMode.GCM, symetricKey2, HKDFLite.DeriveKey(info.ChannelName,outputLength: 16));
                    channel = new SecureUdpChannel(connectedSocket, endpoint, algo2, info, isInitiator, logger);

                    break;
            }

            return channel;
        }
    }
}
