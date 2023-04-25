using System;

namespace Protobuff.P2P.Generic.Interfaces.Messages
{
    public interface IChanneCreationMessage
    {
        Guid DestinationId { get; set; }
        bool Encrypted { get; set; }
        Guid RegistrationId { get; set; }
        byte[] SharedSecret { get; set; }
    }
}