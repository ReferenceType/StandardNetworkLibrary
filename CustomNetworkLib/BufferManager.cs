
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


public class BufferManager
{
    static readonly byte[] buffer = new byte[1];
    static byte[][] data ;

    static int index;
    static int offset=0;
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

    private static ConcurrentBag<int> availableIndexes = new ConcurrentBag<int>();


    public static void WriteInt32AsBytes(ref byte[] buffer, int offset, int value)
    {
        buffer[0 + offset] = (byte)value;
        buffer[1 + offset] = (byte)(value >> 8);
        buffer[2 + offset] = (byte)(value >> 16);
        buffer[3 + offset] = (byte)(value >> 24);
    }

    public static void WriteInt32AsBytes( byte[] buffer, int offset, int value)
    {
        buffer[0 + offset] = (byte)value;
        buffer[1 + offset] = (byte)(value >> 8);
        buffer[2 + offset] = (byte)(value >> 16);
        buffer[3 + offset] = (byte)(value >> 24);
    }




    public static int ReadByteFrame(byte[] buffer, int offset)
    {
        return BitConverter.ToInt32(buffer, 0);
    }


    public static void InitContigiousBuffers(int numBuffers,int bufferSize)
    {
        data = new byte[numBuffers][];

        for (int i = 0; i < numBuffers; i++)
        {
            data[i]= new byte[bufferSize];
            availableIndexes.Add(i);
        }
    }
    public static byte[] GetBuffer()
    {
        if(availableIndexes.TryTake(out var index))
        {
            return data[index];
        }
        else
        {
            throw new InvalidOperationException("No Buffer Space Available");
        }
    }

    public static void ReleaseBuffer(ref byte[] buffer)
    {
        int idx = Array.IndexOf(data, buffer);
        if (idx == -1)
            throw new InvalidOperationException("Buffer Doesnt belong here");

        availableIndexes.Add(idx);

    }
    
}

