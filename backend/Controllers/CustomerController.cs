using Microsoft.AspNetCore.Mvc;
using backend.Services;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
            var isValid = await _tokenService.IsTokenValidAsync();
            return Ok(new { isValid });
        }

        [HttpGet("test-log")]
    public IActionResult TestLog()
    {
        _logger.LogInformation("Test logging from CustomerController!");
        return Ok("Logged");
    }
    }
}
