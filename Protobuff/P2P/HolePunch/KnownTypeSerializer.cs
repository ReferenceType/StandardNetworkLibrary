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
    //[ProtoContract]
    //internal class ChanneCreationMessage : IProtoMessage
    //{
    //    [ProtoMember(1)]
    //    public byte[] SharedSecret { get; set; }
    //    [ProtoMember(2)]
    //    public Guid RegistrationId { get; set; }
    //    [ProtoMember(3)]
    //    public Guid DestinationId { get; set; }

    //    [ProtoMember(4)]
    //    [DefaultValue(true)]
    //    public bool Encrypted { get; set; } = true;

    //}

    //[ProtoContract]
    //internal class EndpointTransferMessage : IProtoMessage
    //{
    //    [ProtoMember(1)]
    //    public string IpRemote { get; set; }
    //    [ProtoMember(2)]
    //    public int PortRemote { get; set; }
    //    [ProtoMember(3)]
    //    public List<EndpointData> LocalEndpoints { get; set; } = new List<EndpointData>();
    //}


    //[ProtoContract]
    //public class EndpointData
    //{
    //    [ProtoMember(1)]
    //    public string Ip;
    //    [ProtoMember(2)]
    //    public int Port;
    //}


    //[ProtoContract]
    //public class PeerInfo : IProtoMessage
    //{
    //    [ProtoMember(1)]
    //    public byte[] Address { get; set; }
    //    [ProtoMember(2)]
    //    public ushort Port { get; set; }
    //}

    //[ProtoContract]
    //public class PeerList : IProtoMessage
    //{
    //    [ProtoMember(1)]
    //    public Dictionary<Guid, PeerInfo> PeerIds { get; set; }

    //}

    internal class KnownTypeSerializer
    {
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

        public static void SerializeChannelCreationMessage(PooledMemoryStream stream, ChanneCreationMessage data)
        {
            byte index = 0;
            int oldPos = stream.Position32;
            stream.WriteByte(index);

            if (data.SharedSecret != null)
            {
                PrimitiveEncoder.WriteInt32(stream, data.SharedSecret.Length);
                stream.Write(data.SharedSecret, 0, data.SharedSecret.Length);
                index = 1;
            }
            if (data.RegistrationId != Guid.Empty)
            {
                PrimitiveEncoder.WriteGuid(stream, data.RegistrationId);
                index += 2;
            }
            if (data.DestinationId != Guid.Empty)
            {
                PrimitiveEncoder.WriteGuid(stream, data.DestinationId);
                index += 4;
            }
            if (data.Encrypted)
            {
                index += 8;
            }
            var buf = stream.GetBuffer();
            buf[oldPos] = index;
        }

        public static ChanneCreationMessage DeserializeChanneCreationMessage(byte[] buffer, int offset)
        {
            var index = buffer[offset++];

            var data = new ChanneCreationMessage();
            if ((index & 1) != 0)
            {
                int arrayLenght = PrimitiveEncoder.ReadInt32(buffer, ref offset);
                data.SharedSecret = ByteCopy.ToArray(buffer, offset, arrayLenght);
                offset+= arrayLenght;
            }


            if ((index & 1 << 1) != 0)
            {
                data.RegistrationId = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            }

            if ((index & 1 << 2) != 0)
            {
                data.DestinationId = PrimitiveEncoder.ReadGuid(buffer, ref offset);

            }
            if ((index & 1 << 3) != 0)
            {
                data.Encrypted = true;

            }

            return data;
        }

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
    }
}
