using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IFinancialReportService _reportService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IFinancialReportService reportService,
        ILogger<ReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Generate General Ledger detailed statement of account with running balance
    /// </summary>
    [HttpPost("general-ledger")]
    [HasPermission("FinancialReports.View")]
    public async Task<ActionResult<GeneralLedgerReportDto>> GetGeneralLedger([FromBody] GeneralLedgerRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _reportService.GetGeneralLedgerAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Generate Trial Balance with opening, period, cumulative and ending balances
    /// </summary>
    [HttpPost("trial-balance")]
    [HasPermission("FinancialReports.View")]
    public async Task<ActionResult<TrialBalanceReportDto>> GetTrialBalance([FromBody] TrialBalanceRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _reportService.GetTrialBalanceAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Generate Income Statement (Profit & Loss) for a specified period
    /// </summary>
    [HttpPost("income-statement")]
    [HasPermission("FinancialReports.View")]
    public async Task<ActionResult<IncomeStatementReportDto>> GetIncomeStatement([FromBody] IncomeStatementRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _reportService.GetIncomeStatementAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Generate Balance Sheet (Statement of Financial Position) as of a specific date
    /// </summary>
    [HttpPost("balance-sheet")]
    [HasPermission("FinancialReports.View")]
    public async Task<ActionResult<BalanceSheetReportDto>> GetBalanceSheet([FromBody] BalanceSheetRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _reportService.GetBalanceSheetAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Generate Statement of Cash Flows
    /// </summary>
    [HttpPost("cash-flow")]
    [HasPermission("FinancialReports.View")]
    public async Task<ActionResult<CashFlowStatementReportDto>> GetCashFlow([FromBody] CashFlowStatementRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _reportService.GetCashFlowStatementAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
