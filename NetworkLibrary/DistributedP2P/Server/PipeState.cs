using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Server
{
    internal class PipeState<T>
    {

        internal List<T> Clients = new List<T>();
        internal PipeToken pipeData;
        private object mtex = new object();

        public PipeState(PipeToken pipeData)
        {
            this.pipeData = pipeData;
        }

        internal bool IsComplete()
        {
            lock (mtex)
                return Clients.Count > 1;
        }

        internal void RegisterClient(T guid)
        {
            lock (mtex)
                Clients.Add(guid);
        }
    }
}
