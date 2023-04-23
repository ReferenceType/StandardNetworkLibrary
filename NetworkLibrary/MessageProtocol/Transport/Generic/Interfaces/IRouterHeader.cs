using System;

namespace MessageProtocol
{
    public interface IRouterHeader
    {
        Guid From { get; }
        bool IsInternal { get; }
        Guid To { get; }
    }
}
