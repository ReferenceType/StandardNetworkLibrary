using System;

namespace NetworkLibrary.MessageProtocol.Serialization
{
    public ref struct RouterHeader
    {
        public bool IsInternal;
        public Guid To;
    }

}



