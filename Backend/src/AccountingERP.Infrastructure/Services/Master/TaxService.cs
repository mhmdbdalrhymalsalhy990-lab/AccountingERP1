using AccountingERP.Application.DTOs.Master;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

public class TaxService : ITaxService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<TaxService> _logger;

    public TaxService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<TaxService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<List<TaxTypeDto>> GetAllTaxTypesAsync(TaxCategory? category = null, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.TaxTypes
            .Include(t => t.Account)
            .Include(t => t.TaxRates)
            .Where(t => t.CompanyId == companyId);

        if (category.HasValue)
            query = query.Where(t => t.Category == category.Value);

        var list = await query.OrderBy(t => t.Code).ToListAsync(cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<TaxTypeDto> GetTaxTypeByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var tax = await _context.TaxTypes
            .Include(t => t.Account)
            .Include(t => t.TaxRates)
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(TaxType), id);

        return MapToDto(tax);
    }

    public async Task<TaxTypeDto> CreateTaxTypeAsync(CreateTaxTypeDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.AccountId);

        if (!account.IsPosting)
            throw new HeaderAccountPostingException(account.AccountCode);

        var taxType = new TaxType
        {
            CompanyId = companyId,
            Code = request.Code.Trim().ToUpper(),
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            Category = request.Category,
            AccountId = account.Id,
            IsActive = request.IsActive
        };

        taxType.TaxRates.Add(new TaxRate
        {
            CompanyId = companyId,
            Rate = request.InitialRate,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = null
        });

        await _context.TaxTypes.AddAsync(taxType, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tax Type '{Code}' created with initial rate {Rate}% in Company {CompanyId}.", taxType.Code, request.InitialRate, companyId);
        return await GetTaxTypeByIdAsync(taxType.Id, cancellationToken);
    }

    public async Task<TaxTypeDto> UpdateTaxTypeAsync(int id, UpdateTaxTypeDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var tax = await _context.TaxTypes
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(TaxType), id);

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.AccountId);

        if (!account.IsPosting)
            throw new HeaderAccountPostingException(account.AccountCode);

        tax.NameAr = request.NameAr.Trim();
        tax.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim();
        tax.AccountId = account.Id;
        tax.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetTaxTypeByIdAsync(tax.Id, cancellationToken);
    }

    public async Task<TaxRateDto> AddTaxRateAsync(int taxTypeId, CreateTaxRateDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var tax = await _context.TaxTypes
            .Include(t => t.TaxRates)
            .FirstOrDefaultAsync(t => t.Id == taxTypeId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(TaxType), taxTypeId);

        // Close out the previous active tax rate
        var activeRate = tax.TaxRates
            .Where(r => r.EffectiveTo == null || r.EffectiveTo > request.EffectiveFrom)
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();

        if (activeRate != null)
        {
            activeRate.EffectiveTo = request.EffectiveFrom;
        }

        var newRate = new TaxRate
        {
            CompanyId = companyId,
            TaxTypeId = tax.Id,
            Rate = request.Rate,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = null
        };

        await _context.TaxRates.AddAsync(newRate, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added new Tax Rate {Rate}% for TaxType {TaxCode} starting {EffectiveFrom}.", newRate.Rate, tax.Code, newRate.EffectiveFrom);

        return new TaxRateDto
        {
            Id = newRate.Id,
            TaxTypeId = newRate.TaxTypeId,
            Rate = newRate.Rate,
            EffectiveFrom = newRate.EffectiveFrom,
            EffectiveTo = newRate.EffectiveTo
        };
    }

    public async Task<decimal> GetEffectiveRateForDateAsync(int taxTypeId, DateTime date, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var rate = await _context.TaxRates
            .Where(r => r.CompanyId == companyId &&
                        r.TaxTypeId == taxTypeId &&
                        r.EffectiveFrom <= date &&
                        (r.EffectiveTo == null || r.EffectiveTo >= date))
            .OrderByDescending(r => r.EffectiveFrom)
            .Select(r => r.Rate)
            .FirstOrDefaultAsync(cancellationToken);

        return rate;
    }

    private static TaxTypeDto MapToDto(TaxType t)
    {
        var currentRate = t.TaxRates
            .OrderByDescending(r => r.EffectiveFrom)
            .Select(r => r.Rate)
            .FirstOrDefault();

        return new TaxTypeDto
        {
            Id = t.Id,
            CompanyId = t.CompanyId,
            Code = t.Code,
            NameAr = t.NameAr,
            NameEn = t.NameEn,
            Category = t.Category,
            AccountId = t.AccountId,
            AccountCode = t.Account?.AccountCode,
            AccountName = t.Account?.NameAr,
            IsActive = t.IsActive,
            CurrentRate = currentRate,
            TaxRates = t.TaxRates.OrderByDescending(r => r.EffectiveFrom).Select(r => new TaxRateDto
            {
                Id = r.Id,
                TaxTypeId = r.TaxTypeId,
                Rate = r.Rate,
                EffectiveFrom = r.EffectiveFrom,
                EffectiveTo = r.EffectiveTo
            }).ToList()
        };
    }
}
