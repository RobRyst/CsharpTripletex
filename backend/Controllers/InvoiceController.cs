using backend.Domain.Interfaces;
using backend.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using backend.Domain.Entities;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(IInvoiceService invoiceService, ILogger<InvoiceController> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetInvoices()
        {
            try
            {
                var invoices = await _invoiceService.GetAllWithCustomerAsync();
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices from database");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetInvoice(int id)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
                return Ok(invoice);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice with id {Id}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateInvoice([FromBody] InvoiceModel invoice)
        {
            try
            {
                var tripletexId = await _invoiceService.CreateInvoiceInTripletexAsync(invoice);
                return Ok(new { message = "Invoice created in Tripletex", tripletexId });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error posting invoices from database");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }


        [HttpPost("sync")]
        public async Task<IActionResult> SyncInvoices()
        {
            try
            {
                await _invoiceService.SyncInvoicesFromTripletexAsync();
                return Ok(new { message = "Invoices synchronized successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing invoices");
                return StatusCode(500, new { error = "Internal server error during synchronization" });
            }
        }
    }
}