using AccountingERP.Application.DTOs.Master;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

public class CurrencyService : ICurrencyService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<CurrencyService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<List<CurrencyDto>> GetAllCurrenciesAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Currencies.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        var list = await query.OrderBy(c => c.Code).ToListAsync(cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<CurrencyDto> GetCurrencyByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Currency), id);

        return MapToDto(currency);
    }

    public async Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyDto request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpper();
        var exists = await _context.Currencies.AnyAsync(c => c.Code == code, cancellationToken);
        if (exists)
            throw new BusinessRuleViolationException($"العملة '{code}' موجودة بالفعل في النظام.");

        var currency = new Currency
        {
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            Symbol = request.Symbol.Trim(),
            DecimalPlaces = request.DecimalPlaces,
            IsActive = request.IsActive
        };

        await _context.Currencies.AddAsync(currency, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Currency '{Code}' ({NameAr}) created successfully.", currency.Code, currency.NameAr);
        return MapToDto(currency);
    }

    public async Task<List<ExchangeRateDto>> GetExchangeRatesAsync(int fromCurrencyId, int? toCurrencyId = null, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.ExchangeRates
            .Include(r => r.FromCurrency)
            .Include(r => r.ToCurrency)
            .Where(r => r.CompanyId == companyId && r.FromCurrencyId == fromCurrencyId);

        if (toCurrencyId.HasValue)
            query = query.Where(r => r.ToCurrencyId == toCurrencyId.Value);

        var list = await query.OrderByDescending(r => r.RateDate).ToListAsync(cancellationToken);
        return list.Select(r => new ExchangeRateDto
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            FromCurrencyId = r.FromCurrencyId,
            FromCurrencyCode = r.FromCurrency?.Code ?? string.Empty,
            ToCurrencyId = r.ToCurrencyId,
            ToCurrencyCode = r.ToCurrency?.Code ?? string.Empty,
            RateDate = r.RateDate,
            Rate = r.Rate
        }).ToList();
    }

    public async Task<ExchangeRateDto> SetExchangeRateAsync(SetExchangeRateDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        if (request.Rate <= 0)
            throw new BusinessRuleViolationException("سعر الصرف يجب أن يكون أكبر من صفر.");

        if (request.FromCurrencyId == request.ToCurrencyId)
            throw new BusinessRuleViolationException("لا يمكن تحديد سعر صرف لنفس العملة.");

        var fromCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == request.FromCurrencyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Currency), request.FromCurrencyId);

        var toCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == request.ToCurrencyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Currency), request.ToCurrencyId);

        var rateDate = request.RateDate.Date;

        var existing = await _context.ExchangeRates
            .FirstOrDefaultAsync(r => r.CompanyId == companyId &&
                                      r.FromCurrencyId == request.FromCurrencyId &&
                                      r.ToCurrencyId == request.ToCurrencyId &&
                                      r.RateDate == rateDate, cancellationToken);

        if (existing != null)
        {
            existing.Rate = request.Rate;
        }
        else
        {
            existing = new ExchangeRate
            {
                CompanyId = companyId,
                FromCurrencyId = request.FromCurrencyId,
                ToCurrencyId = request.ToCurrencyId,
                RateDate = rateDate,
                Rate = request.Rate
            };
            await _context.ExchangeRates.AddAsync(existing, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Exchange Rate set: 1 {FromCode} = {Rate} {ToCode} on {RateDate}.",
            fromCurrency.Code, existing.Rate, toCurrency.Code, existing.RateDate);

        return new ExchangeRateDto
        {
            Id = existing.Id,
            CompanyId = existing.CompanyId,
            FromCurrencyId = existing.FromCurrencyId,
            FromCurrencyCode = fromCurrency.Code,
            ToCurrencyId = existing.ToCurrencyId,
            ToCurrencyCode = toCurrency.Code,
            RateDate = existing.RateDate,
            Rate = existing.Rate
        };
    }

    public async Task<decimal> GetEffectiveExchangeRateAsync(int fromCurrencyId, int toCurrencyId, DateTime date, CancellationToken cancellationToken = default)
    {
        if (fromCurrencyId == toCurrencyId)
            return 1.000000m;

        var companyId = EnsureCompanyId();
        var targetDate = date.Date;

        var rate = await _context.ExchangeRates
            .Where(r => r.CompanyId == companyId &&
                        r.FromCurrencyId == fromCurrencyId &&
                        r.ToCurrencyId == toCurrencyId &&
                        r.RateDate <= targetDate)
            .OrderByDescending(r => r.RateDate)
            .Select(r => r.Rate)
            .FirstOrDefaultAsync(cancellationToken);

        if (rate > 0)
            return rate;

        // Try reverse quotation: 1 / rate
        var reverseRate = await _context.ExchangeRates
            .Where(r => r.CompanyId == companyId &&
                        r.FromCurrencyId == toCurrencyId &&
                        r.ToCurrencyId == fromCurrencyId &&
                        r.RateDate <= targetDate)
            .OrderByDescending(r => r.RateDate)
            .Select(r => r.Rate)
            .FirstOrDefaultAsync(cancellationToken);

        if (reverseRate > 0)
            return Math.Round(1m / reverseRate, 6);

        return 1.000000m; // Fallback 1:1 if no exchange rate defined
    }

    private static CurrencyDto MapToDto(Currency c)
    {
        return new CurrencyDto
        {
            Id = c.Id,
            Code = c.Code,
            NameAr = c.NameAr,
            NameEn = c.NameEn,
            Symbol = c.Symbol,
            DecimalPlaces = c.DecimalPlaces,
            IsActive = c.IsActive
        };
    }
}
