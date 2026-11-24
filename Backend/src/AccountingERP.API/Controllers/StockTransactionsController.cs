using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.TreasuryAndInventory;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class StockTransactionsController : ControllerBase
{
    private readonly IStockTransactionService _stockTransactionService;
    private readonly ILogger<StockTransactionsController> _logger;

    public StockTransactionsController(IStockTransactionService stockTransactionService, ILogger<StockTransactionsController> logger)
    {
        _stockTransactionService = stockTransactionService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<PagedResult<StockTransactionDto>>> GetPaged([FromQuery] StockTransactionFilterRequest filter, CancellationToken cancellationToken)
    {
        var result = await _stockTransactionService.GetPagedAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpPost("inbound")]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<StockTransactionDto>> RecordInbound([FromBody] CreateInboundStockDto request, CancellationToken cancellationToken)
    {
        var result = await _stockTransactionService.RecordInboundAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("outbound")]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<StockTransactionDto>> RecordOutbound([FromBody] CreateOutboundStockDto request, CancellationToken cancellationToken)
    {
        var result = await _stockTransactionService.RecordOutboundAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("transfer")]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult> RecordTransfer([FromBody] CreateStockTransferDto request, CancellationToken cancellationToken)
    {
        var (outbound, inbound) = await _stockTransactionService.RecordTransferAsync(request, cancellationToken);
        return Ok(new { Outbound = outbound, Inbound = inbound });
    }

    [HttpPost("adjustment")]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<StockTransactionDto>> RecordAdjustment([FromBody] CreateStockAdjustmentDto request, CancellationToken cancellationToken)
    {
        var result = await _stockTransactionService.RecordAdjustmentAsync(request, cancellationToken);
        return Ok(result);
    }
}
