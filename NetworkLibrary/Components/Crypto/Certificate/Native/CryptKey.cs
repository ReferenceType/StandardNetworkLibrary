
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetworkLibrary.Components.Crypto.Certificate.Native
{
    public abstract class CryptKey : DisposeableObject
    {
        CryptContext ctx;
        IntPtr handle;

        internal IntPtr Handle { get { return handle; } }

        internal CryptKey(CryptContext ctx, IntPtr handle)
        {
            this.ctx = ctx;
            this.handle = handle;
        }

        public abstract KeyType Type { get; }

        protected override void CleanUp(bool viaDispose)
        {
            if (viaDispose)
                ctx.DestroyKey(this);
        }
    }
}
