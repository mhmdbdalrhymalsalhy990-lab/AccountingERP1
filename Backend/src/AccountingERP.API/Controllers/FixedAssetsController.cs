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
public class FixedAssetsController : ControllerBase
{
    private readonly IFixedAssetService _fixedAssetService;
    private readonly ILogger<FixedAssetsController> _logger;

    public FixedAssetsController(IFixedAssetService fixedAssetService, ILogger<FixedAssetsController> logger)
    {
        _fixedAssetService = fixedAssetService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("FixedAssets.Manage")]
    public async Task<ActionResult<PagedResult<FixedAssetDto>>> GetPaged([FromQuery] FixedAssetFilterRequest filter, CancellationToken cancellationToken)
    {
        var result = await _fixedAssetService.GetPagedAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission("FixedAssets.Manage")]
    public async Task<ActionResult<FixedAssetDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _fixedAssetService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("FixedAssets.Manage")]
    public async Task<ActionResult<FixedAssetDto>> Create([FromBody] CreateFixedAssetDto request, CancellationToken cancellationToken)
    {
        var result = await _fixedAssetService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HasPermission("FixedAssets.Manage")]
    public async Task<ActionResult<FixedAssetDto>> Update(int id, [FromBody] UpdateFixedAssetDto request, CancellationToken cancellationToken)
    {
        var result = await _fixedAssetService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission("FixedAssets.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _fixedAssetService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("depreciation")]
    [HasPermission("FixedAssets.Manage")]
    public async Task<ActionResult<AssetDepreciationDto>> CalculateAndPostDepreciation([FromBody] PostAssetDepreciationDto request, CancellationToken cancellationToken)
    {
        var result = await _fixedAssetService.CalculateAndPostDepreciationAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/depreciations")]
    [HasPermission("FixedAssets.Manage")]
    public async Task<ActionResult<List<AssetDepreciationDto>>> GetDepreciations(int id, CancellationToken cancellationToken)
    {
        var result = await _fixedAssetService.GetAssetDepreciationsAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("dispose")]
    [HasPermission("FixedAssets.Manage")]
    public async Task<ActionResult<AssetDisposalResultDto>> DisposeAsset([FromBody] DisposeFixedAssetDto request, CancellationToken cancellationToken)
    {
        var result = await _fixedAssetService.DisposeAssetAsync(request, cancellationToken);
        return Ok(result);
    }
}
