using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class CostCentersController : ControllerBase
{
    private readonly ICostCenterService _costCenterService;
    private readonly ILogger<CostCentersController> _logger;

    public CostCentersController(ICostCenterService costCenterService, ILogger<CostCentersController> logger)
    {
        _costCenterService = costCenterService;
        _logger = logger;
    }

    [HttpGet("tree")]
    [HasPermission("COA.Manage")]
    public async Task<ActionResult<List<CostCenterTreeDto>>> GetTree(CancellationToken cancellationToken)
    {
        var result = await _costCenterService.GetTreeAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [HasPermission("COA.Manage")]
    public async Task<ActionResult<List<CostCenterDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _costCenterService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission("COA.Manage")]
    public async Task<ActionResult<CostCenterDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _costCenterService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("COA.Manage")]
    public async Task<ActionResult<CostCenterDto>> Create([FromBody] CreateCostCenterDto request, CancellationToken cancellationToken)
    {
        var result = await _costCenterService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HasPermission("COA.Manage")]
    public async Task<ActionResult<CostCenterDto>> Update(int id, [FromBody] UpdateCostCenterDto request, CancellationToken cancellationToken)
    {
        var result = await _costCenterService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission("COA.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _costCenterService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
