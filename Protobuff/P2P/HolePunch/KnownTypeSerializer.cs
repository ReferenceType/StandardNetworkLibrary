using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;

namespace Protobuff.P2P.HolePunch
{
    internal class KnownTypeSerializer
    {
        #region Endpoint Data
        public static void SerializeEndpointData(PooledMemoryStream stream, EndpointData data)
        {
            byte index = 0;
            // Reserve for index
            int oldPos = stream.Position32;
            stream.WriteByte(index);

            if (data.Ip !=null)
            {
                PrimitiveEncoder.WriteInt32(stream,data.Ip.Length);
                stream.Write(data.Ip, 0, data.Ip.Length);
                index = 1;
            }
            if (data.Port != 0)
            {
                PrimitiveEncoder.WriteInt32(stream, data.Port);
                index += 2;
            }

            var buf = stream.GetBuffer();
            buf[oldPos] = index;
        }
       

        public static EndpointData DeserializeEndpointData(byte[] buffer, ref int offset)
        {
            var index = buffer[offset++];

            var data = new EndpointData();
            if ((index & 1) != 0)
            {
               int len = PrimitiveEncoder.ReadInt32(buffer, ref offset);
               data.Ip = ByteCopy.ToArray(buffer,offset,len);
               offset += len;
            }


            if ((index & 1 << 1) != 0)
            {
                data.Port = PrimitiveEncoder.ReadInt32(buffer, ref offset);
            }

            return data;
        }
        public static EndpointData DeserializeEndpointData(byte[] buffer, int offset)
        {
            return DeserializeEndpointData(buffer, ref offset);
        }

        #endregion

        #region Endpoint Transfer Message

        public static void SerializeEndpointTransferMessage(PooledMemoryStream stream, EndpointTransferMessage data)
        {
            byte index = 0;
            int oldPos = stream.Position32;
            stream.WriteByte(index);

            if (data.IpRemote != null)
            {
                PrimitiveEncoder.WriteInt32(stream, data.IpRemote.Length);
                stream.Write(data.IpRemote, 0, data.IpRemote.Length);
                index = 1;
            }
            if (data.PortRemote != 0)
            {
                PrimitiveEncoder.WriteInt32(stream, data.PortRemote);
                index += 2;
            }
            if (data.LocalEndpoints != null && data.LocalEndpoints.Count>0)
            {
                PrimitiveEncoder.WriteInt32(stream, data.LocalEndpoints.Count);
                foreach (EndpointData ep in data.LocalEndpoints)
                {
                    SerializeEndpointData(stream, ep);
                }
                index += 4;
            }

            var buf = stream.GetBuffer();
            buf[oldPos] = index;
        }


        public static EndpointTransferMessage DeserializeEndpointTransferMessage(byte[] buffer, int offset)
        {
            var index = buffer[offset++];

            var data = new EndpointTransferMessage();
            if ((index & 1) != 0)
            {
                int len = PrimitiveEncoder.ReadInt32(buffer, ref offset);
                data.IpRemote = ByteCopy.ToArray(buffer, offset, len);
                offset += len;
            }


            if ((index & 1 << 1) != 0)
            {
                data.PortRemote = PrimitiveEncoder.ReadInt32(buffer, ref offset);
            }

            if ((index & 1 << 2) != 0)
            {
                int listCount = PrimitiveEncoder.ReadInt32(buffer, ref offset);
                data.LocalEndpoints = new List<EndpointData>(listCount+1);
                for (int i = 0; i < listCount; i++)
                {
                    data.LocalEndpoints.Add(DeserializeEndpointData(buffer, ref offset));
                }
            }

            return data;
        }
        #endregion

        #region Peer Info
        public static void SerializePeerInfo(PooledMemoryStream stream, PeerInfo data)
        {
            byte index = 0;
            // Reserve for index
            int oldPos = stream.Position32;
            stream.WriteByte(index);

            if (data.Address != null)
            {
                PrimitiveEncoder.WriteInt32(stream, data.Address.Length);
                stream.Write(data.Address, 0, data.Address.Length);
                index = 1;
            }
            if (data.Port != 0)
            {
                stream.WriteUshort(data.Port);
                index += 2;
            }

            var buf = stream.GetBuffer();
            buf[oldPos] = index;
        }


        public static PeerInfo DeserializePeerInfo(byte[] buffer, ref int offset)
        {
            var index = buffer[offset++];

            var data = new PeerInfo();
            if ((index & 1) != 0)
            {
                int len = PrimitiveEncoder.ReadInt32(buffer, ref offset);
                data.Address = ByteCopy.ToArray(buffer, offset, len);
                offset += len;
            }


            if ((index & 1 << 1) != 0)
            {
                data.Port = BitConverter.ToUInt16(buffer, offset);
                offset+= 2;
            }

            return data;
        }
        #endregion

        #region PeerList
        public static void SerializePeerList(PooledMemoryStream stream, PeerList data)
        {
            byte index = 0;
            // Reserve for index
            int oldPos = stream.Position32;
            stream.WriteByte(index);

            if (data.PeerIds != null && data.PeerIds.Count>0)
            {
                PrimitiveEncoder.WriteInt32(stream, data.PeerIds.Count);
                foreach (var item in data.PeerIds)
                {
                    PrimitiveEncoder.WriteGuid(stream, item.Key);
                    SerializePeerInfo(stream, item.Value);
                }
                index = 1;
            }
          

            var buf = stream.GetBuffer();
            buf[oldPos] = index;
        }

        public static PeerList DeserializePeerList(byte[] buffer,int offset)
        {
            var index = buffer[offset++];
            PeerList data = new PeerList();
            if ((index & 1) != 0)
            {
                int count = PrimitiveEncoder.ReadInt32(buffer, ref offset);
                var dict =  new Dictionary<Guid,PeerInfo>(count);
                for (int i = 0; i < count; i++)
                {
                    var key = PrimitiveEncoder.ReadGuid(buffer, ref offset);
                    var value = DeserializePeerInfo(buffer, ref offset);
                    dict.Add(key, value);
                }
                data.PeerIds = dict;

            }
            else
                data.PeerIds =  new Dictionary<Guid, PeerInfo>();
            return data;
        }
        #endregion
        public static void SerializeRoomPeerList(PooledMemoryStream stream, RoomPeerList roomPeerList)
        {
            byte index = 0;
            int oldPos = stream.Position32;
            stream.WriteByte(index);

            if (roomPeerList.RoomName != null)
            {
                PrimitiveEncoder.WriteStringUtf8(stream, roomPeerList.RoomName);
                index = 1;
            }

            if (roomPeerList.Peers != null)
            {
                SerializePeerList(stream, roomPeerList.Peers);
                index += 2;
            }


            var buf = stream.GetBuffer();
            buf[oldPos] = index;
        }
        public static RoomPeerList DeserializeRoomPeerList(byte[] buffer, int offset)
        {
            var index = buffer[offset++];
            RoomPeerList peerList = new RoomPeerList();
            if ((index & 1) != 0)
            {
                peerList.RoomName = PrimitiveEncoder.ReadStringUtf8(buffer, ref offset);
            }
            if ((index & 1 << 1) != 0)
            {
                peerList.Peers = DeserializePeerList(buffer, offset);
            }
            return peerList;
        }
    }
}
