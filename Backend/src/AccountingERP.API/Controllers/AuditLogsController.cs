using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.AssetAndSystem;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(IAuditLogService auditLogService, ILogger<AuditLogsController> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("AuditLogs.View")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetPaged([FromQuery] AuditLogFilterRequest filter, CancellationToken cancellationToken)
    {
        var result = await _auditLogService.GetPagedAsync(filter, cancellationToken);
        return Ok(result);
    }
}
