using backend.Services;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    public class TokenService : ITokenService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TokenService> _logger;
        private readonly IConfiguration _config;

        private string? _bearerToken;
        private DateTime _expireDateToken;

        public TokenService(HttpClient httpClient, IConfiguration config, ILogger<TokenService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
            
            // Set User-Agent header (required by Tripletex API)
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "YourApp/1.0");
        }

        public async Task<string> GetTokenAsync()
        {
            if (_bearerToken != null && DateTime.UtcNow < _expireDateToken)
            {
                return _bearerToken;
            }

            var consumerToken = _config["Tripletex:ConsumerToken"];
            var employeeToken = _config["Tripletex:EmployeeToken"];

            if (string.IsNullOrEmpty(consumerToken) || string.IsNullOrEmpty(employeeToken))
            {
                throw new InvalidOperationException("Consumer token and employee token must be configured in appsettings.json");
            }

            var expirationDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
            
            var url = $"https://api-test.tripletex.tech/v2/token/session/:create?consumerToken={Uri.EscapeDataString(consumerToken)}&employeeToken={Uri.EscapeDataString(employeeToken)}&expirationDate={expirationDate}";

            try
            {
                _logger.LogInformation("Requesting token with expiration date: {ExpirationDate}", expirationDate);
                _logger.LogInformation("Request URL: {Url}", url.Replace(consumerToken, "***").Replace(employeeToken, "***"));
                
                var response = await _httpClient.PutAsync(url, null);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Token request failed with status {StatusCode}: {ErrorContent}", 
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Token request failed: {response.StatusCode}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Token response: {Response}", jsonResponse);
                
                var json = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                

                if (json.TryGetProperty("value", out var valueProperty))
                {
                    _bearerToken = valueProperty.GetProperty("token").GetString();
                    _expireDateToken = DateTime.UtcNow.AddHours(23);
                }
                else
                {
                    throw new InvalidOperationException("Invalid token response format");
                }

                _logger.LogInformation("Successfully obtained bearer token");
                return _bearerToken!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to obtain bearer token");
                throw;
            }
        }

        public async Task<bool> IsTokenValidAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api-test.tripletex.tech/v2/company");
                
                var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"0:{token}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Token validation failed with status {StatusCode}", response.StatusCode);
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        public async Task<string> GetCustomersAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api-test.tripletex.tech/v2/customer");
                
                var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"0:{token}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                
                _logger.LogInformation("Requesting customers from Tripletex API");
                
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Get customers request failed with status {StatusCode}: {ErrorContent}", 
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Get customers request failed: {response.StatusCode}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Successfully retrieved customers");
                
                return jsonResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get customers");
                throw;
            }
        }
    }
}