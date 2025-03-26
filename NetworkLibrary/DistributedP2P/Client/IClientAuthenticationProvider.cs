namespace NetworkLibrary.DistributedP2P.Client
{
    internal interface IClientAuthenticationProvider
    {
        IClientAuthenticationToken Authenticate();
    }
}