using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Commercial;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PurchaseInvoicesController : ControllerBase
{
    private readonly IPurchaseInvoiceService _purchaseInvoiceService;
    private readonly ILogger<PurchaseInvoicesController> _logger;

    public PurchaseInvoicesController(IPurchaseInvoiceService purchaseInvoiceService, ILogger<PurchaseInvoicesController> logger)
    {
        _purchaseInvoiceService = purchaseInvoiceService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("PurchaseInvoices.Manage")]
    public async Task<ActionResult<PagedResult<PurchaseInvoiceDto>>> GetPaged(
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 20, 
        CancellationToken cancellationToken = default)
    {
        var result = await _purchaseInvoiceService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [HasPermission("PurchaseInvoices.Manage")]
    public async Task<ActionResult<PurchaseInvoiceDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _purchaseInvoiceService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("draft")]
    [HasPermission("PurchaseInvoices.Manage")]
    public async Task<ActionResult<PurchaseInvoiceDto>> CreateDraft([FromBody] CreatePurchaseInvoiceDto request, CancellationToken cancellationToken)
    {
        var result = await _purchaseInvoiceService.CreateDraftAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:long}/post")]
    [HasPermission("PurchaseInvoices.Manage")]
    public async Task<ActionResult<PurchaseInvoiceDto>> PostToGL(long id, CancellationToken cancellationToken)
    {
        var result = await _purchaseInvoiceService.PostToGLAsync(id, cancellationToken);
        return Ok(result);
    }
}
