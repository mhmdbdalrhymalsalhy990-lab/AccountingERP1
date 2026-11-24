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
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("Payments.Manage")]
    public async Task<ActionResult<PagedResult<PaymentDto>>> GetPaged(
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 20, 
        CancellationToken cancellationToken = default)
    {
        var result = await _paymentService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [HasPermission("Payments.Manage")]
    public async Task<ActionResult<PaymentDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _paymentService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("Payments.Manage")]
    public async Task<ActionResult<PaymentDto>> CreateAndPost([FromBody] CreatePaymentDto request, CancellationToken cancellationToken)
    {
        var result = await _paymentService.CreateAndPostPaymentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
