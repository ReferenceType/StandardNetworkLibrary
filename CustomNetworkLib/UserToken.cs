using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    class UserToken
    {
        //public Socket ClientSocket { get; }
        public AutoResetEvent OperationPending = new AutoResetEvent(true);
        public SemaphoreSlim OperationSemaphore = new SemaphoreSlim(1,1);
        public bool recursing;
        public Guid Guid;

        public UserToken()
        {
            //this.ClientSocket = clientSocket;
        }

        public void WaitOperationCompletion()
        {
            OperationPending.WaitOne();
        }
        

        public void OperationCompleted()
        {
            OperationPending.Set();
            OperationSemaphore.Release();

        }
    }
}
