using System.Text.Json;
using backend.Domain.interfaces;
using backend.Dtos;

public class TripleTexService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenService _tokenService;
    private readonly ILogger<TripleTexService> _logger;

    public TripleTexService(HttpClient httpClient, ITokenService tokenService, ILogger<TripleTexService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<List<CustomerDto>> GetCustomersAsync()
    {
        try
        {
            var authHeader = await _tokenService.GetAuthorizationAsync();

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api-test.tripletex.tech/v2/customer");
            request.Headers.Add("Authorization", authHeader);

            _logger.LogInformation("Henter kunder fra Tripletex API...");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Feil ved henting av kunder: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Henting av kunder feilet: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CustomerListResponse>(json);

            _logger.LogInformation("Kunder hentet OK, Antall Kunder: ({Count})", result?.Values?.Count ?? 0);

            return result?.Values ?? new List<CustomerDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Klarte ikke å hente kunder fra Tripletex");
            throw;
        }
    }

        public async Task<List<InvoiceDto>> GetInvoicesAsync()
    {
        try
        {
            var authHeader = await _tokenService.GetAuthorizationAsync();

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api-test.tripletex.tech/v2/invoice");
            request.Headers.Add("Authorization", authHeader);

            _logger.LogInformation("Henter fakturaer fra Tripletex API...");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Feil ved henting av fakturaer: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Henting av fakturaer feilet: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<InvoiceListResponse>(json);

            _logger.LogInformation("Fakturaer hentet OK, Antall Fakturaer: ({Count})", result?.Values?.Count ?? 0);

            return result?.Values ?? new List<InvoiceDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Klarte ikke å hente fakturaer fra Tripletex");
            throw;
        }
    }
}

