using System.Threading;
using System.Threading.Tasks;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.DTOs.Export;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IReportingExportService _exportService;

    public ExportController(IReportingExportService exportService)
    {
        _exportService = exportService;
    }

    /// <summary>
    /// Get printable/PDF tax invoice data with ZATCA Phase 2 / Phase 1 TLV QR Code
    /// </summary>
    [HttpGet("invoices/{id:long}/print")]
    [HasPermission("SalesInvoices.View")]
    public async Task<ActionResult<TaxInvoicePrintDto>> GetInvoicePrintData(long id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _exportService.GetTaxInvoicePrintDataAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Export Trial Balance report directly to Excel/CSV with UTF-8 BOM
    /// </summary>
    [HttpPost("trial-balance/csv")]
    [HasPermission("FinancialReports.View")]
    public async Task<IActionResult> ExportTrialBalanceCsv([FromBody] TrialBalanceRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _exportService.ExportTrialBalanceCsvAsync(request, cancellationToken);
            return File(result.Data, result.ContentType, result.FileName);
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Export General Ledger statement directly to Excel/CSV with UTF-8 BOM
    /// </summary>
    [HttpPost("general-ledger/csv")]
    [HasPermission("FinancialReports.View")]
    public async Task<IActionResult> ExportGeneralLedgerCsv([FromBody] GeneralLedgerRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _exportService.ExportGeneralLedgerCsvAsync(request, cancellationToken);
            return File(result.Data, result.ContentType, result.FileName);
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
