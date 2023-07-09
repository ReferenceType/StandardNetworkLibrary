using System;

namespace NetworkLibrary.MessageProtocol
{
    public interface IRouterHeader
    {
        bool IsInternal { get; set; }
        Guid To { get; set; }
    }
}
