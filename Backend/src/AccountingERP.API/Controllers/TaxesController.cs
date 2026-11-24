using AccountingERP.Application.DTOs.Master;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TaxesController : ControllerBase
{
    private readonly ITaxService _taxService;
    private readonly ILogger<TaxesController> _logger;

    public TaxesController(ITaxService taxService, ILogger<TaxesController> logger)
    {
        _taxService = taxService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<List<TaxTypeDto>>> GetAll([FromQuery] TaxCategory? category, CancellationToken cancellationToken)
    {
        var result = await _taxService.GetAllTaxTypesAsync(category, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<TaxTypeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _taxService.GetTaxTypeByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<TaxTypeDto>> Create([FromBody] CreateTaxTypeDto request, CancellationToken cancellationToken)
    {
        var result = await _taxService.CreateTaxTypeAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<TaxTypeDto>> Update(int id, [FromBody] UpdateTaxTypeDto request, CancellationToken cancellationToken)
    {
        var result = await _taxService.UpdateTaxTypeAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:int}/rates")]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<TaxRateDto>> AddRate(int id, [FromBody] CreateTaxRateDto request, CancellationToken cancellationToken)
    {
        var result = await _taxService.AddTaxRateAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
