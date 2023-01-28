using NetworkLibrary.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NetworkLibrary.Utils
{
    public class SharerdMemoryStreamPool:IDisposable
    {
        private ConcurrentObjectPool<PooledMemoryStream> pool = new ConcurrentObjectPool<PooledMemoryStream>();
        private bool disposedValue;

        public PooledMemoryStream RentStream()
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(SharerdMemoryStreamPool));

                return pool.RentObject();
        }

        public void ReturnStream(PooledMemoryStream stream)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(SharerdMemoryStreamPool));

            stream.Flush();
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
                        if(pool.pool.TryTake(out var stram))
                            stram.Dispose();
                    }
                }

                pool = null;
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
