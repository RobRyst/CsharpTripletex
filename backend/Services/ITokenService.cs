namespace backend.Services
{
    public interface ITokenService
    {
        Task<string> GetTokenAsync();
        Task<bool> IsTokenValidAsync();
    }
}