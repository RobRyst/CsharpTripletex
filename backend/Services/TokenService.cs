using System.Text.Json;
using backend.Domain.interfaces;

public class TokenService : ITokenService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TokenService> _logger;
    private readonly IConfiguration _config;

    private string? _bearerToken;
    private DateTime _expireDateToken;

    public TokenService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<TokenService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "YourApp/1.0");
    }

    public async Task<string> GetTokenAsync()
    {
        if (_bearerToken != null && DateTime.UtcNow < _expireDateToken)
        {
            return _bearerToken!;
        }

        try
        {
            var consumerToken = _config["Tripletex:ConsumerToken"];
            var employeeToken = _config["Tripletex:EmployeeToken"];

            if (string.IsNullOrEmpty(consumerToken) || string.IsNullOrEmpty(employeeToken))
            {
                throw new InvalidOperationException("Token-konfigurasjon mangler i appsettings.json");
            }

            var expirationDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

            var url = $"https://api-test.tripletex.tech/v2/token/session/:create?consumerToken={Uri.EscapeDataString(consumerToken)}&employeeToken={Uri.EscapeDataString(employeeToken)}&expirationDate={expirationDate}";

            _logger.LogInformation("Henter nytt token fra Tripletex...");

            var response = await _httpClient.PutAsync(url, null);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token-feil ({Status}): {Error}", response.StatusCode, error);
                throw new HttpRequestException("Token-request feilet");
            }

            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonDocument.Parse(json);

            if (parsed.RootElement.TryGetProperty("value", out var value))
            {
                _bearerToken = value.GetProperty("token").GetString();
                _expireDateToken = DateTime.UtcNow.AddHours(23);
                _logger.LogInformation("Token hentet OK");
                return _bearerToken!;
            }

            throw new InvalidOperationException("Respons fra Tripletex mangler 'value.token'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kunne ikke hente token fra Tripletex");
            throw;
        }
    }

    public async Task<string> GetAuthorizationAsync()
    {
        var token = await GetTokenAsync();
        var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"0:{token}"));
        return $"Basic {authValue}";
    }

    public async Task<bool> IsTokenValidAsync()
    {
        try
        {
            var authHeader = await GetAuthorizationAsync();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api-test.tripletex.tech/v2/customer");
            request.Headers.Add("Authorization", authHeader);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feil ved validering av token");
            return false;
        }
    }
}
