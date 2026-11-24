using AccountingERP.Application.DTOs.Master;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class CurrenciesController : ControllerBase
{
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<CurrenciesController> _logger;

    public CurrenciesController(ICurrencyService currencyService, ILogger<CurrenciesController> logger)
    {
        _currencyService = currencyService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<List<CurrencyDto>>> GetAll([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var result = await _currencyService.GetAllCurrenciesAsync(isActive, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<CurrencyDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _currencyService.GetCurrencyByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<CurrencyDto>> Create([FromBody] CreateCurrencyDto request, CancellationToken cancellationToken)
    {
        var result = await _currencyService.CreateCurrencyAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:int}/rates")]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<List<ExchangeRateDto>>> GetRates(int id, [FromQuery] int? toCurrencyId, CancellationToken cancellationToken)
    {
        var result = await _currencyService.GetExchangeRatesAsync(id, toCurrencyId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("rates")]
    [HasPermission("TaxAndCurrencies.Manage")]
    public async Task<ActionResult<ExchangeRateDto>> SetRate([FromBody] SetExchangeRateDto request, CancellationToken cancellationToken)
    {
        var result = await _currencyService.SetExchangeRateAsync(request, cancellationToken);
        return Ok(result);
    }
}
