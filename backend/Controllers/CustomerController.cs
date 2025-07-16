using backend.Domain.interfaces;
using backend.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(ICustomerService customerService, ILogger<CustomerController> logger)
        {
            _customerService = customerService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            try
            {
                var customers = await _customerService.GetCustomersAsync();
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers from database");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomerById(int id)
        {
            try
            {
                var customer = await _customerService.GetCustomerById(id);
                return Ok(customer);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing customers");
                return StatusCode(500, new { error = "Internal server error during synchronization" });
            }
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncCustomers()
        {
            try
            {
                await _customerService.SyncCustomersFromTripletexAsync();
                return Ok(new { message = "Customers synchronized successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing customers");
                return StatusCode(500, new { error = "Internal server error during synchronization" });
            }
        }
    }
}