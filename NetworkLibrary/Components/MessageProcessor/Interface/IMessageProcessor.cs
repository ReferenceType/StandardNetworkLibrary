using System;

namespace NetworkLibrary.Components
{
    public interface IMessageProcessor : IDisposable
    {
        // heldover

        /// <summary>
        /// Sets a buffer where the messages will be proccessed into
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        void SetBuffer(ref byte[] buffer, int offset);

        /// <summary>
        /// Proccesses a given message into a buffer set by <see cref="SetBuffer"/>
        /// </summary>
        /// <param name="message"></param>
        /// <returns>true if message is completely process, false means message is partially processed and its heldover,
        /// indicates flush is required returns>
        bool ProcessMessage(byte[] message);

        /// <summary>
        /// Flushes a heldover message into buffer set by <see cref="SetBuffer"/>.
        /// </summary>
        /// <returns>true if heldover message is completel flushed, false if the messages isnt fully processed.</returns>
        bool Flush();

        /// <summary>
        /// Returns the buffer set by <see cref="SetBuffer"/>
        /// </summary>
        /// <param name="Buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        void GetBuffer(out byte[] Buffer, out int offset, out int count);


        bool IsHoldingMessage { get; }

    }
}