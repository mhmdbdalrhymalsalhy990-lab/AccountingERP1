using AccountingERP.Application.DTOs.TreasuryAndInventory;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;
    private readonly ILogger<WarehousesController> _logger;

    public WarehousesController(IWarehouseService warehouseService, ILogger<WarehousesController> logger)
    {
        _warehouseService = warehouseService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<List<WarehouseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _warehouseService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<WarehouseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _warehouseService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/stock")]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<List<WarehouseStockItemDto>>> GetWarehouseStock(int id, CancellationToken cancellationToken)
    {
        var result = await _warehouseService.GetWarehouseStockAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<WarehouseDto>> Create([FromBody] CreateWarehouseDto request, CancellationToken cancellationToken)
    {
        var result = await _warehouseService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<WarehouseDto>> Update(int id, [FromBody] UpdateWarehouseDto request, CancellationToken cancellationToken)
    {
        var result = await _warehouseService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission("Inventory.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _warehouseService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
