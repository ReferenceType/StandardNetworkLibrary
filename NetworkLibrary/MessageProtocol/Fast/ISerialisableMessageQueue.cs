﻿using MessageProtocol;
using MessageProtocol.Serialization;
using Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.MessageProtocol
{
    internal interface ISerialisableMessageQueue
    {
        bool TryEnqueueMessage<T>(MessageEnvelope envelope, T message);
        bool TryEnqueueMessage(MessageEnvelope envelope);
    }
}
