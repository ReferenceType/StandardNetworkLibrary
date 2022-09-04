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
        public AutoResetEvent OperationPending = new AutoResetEvent(true);
        public SemaphoreSlim OperationSemaphore = new SemaphoreSlim(1,1);
        public Guid Guid;

        public UserToken()
        {
        }

        public void WaitOperationCompletion()
        {
            //OperationPending.WaitOne();
            OperationSemaphore.Wait();
        }
        

        public void OperationCompleted()
        {
            //OperationPending.Set();
            OperationSemaphore.Release();

        }
    }
}
