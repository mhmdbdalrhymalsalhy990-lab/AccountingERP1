using AccountingERP.Application.DTOs.TreasuryAndInventory;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class BankReconciliationsController : ControllerBase
{
    private readonly IBankReconciliationService _reconciliationService;
    private readonly ILogger<BankReconciliationsController> _logger;

    public BankReconciliationsController(
        IBankReconciliationService reconciliationService,
        ILogger<BankReconciliationsController> logger)
    {
        _reconciliationService = reconciliationService;
        _logger = logger;
    }

    [HttpGet("by-account/{bankAccountId:int}")]
    [HasPermission("Treasury.Manage")]
    public async Task<ActionResult<List<BankReconciliationDto>>> GetByBankAccount(int bankAccountId, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.GetByBankAccountIdAsync(bankAccountId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("Treasury.Manage")]
    public async Task<ActionResult<BankReconciliationDto>> PerformReconciliation([FromBody] CreateBankReconciliationDto request, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.PerformReconciliationAsync(request, cancellationToken);
        return Ok(result);
    }
}
