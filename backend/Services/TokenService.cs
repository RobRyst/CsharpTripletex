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

        //Brukes for å kunne lese verdier fra appsettings.json
        private readonly IConfiguration _config;

        private string? _bearerToken;
        private DateTime _expireDateToken;


        public TokenService(HttpClient httpClient, IConfiguration config, ILogger<TokenService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GetTokenAsync()
        {

            if (_bearerToken != null && DateTime.UtcNow < _expireDateToken)
            {
                return _bearerToken;
            }

            var url = "https://api.tripletex.io/v2/token/session";
            var payload = new
            {
                consumerToken = _config["Tripletex:ConsumerToken"],
                employeeToken = _config["Tripletex:EmployeeToken"]
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            _bearerToken = json.GetProperty("value").GetProperty("token").GetString();
            _expireDateToken = DateTime.UtcNow.AddMinutes(10);

            _logger.LogInformation("ConsumerToken: {ConsumerToken}", _config["Tripletex:ConsumerToken"]);
            _logger.LogInformation("EmployeeToken: {EmployeeToken}", _config["Tripletex:EmployeeToken"]);

            return _bearerToken;
        }

        public async Task<bool> IsTokenValidAsync()
        {
            var token = await GetTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.tripletex.io/v2/token/session");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request);
            _logger.LogInformation("ConsumerToken: {ConsumerToken}", _config["Tripletex:ConsumerToken"]);
            _logger.LogInformation("EmployeeToken: {EmployeeToken}", _config["Tripletex:EmployeeToken"]);
            return response.IsSuccessStatusCode;
            
        }
    }
}