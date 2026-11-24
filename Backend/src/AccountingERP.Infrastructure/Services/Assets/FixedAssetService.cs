using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.DTOs.AssetAndSystem;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

public class FixedAssetService : IFixedAssetService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IJournalEntryService _journalEntryService;
    private readonly ILogger<FixedAssetService> _logger;

    public FixedAssetService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IJournalEntryService journalEntryService,
        ILogger<FixedAssetService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _journalEntryService = journalEntryService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<PagedResult<FixedAssetDto>> GetPagedAsync(FixedAssetFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.FixedAssets
            .Include(a => a.Branch)
            .Include(a => a.AssetAccount)
            .Include(a => a.AccumulatedDepreciationAccount)
            .Include(a => a.DepreciationExpenseAccount)
            .Where(a => a.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(a => a.AssetCode.Contains(term) || a.NameAr.Contains(term) || a.NameEn.Contains(term));
        }

        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var assets = await query
            .OrderBy(a => a.AssetCode)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var assetIds = assets.Select(a => a.Id).ToList();
        var accumMap = await _context.AssetDepreciations
            .Where(d => d.CompanyId == companyId && assetIds.Contains(d.FixedAssetId))
            .GroupBy(d => d.FixedAssetId)
            .Select(g => new { FixedAssetId = g.Key, Total = g.Sum(x => x.DepreciationAmount) })
            .ToDictionaryAsync(x => x.FixedAssetId, x => x.Total, cancellationToken);

        var dtos = assets.Select(a => MapToDto(a, accumMap.GetValueOrDefault(a.Id, 0m))).ToList();
        return new PagedResult<FixedAssetDto>(dtos, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<FixedAssetDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var asset = await _context.FixedAssets
            .Include(a => a.Branch)
            .Include(a => a.AssetAccount)
            .Include(a => a.AccumulatedDepreciationAccount)
            .Include(a => a.DepreciationExpenseAccount)
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(FixedAsset), id);

        var accum = await _context.AssetDepreciations
            .Where(d => d.FixedAssetId == id && d.CompanyId == companyId)
            .SumAsync(d => (decimal?)d.DepreciationAmount, cancellationToken) ?? 0m;

        return MapToDto(asset, accum);
    }

    public async Task<FixedAssetDto> CreateAsync(CreateFixedAssetDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        if (request.PurchaseCost <= 0)
            throw new BusinessRuleViolationException("تكلفة شراء الأصل يجب أن تكون أكبر من صفر.");

        if (request.UsefulLifeMonths <= 0)
            throw new BusinessRuleViolationException("العمر الإنتاجي للأصل بالأشهر يجب أن يكون أكبر من صفر.");

        if (request.SalvageValue < 0 || request.SalvageValue >= request.PurchaseCost)
            throw new BusinessRuleViolationException("القيمة التخريدية يجب أن تكون موجبة وأقل من تكلفة شراء الأصل.");

        var branchId = request.BranchId ?? _currentTenantService.BranchId
            ?? (await _context.Branches.Where(b => b.CompanyId == companyId).Select(b => b.Id).FirstOrDefaultAsync(cancellationToken));

        if (branchId == 0)
            throw new BusinessRuleViolationException("لا يوجد فرع محدد لربط الأصل به.");

        // 1. Resolve GL Accounts
        int assetAccountId = request.AssetAccountId ?? 0;
        if (assetAccountId == 0)
        {
            var defAsset = await _context.Accounts.FirstOrDefaultAsync(a => a.CompanyId == companyId && a.AccountCode == "120101", cancellationToken);
            assetAccountId = defAsset?.Id ?? throw new BusinessRuleViolationException("لم يتم العثور على حساب أستاذ عام للأصول الثابتة (120101).");
        }

        int accumAccountId = request.AccumulatedDepreciationAccountId ?? 0;
        if (accumAccountId == 0)
        {
            var defAccum = await _context.Accounts.FirstOrDefaultAsync(a => a.CompanyId == companyId && a.AccountCode == "120102", cancellationToken);
            accumAccountId = defAccum?.Id ?? throw new BusinessRuleViolationException("لم يتم العثور على حساب مجمع إهلاك الأصول الثابتة (120102).");
        }

        int expenseAccountId = request.DepreciationExpenseAccountId ?? 0;
        if (expenseAccountId == 0)
        {
            var defExpense = await _context.Accounts.FirstOrDefaultAsync(a => a.CompanyId == companyId && a.AccountCode == "520301", cancellationToken);
            expenseAccountId = defExpense?.Id ?? throw new BusinessRuleViolationException("لم يتم العثور على حساب مصروف الإهلاك (520301).");
        }

        // 2. Generate Asset Code if not supplied
        string assetCode = request.AssetCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(assetCode))
        {
            var count = await _context.FixedAssets.CountAsync(a => a.CompanyId == companyId, cancellationToken);
            assetCode = $"FA-{(count + 1):D4}";
        }

        var asset = new FixedAsset
        {
            CompanyId = companyId,
            BranchId = branchId,
            AssetCode = assetCode,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            PurchaseDate = request.PurchaseDate,
            PurchaseCost = request.PurchaseCost,
            SalvageValue = request.SalvageValue,
            UsefulLifeMonths = request.UsefulLifeMonths,
            DepreciationMethod = request.DepreciationMethod,
            AssetAccountId = assetAccountId,
            AccumulatedDepreciationAccountId = accumAccountId,
            DepreciationExpenseAccountId = expenseAccountId,
            Status = FixedAssetStatus.Active
        };

        await _context.FixedAssets.AddAsync(asset, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fixed Asset '{AssetCode}' ({NameAr}) created successfully.", asset.AssetCode, asset.NameAr);
        return await GetByIdAsync(asset.Id, cancellationToken);
    }

    public async Task<FixedAssetDto> UpdateAsync(int id, UpdateFixedAssetDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var asset = await _context.FixedAssets
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(FixedAsset), id);

        if (asset.Status == FixedAssetStatus.Disposed)
            throw new BusinessRuleViolationException("لا يمكن تعديل أصل تم استبعاده وبيعه.");

        if (request.UsefulLifeMonths <= 0)
            throw new BusinessRuleViolationException("العمر الإنتاجي للأصل يجب أن يكون أكبر من صفر.");

        asset.NameAr = request.NameAr.Trim();
        asset.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim();
        asset.SalvageValue = request.SalvageValue;
        asset.UsefulLifeMonths = request.UsefulLifeMonths;

        if (request.AssetAccountId.HasValue && request.AssetAccountId.Value > 0)
            asset.AssetAccountId = request.AssetAccountId.Value;

        if (request.AccumulatedDepreciationAccountId.HasValue && request.AccumulatedDepreciationAccountId.Value > 0)
            asset.AccumulatedDepreciationAccountId = request.AccumulatedDepreciationAccountId.Value;

        if (request.DepreciationExpenseAccountId.HasValue && request.DepreciationExpenseAccountId.Value > 0)
            asset.DepreciationExpenseAccountId = request.DepreciationExpenseAccountId.Value;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(asset.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var asset = await _context.FixedAssets
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(FixedAsset), id);

        var hasDepreciations = await _context.AssetDepreciations.AnyAsync(d => d.FixedAssetId == id, cancellationToken);
        if (hasDepreciations)
            throw new BusinessRuleViolationException($"لا يمكن حذف الأصل '{asset.NameAr}' لوجود إهلاكات مرحلة مسجلة عليه.");

        _context.FixedAssets.Remove(asset);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fixed Asset {Id} deleted.", id);
    }

    public async Task<AssetDepreciationDto> CalculateAndPostDepreciationAsync(PostAssetDepreciationDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var asset = await _context.FixedAssets
            .Include(a => a.Branch)
            .Include(a => a.AssetAccount)
            .Include(a => a.AccumulatedDepreciationAccount)
            .Include(a => a.DepreciationExpenseAccount)
            .FirstOrDefaultAsync(a => a.Id == request.FixedAssetId && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(FixedAsset), request.FixedAssetId);

        if (asset.Status == FixedAssetStatus.Disposed)
            throw new BusinessRuleViolationException($"الأصل '{asset.NameAr}' تم استبعاده وبيعه ولا يمكن احتساب إهلاك له.");

        var existingAccum = await _context.AssetDepreciations
            .Where(d => d.FixedAssetId == asset.Id && d.CompanyId == companyId)
            .SumAsync(d => (decimal?)d.DepreciationAmount, cancellationToken) ?? 0m;

        decimal depreciableBase = asset.PurchaseCost - asset.SalvageValue;
        decimal remainingDepreciable = depreciableBase - existingAccum;

        if (remainingDepreciable <= 0.001m)
        {
            asset.Status = FixedAssetStatus.FullyDepreciated;
            await _context.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleViolationException($"الأصل '{asset.NameAr}' مهلك بالكامل ووصل للقيمة التخريدية.");
        }

        // Straight-Line Monthly Depreciation Amount
        decimal monthlyDepreciation = Math.Round(depreciableBase / asset.UsefulLifeMonths, 4);
        decimal depreciationAmount = Math.Min(monthlyDepreciation, remainingDepreciable);

        // 1. Fiscal Period
        FiscalPeriod fiscalPeriod;
        if (request.FiscalPeriodId.HasValue && request.FiscalPeriodId.Value > 0)
        {
            fiscalPeriod = await _context.FiscalPeriods
                .Include(p => p.FiscalYear)
                .FirstOrDefaultAsync(p => p.Id == request.FiscalPeriodId.Value && p.FiscalYear != null && p.FiscalYear.CompanyId == companyId, cancellationToken)
                ?? throw new NotFoundException(nameof(FiscalPeriod), request.FiscalPeriodId.Value);
        }
        else
        {
            fiscalPeriod = await _context.FiscalPeriods
                .Include(p => p.FiscalYear)
                .FirstOrDefaultAsync(p => p.FiscalYear != null && p.FiscalYear.CompanyId == companyId &&
                                          p.StartDate <= request.DepreciationDate &&
                                          p.EndDate >= request.DepreciationDate &&
                                          p.Status == FiscalPeriodStatus.Open, cancellationToken)
                ?? throw new ClosedFiscalPeriodException($"لا توجد فترة مالية مفتوحة لتاريخ الإهلاك {request.DepreciationDate:yyyy-MM-dd}.");
        }

        // 2. Automated Double-Entry Balanced GL Journal Entry
        // Debit: Depreciation Expense Account
        // Credit: Accumulated Depreciation Account
        var journalEntryDto = new CreateJournalEntryDto
        {
            EntryDate = request.DepreciationDate,
            ReferenceNumber = $"DEP-{asset.AssetCode}-{fiscalPeriod.NameAr}",
            Description = $"قيد إهلاك أصل {asset.NameAr} ({asset.AssetCode}) عن فترة {fiscalPeriod.NameAr}",
            BranchId = asset.BranchId,
            EntryType = JournalEntryType.Manual,
            SourceDocumentType = SourceDocumentType.AssetDepreciation,
            SourceDocumentId = $"{asset.AssetCode}-DEP-{fiscalPeriod.Id}",
            Lines = new List<CreateJournalEntryLineDto>
            {
                new()
                {
                    AccountId = asset.DepreciationExpenseAccountId,
                    Description = $"مصروف إهلاك أصل {asset.NameAr} ({asset.AssetCode})",
                    Debit = depreciationAmount,
                    Credit = 0m
                },
                new()
                {
                    AccountId = asset.AccumulatedDepreciationAccountId,
                    Description = $"مجمع إهلاك أصل {asset.NameAr} ({asset.AssetCode})",
                    Debit = 0m,
                    Credit = depreciationAmount
                }
            }
        };

        var postedEntry = await _journalEntryService.CreateAndPostAsync(journalEntryDto, cancellationToken);

        decimal bookValueAfter = Math.Max(asset.SalvageValue, asset.PurchaseCost - (existingAccum + depreciationAmount));

        var depRecord = new AssetDepreciation
        {
            CompanyId = companyId,
            FixedAssetId = asset.Id,
            FiscalPeriodId = fiscalPeriod.Id,
            DepreciationDate = request.DepreciationDate,
            DepreciationAmount = depreciationAmount,
            BookValueAfterDepreciation = bookValueAfter,
            JournalEntryId = postedEntry.Id
        };

        if (existingAccum + depreciationAmount >= depreciableBase - 0.001m)
        {
            asset.Status = FixedAssetStatus.FullyDepreciated;
        }

        await _context.AssetDepreciations.AddAsync(depRecord, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Posted Depreciation {Amount} for Asset {AssetCode}. BookValueAfter: {BookVal}",
            depreciationAmount, asset.AssetCode, bookValueAfter);

        return new AssetDepreciationDto
        {
            Id = depRecord.Id,
            CompanyId = depRecord.CompanyId,
            FixedAssetId = depRecord.FixedAssetId,
            AssetCode = asset.AssetCode,
            AssetName = asset.NameAr,
            FiscalPeriodId = depRecord.FiscalPeriodId,
            FiscalPeriodName = fiscalPeriod.NameAr,
            DepreciationDate = depRecord.DepreciationDate,
            DepreciationAmount = depRecord.DepreciationAmount,
            BookValueAfterDepreciation = depRecord.BookValueAfterDepreciation,
            JournalEntryId = depRecord.JournalEntryId,
            JournalEntryNumber = postedEntry.EntryNumber
        };
    }

    public async Task<List<AssetDepreciationDto>> GetAssetDepreciationsAsync(int fixedAssetId, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var list = await _context.AssetDepreciations
            .Include(d => d.FixedAsset)
            .Include(d => d.FiscalPeriod)
            .Include(d => d.JournalEntry)
            .Where(d => d.CompanyId == companyId && d.FixedAssetId == fixedAssetId)
            .OrderByDescending(d => d.DepreciationDate)
            .ToListAsync(cancellationToken);

        return list.Select(d => new AssetDepreciationDto
        {
            Id = d.Id,
            CompanyId = d.CompanyId,
            FixedAssetId = d.FixedAssetId,
            AssetCode = d.FixedAsset?.AssetCode ?? string.Empty,
            AssetName = d.FixedAsset?.NameAr ?? string.Empty,
            FiscalPeriodId = d.FiscalPeriodId,
            FiscalPeriodName = d.FiscalPeriod?.NameAr ?? string.Empty,
            DepreciationDate = d.DepreciationDate,
            DepreciationAmount = d.DepreciationAmount,
            BookValueAfterDepreciation = d.BookValueAfterDepreciation,
            JournalEntryId = d.JournalEntryId,
            JournalEntryNumber = d.JournalEntry?.EntryNumber
        }).ToList();
    }

    public async Task<AssetDisposalResultDto> DisposeAssetAsync(DisposeFixedAssetDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var asset = await _context.FixedAssets
            .FirstOrDefaultAsync(a => a.Id == request.FixedAssetId && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(FixedAsset), request.FixedAssetId);

        if (asset.Status == FixedAssetStatus.Disposed)
            throw new BusinessRuleViolationException($"الأصل '{asset.NameAr}' تم استبعاده وبيعه بالفعل مسبقاً.");

        // 1. Calculate accumulated depreciation & book value
        var accumulated = await _context.AssetDepreciations
            .Where(d => d.FixedAssetId == asset.Id && d.CompanyId == companyId)
            .SumAsync(d => (decimal?)d.DepreciationAmount, cancellationToken) ?? 0m;

        decimal netBookValue = Math.Max(0m, asset.PurchaseCost - accumulated);
        decimal saleProceeds = request.SaleProceeds;
        decimal gainLoss = saleProceeds - netBookValue;

        // 2. Resolve Bank/Cash GL Account
        int cashAccountId;
        if (request.BankAccountId.HasValue && request.BankAccountId.Value > 0)
        {
            var ba = await _context.BankAccounts.FirstOrDefaultAsync(b => b.Id == request.BankAccountId.Value && b.CompanyId == companyId, cancellationToken)
                ?? throw new NotFoundException(nameof(BankAccount), request.BankAccountId.Value);
            cashAccountId = ba.AccountId;
        }
        else
        {
            // Default Main Cash Account
            var defCash = await _context.Accounts.FirstOrDefaultAsync(a => a.CompanyId == companyId && a.AccountCode == "110101", cancellationToken);
            cashAccountId = defCash?.Id ?? throw new BusinessRuleViolationException("لم يتم العثور على حساب نقدية أو بنك لاستلام متحصلات البيع.");
        }

        // Default Gain/Loss GL Account (520401 or Other Revenue)
        var defGainLoss = await _context.Accounts.FirstOrDefaultAsync(a => a.CompanyId == companyId && a.AccountCode == "520401", cancellationToken);
        int gainLossAccountId = defGainLoss?.Id ?? asset.DepreciationExpenseAccountId;

        // 3. Build Balanced Journal Entry for Disposal
        // Equation:
        // Debits:
        //   - Cash/Bank (Sale Proceeds) [if proceeds > 0]
        //   - Accumulated Depreciation (clears accumulated depreciation) [if accumulated > 0]
        //   - Loss on Disposal [if gainLoss < 0]
        // Credits:
        //   - Fixed Asset Account (Purchase Cost)
        //   - Gain on Disposal [if gainLoss > 0]
        var lines = new List<CreateJournalEntryLineDto>();

        if (saleProceeds > 0)
        {
            lines.Add(new CreateJournalEntryLineDto
            {
                AccountId = cashAccountId,
                Description = $"متحصلات بيع الأصل {asset.NameAr}",
                Debit = saleProceeds,
                Credit = 0m
            });
        }

        if (accumulated > 0)
        {
            lines.Add(new CreateJournalEntryLineDto
            {
                AccountId = asset.AccumulatedDepreciationAccountId,
                Description = $"إقفال مجمع إهلاك الأصل {asset.NameAr}",
                Debit = accumulated,
                Credit = 0m
            });
        }

        if (gainLoss < 0)
        {
            // Loss on Disposal
            lines.Add(new CreateJournalEntryLineDto
            {
                AccountId = gainLossAccountId,
                Description = $"خسائر استبعاد وبيع الأصل {asset.NameAr}",
                Debit = Math.Abs(gainLoss),
                Credit = 0m
            });
        }

        // Credit Asset Account with historical purchase cost
        lines.Add(new CreateJournalEntryLineDto
        {
            AccountId = asset.AssetAccountId,
            Description = $"استبعاد التكلفة التاريخية للأصل {asset.NameAr}",
            Debit = 0m,
            Credit = asset.PurchaseCost
        });

        if (gainLoss > 0)
        {
            // Gain on Disposal
            lines.Add(new CreateJournalEntryLineDto
            {
                AccountId = gainLossAccountId,
                Description = $"أرباح رأسمالية من بيع الأصل {asset.NameAr}",
                Debit = 0m,
                Credit = gainLoss
            });
        }

        var entryDto = new CreateJournalEntryDto
        {
            EntryDate = request.DisposalDate,
            ReferenceNumber = $"DISP-{asset.AssetCode}",
            Description = $"قيد استبعاد وتكهين/بيع الأصل {asset.NameAr} ({asset.AssetCode})",
            BranchId = asset.BranchId,
            EntryType = JournalEntryType.Manual,
            SourceDocumentType = SourceDocumentType.AssetDepreciation,
            SourceDocumentId = $"{asset.AssetCode}-DISPOSAL",
            Lines = lines
        };

        var postedEntry = await _journalEntryService.CreateAndPostAsync(entryDto, cancellationToken);

        asset.Status = FixedAssetStatus.Disposed;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fixed Asset {AssetCode} Disposed. Proceeds: {Proceeds}, NetBookValue: {NBV}, GainLoss: {GainLoss}, Journal: {EntryNumber}",
            asset.AssetCode, saleProceeds, netBookValue, gainLoss, postedEntry.EntryNumber);

        return new AssetDisposalResultDto
        {
            FixedAssetId = asset.Id,
            AssetCode = asset.AssetCode,
            PurchaseCost = asset.PurchaseCost,
            AccumulatedDepreciation = accumulated,
            NetBookValue = netBookValue,
            SaleProceeds = saleProceeds,
            JournalEntryId = postedEntry.Id,
            JournalEntryNumber = postedEntry.EntryNumber
        };
    }

    private static FixedAssetDto MapToDto(FixedAsset a, decimal accumulated)
    {
        return new FixedAssetDto
        {
            Id = a.Id,
            CompanyId = a.CompanyId,
            BranchId = a.BranchId,
            BranchName = a.Branch?.NameAr ?? string.Empty,
            AssetCode = a.AssetCode,
            NameAr = a.NameAr,
            NameEn = a.NameEn,
            PurchaseDate = a.PurchaseDate,
            PurchaseCost = a.PurchaseCost,
            SalvageValue = a.SalvageValue,
            UsefulLifeMonths = a.UsefulLifeMonths,
            DepreciationMethod = a.DepreciationMethod,
            AssetAccountId = a.AssetAccountId,
            AssetAccountCode = a.AssetAccount?.AccountCode,
            AssetAccountName = a.AssetAccount?.NameAr,
            AccumulatedDepreciationAccountId = a.AccumulatedDepreciationAccountId,
            AccumulatedDepreciationAccountCode = a.AccumulatedDepreciationAccount?.AccountCode,
            AccumulatedDepreciationAccountName = a.AccumulatedDepreciationAccount?.NameAr,
            DepreciationExpenseAccountId = a.DepreciationExpenseAccountId,
            DepreciationExpenseAccountCode = a.DepreciationExpenseAccount?.AccountCode,
            DepreciationExpenseAccountName = a.DepreciationExpenseAccount?.NameAr,
            Status = a.Status,
            TotalAccumulatedDepreciation = accumulated
        };
    }
}
