using backend.Domain.interfaces;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(ITokenService tokenService, ILogger<CustomerController> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        [HttpGet("token/validate")]
        public async Task<IActionResult> ValidateToken()
        {
            try
            {
                var isValid = await _tokenService.IsTokenValidAsync();
                return Ok(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, new { error = "Internal server error during token validation" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            try
            {
                var result = await _tokenService.GetCustomersAsync();
                return Content(result, "application/json");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error occurred while fetching customers");
                return StatusCode(502, new { error = "Error communicating with Tripletex API" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while fetching customers");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}