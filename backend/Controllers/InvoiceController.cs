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

        [HttpPost("with-attachment-json")]
        public async Task<IActionResult> CreateInvoiceWithBase64([FromBody] InvoiceWithBase64Dto dto)
        {
            try
            {
                if (dto.Invoice?.Customer?.Id <= 0)
                    return BadRequest(new { error = "Valid Tripletex customer ID is required" });

                if (string.IsNullOrWhiteSpace(dto.FileBase64))
                    return BadRequest(new { error = "Base64 file data is required" });

                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(dto.FileBase64);
                }
                catch (FormatException ex)
                {
                    _logger.LogError("Invalid base64 file content: {Snippet}", dto.FileBase64?.Substring(0, Math.Min(20, dto.FileBase64.Length)));
                    return BadRequest(new
                    {
                        error = "Invalid base64 file format",
                        detail = ex.Message
                    });
                }

                var tripletexId = await _invoiceService.CreateInvoiceInTripletexAsync(
                    dto.Invoice,
                    fileBytes,
                    dto.FileName,
                    dto.UserId
                );

                return Ok(new { message = "Invoice created and attachment uploaded", tripletexId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice with base64 attachment");
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