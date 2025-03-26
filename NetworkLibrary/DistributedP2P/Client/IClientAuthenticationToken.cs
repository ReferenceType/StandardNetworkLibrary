namespace NetworkLibrary.DistributedP2P.Client
{
    internal interface IClientAuthenticationToken
    {
        string Token { get; }
        string AuthenticationMethod { get; }
        string AdditionalData { get; }
    }
}