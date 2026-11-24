using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(IAccountService accountService, ILogger<AccountsController> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }

    /// <summary>
    /// Get full hierarchical tree of accounts for the active company
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult<List<AccountTreeDto>>> GetAccountTree([FromQuery] AccountType? type, CancellationToken cancellationToken)
    {
        var result = await _accountService.GetAccountTreeAsync(type, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get posting-only accounts for drop-down selections in transaction screens
    /// </summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<List<AccountLookupDto>>> GetPostingAccountsLookup([FromQuery] AccountType? type, CancellationToken cancellationToken)
    {
        var result = await _accountService.GetPostingAccountsLookupAsync(type, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Search and filter accounts with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AccountDto>>> GetAccountsPaged([FromQuery] AccountFilterRequest filter, CancellationToken cancellationToken)
    {
        var result = await _accountService.GetAccountsPagedAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get single account by its primary key ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AccountDto>> GetById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _accountService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get single account by its unique account code
    /// </summary>
    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<AccountDto>> GetByCode(string code, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _accountService.GetByCodeAsync(code, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Suggest the next sequential account code for a child under the given parent account
    /// </summary>
    [HttpGet("next-code/{parentAccountId:int}")]
    public async Task<ActionResult<object>> GetNextAccountCode(int parentAccountId, CancellationToken cancellationToken)
    {
        try
        {
            var nextCode = await _accountService.GenerateNextAccountCodeAsync(parentAccountId, cancellationToken);
            return Ok(new { parentAccountId, nextCode });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create a new account in the chart of accounts
    /// </summary>
    [HttpPost]
    [HasPermission("COA.Manage")]
    public async Task<ActionResult<AccountDto>> CreateAccount([FromBody] CreateAccountDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _accountService.CreateAccountAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update existing account details (names, nature, currency, active state)
    /// </summary>
    [HttpPut("{id:int}")]
    [HasPermission("COA.Manage")]
    public async Task<ActionResult<AccountDto>> UpdateAccount(int id, [FromBody] UpdateAccountDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _accountService.UpdateAccountAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Soft delete an account if it has no sub-accounts, transactions, or system mappings
    /// </summary>
    [HttpDelete("{id:int}")]
    [HasPermission("COA.Manage")]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _accountService.DeleteAccountAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Toggle active / inactive status of an account
    /// </summary>
    [HttpPatch("{id:int}/toggle-status")]
    [HasPermission("COA.Manage")]
    public async Task<ActionResult<object>> ToggleAccountStatus(int id, CancellationToken cancellationToken)
    {
        try
        {
            var isActive = await _accountService.ToggleAccountStatusAsync(id, cancellationToken);
            return Ok(new { id, isActive, message = isActive ? "تم تفعيل الحساب بنجاح." : "تم تعطيل الحساب بنجاح." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
