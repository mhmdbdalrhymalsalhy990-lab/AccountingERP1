using AccountingERP.Application.DTOs.TreasuryAndInventory;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class BankAccountsController : ControllerBase
{
    private readonly IBankAccountService _bankAccountService;
    private readonly ILogger<BankAccountsController> _logger;

    public BankAccountsController(IBankAccountService bankAccountService, ILogger<BankAccountsController> logger)
    {
        _bankAccountService = bankAccountService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("Treasury.Manage")]
    public async Task<ActionResult<List<BankAccountDto>>> GetAll([FromQuery] bool? isCash, CancellationToken cancellationToken)
    {
        var result = await _bankAccountService.GetAllAsync(isCash, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission("Treasury.Manage")]
    public async Task<ActionResult<BankAccountDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _bankAccountService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/balance")]
    [HasPermission("Treasury.Manage")]
    public async Task<ActionResult<decimal>> GetBookBalance(int id, CancellationToken cancellationToken)
    {
        var result = await _bankAccountService.GetBookBalanceAsync(id, cancellationToken);
        return Ok(new { BankAccountId = id, BookBalance = result });
    }

    [HttpPost]
    [HasPermission("Treasury.Manage")]
    public async Task<ActionResult<BankAccountDto>> Create([FromBody] CreateBankAccountDto request, CancellationToken cancellationToken)
    {
        var result = await _bankAccountService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HasPermission("Treasury.Manage")]
    public async Task<ActionResult<BankAccountDto>> Update(int id, [FromBody] UpdateBankAccountDto request, CancellationToken cancellationToken)
    {
        var result = await _bankAccountService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission("Treasury.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _bankAccountService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
