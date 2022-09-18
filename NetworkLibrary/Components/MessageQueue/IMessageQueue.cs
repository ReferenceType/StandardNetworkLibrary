using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.Components.MessageQueue
{
    internal interface IMessageQueue
    {
        bool TryEnqueueMessage(byte[] bytes);
        bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten);
        bool IsEmpty();
    }
}
