using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NetworkLibrary.Utils
{
    public class MemoryStreamPool
    {
        ConcurrentObjectPool<MemoryStream> pool = new ConcurrentObjectPool<MemoryStream>();

        public MemoryStream RentStream()
        {
            return pool.RentObject();
        }

        public void ReturnStream(MemoryStream stream)
        {
            stream.Position = 0;
            pool.ReturnObject(stream);
        }
        
    }
}
