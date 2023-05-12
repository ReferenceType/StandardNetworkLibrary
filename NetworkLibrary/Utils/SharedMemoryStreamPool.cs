using NetworkLibrary.Components;
using System;

namespace NetworkLibrary.Utils
{
    public class SharerdMemoryStreamPool : IDisposable
    {
        private ConcurrentObjectPool<PooledMemoryStream> pool = new ConcurrentObjectPool<PooledMemoryStream>();
        private static ConcurrentObjectPool<PooledMemoryStream> poolStatic = new ConcurrentObjectPool<PooledMemoryStream>();
        private bool disposedValue;

        public PooledMemoryStream RentStream()
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(SharerdMemoryStreamPool));

            return pool.RentObject();
        }

        public static PooledMemoryStream RentStreamStatic()
        {
            return poolStatic.RentObject();
        }

        public void ReturnStream(PooledMemoryStream stream)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(SharerdMemoryStreamPool));

            stream.Clear();
            pool.ReturnObject(stream);
        }

        public static void ReturnStreamStatic(PooledMemoryStream stream)
        {
            stream.Clear();
            poolStatic.ReturnObject(stream);
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
