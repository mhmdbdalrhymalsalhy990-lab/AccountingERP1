using AccountingERP.Application.DTOs.Master;

namespace AccountingERP.Application.Interfaces;

public interface ICurrencyService
{
    Task<List<CurrencyDto>> GetAllCurrenciesAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<CurrencyDto> GetCurrencyByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyDto request, CancellationToken cancellationToken = default);
    Task<List<ExchangeRateDto>> GetExchangeRatesAsync(int fromCurrencyId, int? toCurrencyId = null, CancellationToken cancellationToken = default);
    Task<ExchangeRateDto> SetExchangeRateAsync(SetExchangeRateDto request, CancellationToken cancellationToken = default);
    Task<decimal> GetEffectiveExchangeRateAsync(int fromCurrencyId, int toCurrencyId, DateTime date, CancellationToken cancellationToken = default);
}
