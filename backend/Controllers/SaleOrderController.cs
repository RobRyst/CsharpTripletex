using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SaleOrderController : ControllerBase
    {
        private readonly SaleOrderService _saleOrderService;
        private readonly ILogger<SaleOrderController> _logger;

        public SaleOrderController(SaleOrderService saleOrderService, ILogger<SaleOrderController> logger)
        {
            _saleOrderService = saleOrderService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetSaleOrders()
        {
            try
            {
                var saleOrders = await _saleOrderService.GetAllWithUserAsync();
                return Ok(saleOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Sale Order from database");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSaleOrder(int id)
        {
            try
            {
                var saleOrder = await _saleOrderService.GetSaleOrderByIdAsync(id);
                return Ok(saleOrder);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "Sale Order not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Sale Order with id {Id}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("sync")]
        public async Task<IActionResult> SyncSaleOrders()
        {
            try
            {
                await _saleOrderService.SyncSaleOrdersFromTripletexAsync();
                return Ok(new { message = "Sales orders synced successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing sales orders");
                return StatusCode(500, new { message = "An error occurred while syncing sales orders." });
            }
        }
    }
}
