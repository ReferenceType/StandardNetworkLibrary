
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
    private static byte[][] sendBuffers ;

    private static ConcurrentBag<int> availableIndexesSB = new ConcurrentBag<int>();

    private static byte[][] receiveBuffers;

    private static ConcurrentBag<int> availableIndexesRB = new ConcurrentBag<int>();


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
        return BitConverter.ToInt32(buffer, offset);
    }


    public static void InitContigiousSendBuffers(int numBuffers,int bufferSize)
    {
        sendBuffers = new byte[numBuffers][];
        availableIndexesSB= new ConcurrentBag<int>();
        for (int i = 0; i < numBuffers; i++)
        {
            sendBuffers[i]= new byte[bufferSize];
            availableIndexesSB.Add(i);
        }
    }

    public static void InitContigiousReceiveBuffers(int numBuffers, int bufferSize)
    {
        receiveBuffers = new byte[numBuffers][];
        availableIndexesRB = new ConcurrentBag<int>();
        for (int i = 0; i < numBuffers; i++)
        {
            receiveBuffers[i] = new byte[bufferSize];
            availableIndexesRB.Add(i);
        }
    }

    public static byte[] GetSendBuffer()
    {
        if(availableIndexesSB.TryTake(out var index))
        {
            return sendBuffers[index];
        }
        else
        {
            throw new InvalidOperationException("No Buffer Space Available");
        }
    }

    public static byte[] GetReceiveBuffer()
    {
        if (availableIndexesRB.TryTake(out var index))
        {
            return receiveBuffers[index];
        }
        else
        {
            throw new InvalidOperationException("No Buffer Space Available");
        }
    }

    public static void ReturnSendBuffer(ref byte[] buffer)
    {
        int idx = Array.IndexOf(sendBuffers, buffer);
        if (idx == -1)
            throw new InvalidOperationException("Buffer Doesnt belong here");

        availableIndexesSB.Add(idx);

    }

    public static void ReturnReceiveBuffer(ref byte[] buffer)
    {
        int idx = Array.IndexOf(receiveBuffers, buffer);
        if (idx == -1)
            throw new InvalidOperationException("Buffer Doesnt belong here");

        availableIndexesRB.Add(idx);

    }

    public static bool VerifyAvailableSBIndexes()
    {
        HashSet<int> availableIndexes = new HashSet<int>();
        foreach (var item in availableIndexesSB)
        {
            if (!availableIndexes.Add(item))
                return false;
        }
        return true;
    }
    public static bool VerifyAvailableRBIndexes()
    {
        HashSet<int> availableIndexes = new HashSet<int>();
        foreach (var item in availableIndexesRB)
        {
            if (!availableIndexes.Add(item))
                return false;
        }
        return true;
    }

}

