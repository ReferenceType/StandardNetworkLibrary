using MessageProtocol;
using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.Utils;
using ProtoBuf;
using Protobuff.Components.Serialiser;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;

namespace Protobuff
{
    public class ConcurrentProtoSerialiser:GenericMessageSerializer<ProtoSerializer>
    { }
  
}

