namespace NetworkLibrary.DistributedP2P.Client
{
    public interface IClientAuthenticationProvider
    {
        IClientAuthenticationToken Authenticate();
    }
}