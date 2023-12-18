
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetworkLibrary.Components.Crypto.Certificate.Native
{
    public class KeyExchangeKey : CryptKey
    {
        internal KeyExchangeKey(CryptContext ctx, IntPtr handle) : base(ctx, handle)  {}
        
        public override KeyType Type
        {
            get { return KeyType.Exchange; }
        }
    }
}
