namespace backend.Domain.interfaces
{
    public interface ITokenService
    {
        Task<string> GetTokenAsync();
        Task<bool> IsTokenValidAsync();
        Task<string> GetAuthorizationAsync();
    }
}