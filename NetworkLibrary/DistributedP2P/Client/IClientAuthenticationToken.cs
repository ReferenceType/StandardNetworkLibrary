namespace NetworkLibrary.DistributedP2P.Client
{
    public interface IClientAuthenticationToken
    {
        string Token { get; }
        string AuthenticationMethod { get; }
        string AdditionalData { get; }
    }
}