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
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService productService, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetPaged([FromQuery] ProductFilterRequest filter, CancellationToken cancellationToken)
    {
        var result = await _productService.GetPagedAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _productService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductDto request, CancellationToken cancellationToken)
    {
        var result = await _productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HasPermission("Inventory.Manage")]
    public async Task<ActionResult<ProductDto>> Update(int id, [FromBody] UpdateProductDto request, CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission("Inventory.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _productService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
