using System;

namespace NetworkLibrary.DistributedP2P.Components
{
    public interface ITimeProvider
    {
        DateTime GetTime();
    }
}