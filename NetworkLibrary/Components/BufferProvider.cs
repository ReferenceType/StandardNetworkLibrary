
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// This class provides contigious send and receive buffer, this was to optimise GC because the send and receive buffers are pinned
/// by the WSA calls.
/// </summary>
public class BufferProvider:IDisposable
{
    private  byte[][] sendBuffers ;

    private  ConcurrentBag<int> availableIndexesSB = new ConcurrentBag<int>();

    private  byte[][] receiveBuffers;

    private  ConcurrentBag<int> availableIndexesRB = new ConcurrentBag<int>();

    private List<GCHandle> pinHandels = new List<GCHandle>();

    private int disposedStatus = 0;

    internal BufferProvider(int numSendBuffers, int sendBufSizes,int numRecvBuffers,int recvBufSizes)
    {
        InitContigiousReceiveBuffers(numRecvBuffers, recvBufSizes);
        InitContigiousSendBuffers(numSendBuffers, sendBufSizes);
        
    }

    public bool IsExhausted()
    {
        return availableIndexesRB.Count < 1 && availableIndexesSB.Count < 1;
    }


    private void InitContigiousSendBuffers(int numBuffers,int bufferSize)
    {
        sendBuffers = new byte[numBuffers][];
        availableIndexesSB= new ConcurrentBag<int>();
        for (int i = 0; i < numBuffers; i++)
        {
            var bufer = new byte[bufferSize];
            //var handle = GCHandle.Alloc(bufer, GCHandleType.Pinned);
            //pinHandels.Add(handle);

            sendBuffers[i]= bufer;
            availableIndexesSB.Add(i);
        }
    }

    private void InitContigiousReceiveBuffers(int numBuffers, int bufferSize)
    {
        receiveBuffers = new byte[numBuffers][];
        availableIndexesRB = new ConcurrentBag<int>();
        for (int i = 0; i < numBuffers; i++)
        {
            var bufer = new byte[bufferSize];
            //var handle = GCHandle.Alloc(bufer, GCHandleType.Pinned);
            //pinHandels.Add(handle);

            receiveBuffers[i] = bufer;
            availableIndexesRB.Add(i);
        }
    }

    public byte[] GetSendBuffer()
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

    public byte[] GetReceiveBuffer()
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

    public void ReturnSendBuffer(ref byte[] buffer)
    {
        int idx = Array.IndexOf(sendBuffers, buffer);
        if (idx == -1)
            throw new InvalidOperationException("Buffer Doesnt belong here");

        availableIndexesSB.Add(idx);

    }

    public void ReturnReceiveBuffer(ref byte[] buffer)
    {
        int idx = Array.IndexOf(receiveBuffers, buffer);
        if (idx == -1)
            throw new InvalidOperationException("Buffer Doesnt belong here");

        availableIndexesRB.Add(idx);
    }

    

    public bool VerifyAvailableSBIndexes()
    {
        HashSet<int> availableIndexes = new HashSet<int>();
        foreach (var item in availableIndexesSB)
        {
            if (!availableIndexes.Add(item))
                return false;
        }
        return true;
    }
    public bool VerifyAvailableRBIndexes()
    {
        HashSet<int> availableIndexes = new HashSet<int>();
        foreach (var item in availableIndexesRB)
        {
            if (!availableIndexes.Add(item))
                return false;
        }
        return true;
    }

    public void Dispose()
    {
        if(Interlocked.CompareExchange(ref disposedStatus, 1, 0) == 0)
        {
            foreach (var handle in pinHandels)
            {
                handle.Free();
            }
        }
        
    }
}

