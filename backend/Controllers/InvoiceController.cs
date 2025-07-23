using backend.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using backend.Dtos;

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
                var invoices = await _invoiceService.GetAllInvoicesAsync();
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
        public async Task<IActionResult> CreateInvoice([FromBody] TripletexInvoiceCreateDto invoice)
        {
            try
            {
                if (invoice?.Customer?.Id <= 0)
                    return BadRequest(new { error = "Valid Tripletex customer ID is required" });

                var tripletexId = await _invoiceService.CreateInvoiceInTripletexAsync(invoice);

                return Ok(new { message = "Invoice created in Tripletex", tripletexId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("with-attachment")]
        public async Task<IActionResult> CreateInvoiceWithAttachment([FromBody] TripletexInvoiceCreateDto dto)
        {
            try
            {
                if (dto?.Customer?.Id <= 0)
                    return BadRequest(new { error = "Valid Tripletex customer ID is required" });
                    
                if (dto.Amount <= 0)
                    return BadRequest(new { error = "Invoice amount must be greater than 0" });

                if (string.IsNullOrEmpty(dto.InvoiceDate))
                    dto.InvoiceDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
                    
                if (string.IsNullOrEmpty(dto.InvoiceDueDate))
                    dto.InvoiceDueDate = DateTime.UtcNow.AddDays(14).Date.ToString("yyyy-MM-dd");

                _logger.LogInformation("Creating invoice with attachment for customer {CustomerId}, amount {Amount}", 
                    dto.Customer.Id, dto.Amount);

                var invoiceId = await _invoiceService.CreateInvoiceWithAttachmentAsync(dto);

                return Ok(new { 
                    message = "Invoice created with attachment", 
                    invoiceId,
                    tripletexUrl = $"https://api-test.tripletex.tech/execute/invoiceMenu?invoiceId={invoiceId}&contextId=80382946#attachments"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice with attachment");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpGet("{id}/attachment/verify")]
        public async Task<IActionResult> VerifyInvoiceAttachment(int id)
        {
            try
            {
                var hasAttachment = await _invoiceService.VerifyInvoiceAttachmentAsync(id);
                return Ok(new { 
                    invoiceId = id, 
                    hasAttachment,
                    message = hasAttachment ? "Attachment found" : "No attachment found"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying attachment for invoice {Id}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{id}/attachment/details")]
        public async Task<IActionResult> GetInvoiceAttachmentDetails(int id)
        {
            try
            {
                var details = await _invoiceService.GetInvoiceAttachmentDetailsAsync(id);
                return Ok(details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attachment details for invoice {Id}", id);
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