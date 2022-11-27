using NetworkLibrary.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NetworkLibrary.Utils
{
    public class MemoryStreamPool : IDisposable
    {
        ConcurrentObjectPool<MemoryStream> pool = new ConcurrentObjectPool<MemoryStream>();
        private bool disposedValue;

        public MemoryStream RentStream()
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(MemoryStreamPool));

            return pool.RentObject();
        }

        public void ReturnStream(MemoryStream stream)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(MemoryStreamPool));

            stream.Position = 0;
            pool.ReturnObject(stream);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    for (int i = 0; i < pool.pool.Count; i++)
                    {
                        if (pool.pool.TryTake(out var stram))
                            stram.Dispose();
                    }
                }

                pool = null;
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
