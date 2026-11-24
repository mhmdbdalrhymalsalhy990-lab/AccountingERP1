using AccountingERP.Application.DTOs.Commercial;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly ILogger<SuppliersController> _logger;

    public SuppliersController(ISupplierService supplierService, ILogger<SuppliersController> logger)
    {
        _supplierService = supplierService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("Suppliers.Manage")]
    public async Task<ActionResult<List<SupplierDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission("Suppliers.Manage")]
    public async Task<ActionResult<SupplierDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("Suppliers.Manage")]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] CreateSupplierDto request, CancellationToken cancellationToken)
    {
        var result = await _supplierService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HasPermission("Suppliers.Manage")]
    public async Task<ActionResult<SupplierDto>> Update(int id, [FromBody] UpdateSupplierDto request, CancellationToken cancellationToken)
    {
        var result = await _supplierService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission("Suppliers.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _supplierService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
