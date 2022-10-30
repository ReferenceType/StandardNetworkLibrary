using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary.Components
{
    internal interface IMessageProcessQueue
    {
        /// <summary>
        /// Enqueues the message if there is enough space available
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>true if message is enqueued.</returns>
        bool TryEnqueueMessage(byte[] bytes);
        bool TryEnqueueMessage(byte[] bytes, int offset,int count);

        /// <summary>
        /// Flushes the queue if there is anything to flush.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="amountWritten"></param>
        /// <returns>true if something succesfully flushed.</returns>
        bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten);

        /// <summary>
        /// Is Queue empty
        /// </summary>
        /// <returns></returns>
        bool IsEmpty();

        int CurrentIndexedMemory { get; }
        long TotalMessageDispatched { get; }
    }
}
