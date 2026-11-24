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
public class SalesInvoicesController : ControllerBase
{
    private readonly ISalesInvoiceService _salesInvoiceService;
    private readonly ILogger<SalesInvoicesController> _logger;

    public SalesInvoicesController(ISalesInvoiceService salesInvoiceService, ILogger<SalesInvoicesController> logger)
    {
        _salesInvoiceService = salesInvoiceService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("SalesInvoices.Manage")]
    public async Task<ActionResult<PagedResult<SalesInvoiceDto>>> GetPaged(
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 20, 
        CancellationToken cancellationToken = default)
    {
        var result = await _salesInvoiceService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [HasPermission("SalesInvoices.Manage")]
    public async Task<ActionResult<SalesInvoiceDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _salesInvoiceService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("draft")]
    [HasPermission("SalesInvoices.Manage")]
    public async Task<ActionResult<SalesInvoiceDto>> CreateDraft([FromBody] CreateSalesInvoiceDto request, CancellationToken cancellationToken)
    {
        var result = await _salesInvoiceService.CreateDraftAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:long}/post")]
    [HasPermission("SalesInvoices.Manage")]
    public async Task<ActionResult<SalesInvoiceDto>> PostToGL(long id, CancellationToken cancellationToken)
    {
        var result = await _salesInvoiceService.PostToGLAsync(id, cancellationToken);
        return Ok(result);
    }
}
