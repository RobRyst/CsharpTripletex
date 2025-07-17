using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SalesOrderController : ControllerBase
    {
        private readonly SaleOrderService _salesOrderService;
        private readonly ILogger<SalesOrderController> _logger;

        public SalesOrderController(SaleOrderService salesOrderService, ILogger<SalesOrderController> logger)
        {
            _salesOrderService = salesOrderService;
            _logger = logger;
        }

        [HttpGet("sync")]
        public async Task<IActionResult> SyncSalesOrders()
        {
            try
            {
                await _salesOrderService.SyncSalesOrdersFromTripletexAsync();
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
