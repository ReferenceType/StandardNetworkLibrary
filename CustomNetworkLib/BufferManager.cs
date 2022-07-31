
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


class BufferManager
{
    /// <summary>
    /// The last leftover amount from prevoius read.
    /// </summary>
    public int LastLeftover;

    /// <summary>
    /// The leftover frame bytes
    /// Socket reciever consume this to stich the buffers. 
    /// </summary>
    public byte[] LeftoverFrameBytes;

    private static int byteFrameLength = 4;

    private object messageParserLocker = new object();

    /// <summary>
    /// Gets the last message from stream of bytes.
    /// </summary>
    /// <param name="recievedBytes">The recieved bytes.</param>
    /// <returns></returns>
    public byte[] GetLastMessageFromBytes(byte[] recievedBytes)
    {
        try
        {
            lock (messageParserLocker)
            {
                if (LastLeftover == 0)
                {
                    // means there is no message in the buffer, its fragmented.
                    int msgLength1 = ReadByteFrame(recievedBytes, 0);
                    if (msgLength1 > recievedBytes.Length + 4)
                    {
                        return null;
                    }
                    // its an atomic message no more no less
                    else if (msgLength1 == recievedBytes.Length + 4)
                    {
                        return recievedBytes;
                    }
                }
                // skip the leftover bytes from previous read and reset
                recievedBytes = recievedBytes.Skip(LastLeftover).ToArray();
                LastLeftover = 0;

                int msgLength = ReadByteFrame(recievedBytes, 0);
                byte[] lastChunk = null;
                int offset = 0;

                // read messages 1 by 1 using the byteframes, obtain the last one
                while (true)
                {
                    // skip by offset each time to continue on next msg.
                    var chunk = recievedBytes.Skip(offset).Take(msgLength + 4).ToArray();
                    // we found a message store it
                    if (chunk.Length == msgLength + 4)
                    {
                        lastChunk = chunk;
                    }
                    else
                    {
                        // end of messages, store the leftover to indicate next read we will have to skip, 
                        // the remaining* part of the message
                        LastLeftover = Math.Abs(msgLength - chunk.Length) + 4;
                        break;
                    }
                    // move the offset my message length + header
                    offset += msgLength + 4;
                    if (offset == recievedBytes.Length)
                    {
                        LastLeftover = 0;
                        break;
                    }

                    // here we hit when byteframe is also fragmented. :(
                    // dont read length
                    // save last bytes(up to 3) so we can do concat afteron next message. we need all 4 to see the lenght of incoming message
                    if (recievedBytes.Length - offset < 4)
                    {
                        LeftoverFrameBytes = new byte[recievedBytes.Length - offset];
                        Buffer.BlockCopy(recievedBytes, offset, LeftoverFrameBytes, 0, recievedBytes.Length - offset);
                        LastLeftover = 0;
                        lastChunk = chunk;
                        //lastChunk = lastChunk.Take(lastChunk.Length - (recievedBytes.Length - offset)).ToArray() ;
                        break;
                    }

                    msgLength = ReadByteFrame(recievedBytes, offset);
                    // i dojnt remember
                    if (chunk != null || chunk.Length > 0)
                        lastChunk = chunk;
                }
                return lastChunk.Skip(4).Take(lastChunk.Length).ToArray();
            }
        }
        catch (Exception e)
        {
            //Logger.LogError("While parsing the message buffer to obtain the last one something went wrong : " + e.Message);
            return null;
        }



    }

    /// <summary>
    /// Reads the byte frame from headder to get expected message size.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <returns></returns>
    public static int ReadByteFrame(byte[] buffer, int offset)
    {
        byte[] frame = new byte[byteFrameLength];
        Buffer.BlockCopy(buffer, offset, frame, 0, byteFrameLength);
        return BitConverter.ToInt32(frame, 0);
    }

    /// <summary>
    /// Adds byte frame to a byte message.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <returns></returns>
    public static byte[] AddByteFrame(byte[] buffer)
    {
        byte[] byteFrame = new byte[byteFrameLength];
        byteFrame = BitConverter.GetBytes(buffer.Length);
        return CombineByteArrays(byteFrame, buffer);
    }

    /// <summary>
    /// Removes the byte frame from framed message.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="frameSize">Size of the frame.</param>
    /// <returns></returns>
    public static byte[] RemoveByteFrame(byte[] buffer, out int frameSize)
    {
        byte[] frame = new byte[byteFrameLength];
        Buffer.BlockCopy(buffer, 0, frame, 0, byteFrameLength);
        frameSize = BitConverter.ToInt32(frame, 0);

        return buffer.Skip(byteFrameLength).ToArray();
    }

    /// <summary>
    /// Combines the byte arrays.
    /// </summary>
    /// <param name="first">The first.</param>
    /// <param name="second">The second.</param>
    /// <returns></returns>
    public static byte[] CombineByteArrays(byte[] first, byte[] second)
    {
        byte[] ret = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, ret, 0, first.Length);
        Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
        return ret;

    }

    /// <summary>
    /// Enumerable chunker which get a spesified amount of chunks from source byte array on each call
    /// </summary>
    /// <param name="source"></param>
    /// <param name="chunkSize"></param>
    /// <returns></returns>
    // call it like foreach(var chunk in GetNextChunk())
    public static IEnumerable<byte[]> GetNextChunk(byte[] source, int chunkSize)
    {
        for (var i = 0; i < source.Length; i += chunkSize)
        {
            // If you are end of the array and remanining is less than chunk size,
            // you will recieve the remainder not padded 0's.
            yield return source.Skip(i).Take(chunkSize).ToArray();
        }
    }

    #region Debug
    //// Test this buffer manager
    //// keeping here for future debug
    //internal void TestYourself()
    //{
    //    byte[] testBytes = new byte[200];
    //    byte[] testMsgs = new byte[72];
    //    byte[] testMsgs1 = new byte[65];
    //    byte[] testMsgs2 = new byte[55];
    //    var msg1 = AddByteFrame(testMsgs);
    //    var msg2 = AddByteFrame(testMsgs1);
    //    var msg3 = AddByteFrame(testMsgs);
    //    var msg4 = AddByteFrame(testMsgs2);
    //    var c1 = CombineByteArrays(msg1, msg2);
    //    var c2 = CombineByteArrays(msg3, msg4);
    //    var c3 = CombineByteArrays(c1, c2);

    //    var c4 = CombineByteArrays(c3, CombineByteArrays(BitConverter.GetBytes(82), new byte[39]));

    //    var ret = GetLastMessageFromBytes(c4);
    //    var r2 = GetLastMessageFromBytes(msg1);
    //}
    #endregion
}

