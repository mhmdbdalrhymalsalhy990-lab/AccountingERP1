using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

public class JournalEntryService : IJournalEntryService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<JournalEntryService> _logger;

    public JournalEntryService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService,
        ILogger<JournalEntryService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private int EnsureCompanyId()
    {
        return _currentTenantService.CompanyId
            ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب (Company context is missing).");
    }

    public async Task<JournalEntryDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var entry = await _context.JournalEntries
            .Include(je => je.Branch)
            .Include(je => je.FiscalPeriod)
            .Include(je => je.ReversedEntry)
            .Include(je => je.Lines)
                .ThenInclude(l => l.Account)
            .Include(je => je.Lines)
                .ThenInclude(l => l.ForeignCurrency)
            .Include(je => je.Lines)
                .ThenInclude(l => l.CostCenter)
            .FirstOrDefaultAsync(je => je.Id == id && je.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), id);

        return MapToDto(entry);
    }

    public async Task<JournalEntryDto> GetByEntryNumberAsync(string entryNumber, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var entry = await _context.JournalEntries
            .Include(je => je.Branch)
            .Include(je => je.FiscalPeriod)
            .Include(je => je.ReversedEntry)
            .Include(je => je.Lines)
                .ThenInclude(l => l.Account)
            .Include(je => je.Lines)
                .ThenInclude(l => l.ForeignCurrency)
            .Include(je => je.Lines)
                .ThenInclude(l => l.CostCenter)
            .FirstOrDefaultAsync(je => je.EntryNumber == entryNumber && je.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), entryNumber);

        return MapToDto(entry);
    }

    public async Task<PagedResult<JournalEntryDto>> GetPagedAsync(JournalEntryFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.JournalEntries
            .Include(je => je.Branch)
            .Include(je => je.FiscalPeriod)
            .Include(je => je.ReversedEntry)
            .Include(je => je.Lines)
                .ThenInclude(l => l.Account)
            .Include(je => je.Lines)
                .ThenInclude(l => l.ForeignCurrency)
            .Include(je => je.Lines)
                .ThenInclude(l => l.CostCenter)
            .Where(je => je.CompanyId == companyId);

        if (filter.FromDate.HasValue)
            query = query.Where(je => je.EntryDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(je => je.EntryDate <= filter.ToDate.Value);

        if (filter.Status.HasValue)
            query = query.Where(je => je.Status == filter.Status.Value);

        if (filter.EntryType.HasValue)
            query = query.Where(je => je.EntryType == filter.EntryType.Value);

        if (filter.BranchId.HasValue)
            query = query.Where(je => je.BranchId == filter.BranchId.Value);

        if (filter.FiscalPeriodId.HasValue)
            query = query.Where(je => je.FiscalPeriodId == filter.FiscalPeriodId.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            query = query.Where(je => je.EntryNumber.Contains(search) || je.Description.Contains(search) || (je.ReferenceNumber != null && je.ReferenceNumber.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var entries = await query
            .OrderByDescending(je => je.EntryDate)
            .ThenByDescending(je => je.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = entries.Select(MapToDto).ToList();
        return new PagedResult<JournalEntryDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<JournalEntryDto> CreateDraftAsync(CreateJournalEntryDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        var branchId = request.BranchId ?? _currentTenantService.BranchId;

        if (!branchId.HasValue)
        {
            var mainBranch = await _context.Branches.FirstOrDefaultAsync(b => b.CompanyId == companyId && b.IsMainBranch && !b.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("لم يتم تحديد الفرع ولا يوجد فرع رئيسي معرف للشركة.");
            branchId = mainBranch.Id;
        }

        var fiscalPeriod = await GetActiveFiscalPeriodAsync(companyId, request.EntryDate, cancellationToken);
        var (totalDebit, totalCredit) = await ValidateLinesAndCalculateTotalsAsync(companyId, request.Lines, cancellationToken);

        // Generate temporary draft number
        var draftNumber = $"DRAFT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        var journalEntry = new JournalEntry
        {
            CompanyId = companyId,
            BranchId = branchId.Value,
            FiscalPeriodId = fiscalPeriod.Id,
            EntryNumber = draftNumber,
            EntryDate = request.EntryDate,
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            Description = request.Description.Trim(),
            EntryType = request.EntryType,
            Status = JournalEntryStatus.Draft,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId
        };

        int lineNum = 1;
        foreach (var l in request.Lines)
        {
            var line = new JournalEntryLine
            {
                CompanyId = companyId,
                BranchId = branchId.Value,
                AccountId = l.AccountId,
                LineNumber = lineNum++,
                Description = string.IsNullOrWhiteSpace(l.Description) ? request.Description.Trim() : l.Description.Trim(),
                Debit = l.Debit,
                Credit = l.Credit,
                ForeignCurrencyId = l.ForeignCurrencyId,
                ExchangeRate = l.ExchangeRate ?? 1.0m,
                ForeignDebit = l.ForeignDebit ?? 0m,
                ForeignCredit = l.ForeignCredit ?? 0m,
                CostCenterId = l.CostCenterId
            };
            journalEntry.Lines.Add(line);
        }

        await _context.JournalEntries.AddAsync(journalEntry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Draft journal entry created: {EntryNumber} (Id: {Id})", journalEntry.EntryNumber, journalEntry.Id);
        return await GetByIdAsync(journalEntry.Id, cancellationToken);
    }

    public async Task<JournalEntryDto> UpdateDraftAsync(long id, UpdateJournalEntryDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var entry = await _context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == id && je.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), id);

        if (entry.Status != JournalEntryStatus.Draft)
        {
            throw new CannotModifyPostedEntryException(entry.EntryNumber);
        }

        var fiscalPeriod = await GetActiveFiscalPeriodAsync(companyId, request.EntryDate, cancellationToken);
        var (totalDebit, totalCredit) = await ValidateLinesAndCalculateTotalsAsync(companyId, request.Lines, cancellationToken);

        entry.EntryDate = request.EntryDate;
        entry.FiscalPeriodId = fiscalPeriod.Id;
        entry.ReferenceNumber = request.ReferenceNumber?.Trim();
        entry.Description = request.Description.Trim();
        entry.TotalDebit = totalDebit;
        entry.TotalCredit = totalCredit;

        if (request.BranchId.HasValue)
            entry.BranchId = request.BranchId.Value;

        // Replace Lines
        _context.JournalEntryLines.RemoveRange(entry.Lines);

        int lineNum = 1;
        foreach (var l in request.Lines)
        {
            var line = new JournalEntryLine
            {
                JournalEntryId = entry.Id,
                CompanyId = companyId,
                BranchId = entry.BranchId,
                AccountId = l.AccountId,
                LineNumber = lineNum++,
                Description = string.IsNullOrWhiteSpace(l.Description) ? request.Description.Trim() : l.Description.Trim(),
                Debit = l.Debit,
                Credit = l.Credit,
                ForeignCurrencyId = l.ForeignCurrencyId,
                ExchangeRate = l.ExchangeRate ?? 1.0m,
                ForeignDebit = l.ForeignDebit ?? 0m,
                ForeignCredit = l.ForeignCredit ?? 0m,
                CostCenterId = l.CostCenterId
            };
            entry.Lines.Add(line);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Draft journal entry updated: {EntryNumber} (Id: {Id})", entry.EntryNumber, entry.Id);

        return await GetByIdAsync(entry.Id, cancellationToken);
    }

    public async Task DeleteDraftAsync(long id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var entry = await _context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == id && je.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), id);

        if (entry.Status != JournalEntryStatus.Draft)
        {
            throw new CannotModifyPostedEntryException(entry.EntryNumber);
        }

        _context.JournalEntryLines.RemoveRange(entry.Lines);
        _context.JournalEntries.Remove(entry);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Draft journal entry deleted: {EntryNumber} (Id: {Id})", entry.EntryNumber, entry.Id);
    }

    public async Task<JournalEntryDto> PostAsync(long id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var entry = await _context.JournalEntries
            .Include(je => je.FiscalPeriod!)
                .ThenInclude(fp => fp.FiscalYear)
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == id && je.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), id);

        if (entry.Status == JournalEntryStatus.Posted)
        {
            throw new InvalidOperationException($"القيد رقم '{entry.EntryNumber}' مرحّل بالفعل.");
        }

        if (entry.Status == JournalEntryStatus.Reversed)
        {
            throw new InvalidOperationException($"لا يمكن ترحيل القيد المعكوس '{entry.EntryNumber}'.");
        }

        // Re-validate Fiscal Period & Total Balance
        var fiscalPeriod = await GetActiveFiscalPeriodAsync(companyId, entry.EntryDate, cancellationToken);
        if (entry.TotalDebit != entry.TotalCredit || entry.TotalDebit <= 0)
        {
            throw new UnbalancedJournalEntryException(entry.TotalDebit, entry.TotalCredit);
        }

        // Generate Gapless Sequential Number
        var sequentialNumber = await GenerateGaplessPostingNumberAsync(companyId, fiscalPeriod.FiscalYearId, cancellationToken);

        entry.EntryNumber = sequentialNumber;
        entry.Status = JournalEntryStatus.Posted;
        entry.FiscalPeriodId = fiscalPeriod.Id;
        entry.PostedAt = DateTime.UtcNow;
        entry.PostedByUserId = _currentUserService.UserId ?? "System";

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Journal entry posted successfully with sequential number: {EntryNumber}", entry.EntryNumber);

        return await GetByIdAsync(entry.Id, cancellationToken);
    }

    public async Task<JournalEntryDto> CreateAndPostAsync(CreateJournalEntryDto request, CancellationToken cancellationToken = default)
    {
        var draft = await CreateDraftAsync(request, cancellationToken);
        return await PostAsync(draft.Id, cancellationToken);
    }

    public async Task<JournalEntryDto> ReverseAsync(long id, ReverseJournalEntryRequestDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var originalEntry = await _context.JournalEntries
            .Include(je => je.FiscalPeriod!)
                .ThenInclude(fp => fp.FiscalYear)
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == id && je.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), id);

        if (originalEntry.Status != JournalEntryStatus.Posted)
        {
            throw new InvalidOperationException("لا يمكن عكس قيد لم يتم ترحيله بعد.");
        }

        if (originalEntry.IsReversed)
        {
            throw new EntryAlreadyReversedException(originalEntry.EntryNumber);
        }

        if (originalEntry.EntryType == JournalEntryType.Reversal)
        {
            throw new InvalidOperationException("لا يمكن عكس قيد من نوع قيد عكسي.");
        }

        var reversalPeriod = await GetActiveFiscalPeriodAsync(companyId, request.ReversalDate, cancellationToken);
        var reversalNumber = await GenerateGaplessPostingNumberAsync(companyId, reversalPeriod.FiscalYearId, cancellationToken);

        var reversalEntry = new JournalEntry
        {
            CompanyId = companyId,
            BranchId = originalEntry.BranchId,
            FiscalPeriodId = reversalPeriod.Id,
            EntryNumber = reversalNumber,
            EntryDate = request.ReversalDate,
            ReferenceNumber = originalEntry.EntryNumber,
            Description = $"قيد عكسي للقيد رقم {originalEntry.EntryNumber} - السبب: {request.Reason.Trim()}",
            EntryType = JournalEntryType.Reversal,
            Status = JournalEntryStatus.Posted,
            TotalDebit = originalEntry.TotalCredit,
            TotalCredit = originalEntry.TotalDebit,
            PostedAt = DateTime.UtcNow,
            PostedByUserId = _currentUserService.UserId ?? "System",
            ReversedEntryId = originalEntry.Id
        };

        int lineNum = 1;
        foreach (var origLine in originalEntry.Lines.OrderBy(l => l.LineNumber))
        {
            var reversedLine = new JournalEntryLine
            {
                CompanyId = companyId,
                BranchId = originalEntry.BranchId,
                AccountId = origLine.AccountId,
                LineNumber = lineNum++,
                Description = $"عكس: {origLine.Description}",
                // Swap debit and credit
                Debit = origLine.Credit,
                Credit = origLine.Debit,
                ForeignDebit = origLine.ForeignCredit,
                ForeignCredit = origLine.ForeignDebit,
                ExchangeRate = origLine.ExchangeRate,
                ForeignCurrencyId = origLine.ForeignCurrencyId,
                CostCenterId = origLine.CostCenterId
            };
            reversalEntry.Lines.Add(reversedLine);
        }

        // Mark original entry as reversed
        originalEntry.IsReversed = true;

        await _context.JournalEntries.AddAsync(reversalEntry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Journal entry {OriginalNumber} reversed by entry {ReversalNumber}", originalEntry.EntryNumber, reversalEntry.EntryNumber);

        return await GetByIdAsync(reversalEntry.Id, cancellationToken);
    }

    private async Task<(decimal TotalDebit, decimal TotalCredit)> ValidateLinesAndCalculateTotalsAsync(
        int companyId, 
        List<CreateJournalEntryLineDto> lines, 
        CancellationToken cancellationToken)
    {
        if (lines.Count < 2)
        {
            throw new InvalidOperationException("يجب أن يحتوي القيد المحاسبي على سطرين على الأقل (طرف مدين وطرف دائن).");
        }

        var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id) && a.CompanyId == companyId && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        foreach (var line in lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account))
            {
                throw new NotFoundException(nameof(Account), line.AccountId);
            }

            if (!account.IsActive)
            {
                throw new InvalidOperationException($"الحساب '{account.AccountCode} - {account.NameAr}' غير مفعّل ولا يمكن إجراء قيود عليه.");
            }

            if (!account.IsPosting)
            {
                throw new HeaderAccountPostingException(account.AccountCode);
            }

            if (line.Debit < 0 || line.Credit < 0)
            {
                throw new InvalidOperationException("لا يمكن إدخال مبالغ سالبة في سطور القيد المحاسبي.");
            }

            if (line.Debit > 0 && line.Credit > 0)
            {
                throw new InvalidOperationException("لا يمكن أن يحتوي سطر القيد على مبلغ مدين ومبلغ دائن في نفس الوقت.");
            }

            if (line.Debit == 0 && line.Credit == 0)
            {
                // Try calculating from foreign currency if provided
                var exchangeRate = line.ExchangeRate.GetValueOrDefault(1.0m);
                var fDebit = line.ForeignDebit.GetValueOrDefault();
                var fCredit = line.ForeignCredit.GetValueOrDefault();

                if (line.ForeignCurrencyId.HasValue && exchangeRate > 0)
                {
                    if (fDebit > 0)
                        line.Debit = Math.Round(fDebit * exchangeRate, 4);
                    else if (fCredit > 0)
                        line.Credit = Math.Round(fCredit * exchangeRate, 4);
                }

                if (line.Debit == 0 && line.Credit == 0)
                {
                    throw new InvalidOperationException("يجب تحديد مبلغ مدين أو دائن في كل سطر من سطور القيد.");
                }
            }
        }

        var totalDebit = Math.Round(lines.Sum(l => l.Debit), 4);
        var totalCredit = Math.Round(lines.Sum(l => l.Credit), 4);

        if (totalDebit != totalCredit)
        {
            throw new UnbalancedJournalEntryException(totalDebit, totalCredit);
        }

        return (totalDebit, totalCredit);
    }

    private async Task<FiscalPeriod> GetActiveFiscalPeriodAsync(int companyId, DateTime entryDate, CancellationToken cancellationToken)
    {
        var period = await _context.FiscalPeriods
            .Include(fp => fp.FiscalYear)
            .FirstOrDefaultAsync(fp => fp.CompanyId == companyId && 
                                       fp.StartDate.Date <= entryDate.Date && 
                                       entryDate.Date <= fp.EndDate.Date, cancellationToken);

        if (period == null)
        {
            throw new ClosedFiscalPeriodException($"لا توجد فترة مالية معرّفة تغطي تاريخ القيد: {entryDate:yyyy-MM-dd}.");
        }

        if (period.Status != FiscalPeriodStatus.Open)
        {
            throw new ClosedFiscalPeriodException($"الفترة المالية '{period.NameAr}' مغلقة أو مقفلة ولا يمكن إجراء قيود بها.");
        }

        if (period.FiscalYear == null || period.FiscalYear.IsClosed)
        {
            throw new ClosedFiscalPeriodException($"السنة المالية '{period.FiscalYear?.Name}' مقفلة نهائياً.");
        }

        return period;
    }

    private async Task<string> GenerateGaplessPostingNumberAsync(int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var sequence = await _context.DocumentSequences
            .FirstOrDefaultAsync(ds => ds.CompanyId == companyId && ds.FiscalYearId == fiscalYearId && ds.DocumentType == 1, cancellationToken);

        if (sequence == null)
        {
            sequence = new DocumentSequence
            {
                CompanyId = companyId,
                FiscalYearId = fiscalYearId,
                DocumentType = 1, // JournalEntry
                Prefix = "JV",
                LastNumber = 0
            };
            await _context.DocumentSequences.AddAsync(sequence, cancellationToken);
        }

        sequence.LastNumber += 1;
        var fiscalYear = await _context.FiscalYears.FirstAsync(fy => fy.Id == fiscalYearId, cancellationToken);

        return $"{sequence.Prefix}-{fiscalYear.StartDate.Year}-{sequence.LastNumber:D6}";
    }

    private static JournalEntryDto MapToDto(JournalEntry je)
    {
        return new JournalEntryDto
        {
            Id = je.Id,
            CompanyId = je.CompanyId,
            BranchId = je.BranchId,
            BranchName = je.Branch?.NameAr ?? "",
            FiscalPeriodId = je.FiscalPeriodId,
            FiscalPeriodName = je.FiscalPeriod?.NameAr ?? "",
            EntryNumber = je.EntryNumber,
            EntryDate = je.EntryDate,
            ReferenceNumber = je.ReferenceNumber,
            Description = je.Description,
            EntryType = je.EntryType,
            Status = je.Status,
            TotalDebit = je.TotalDebit,
            TotalCredit = je.TotalCredit,
            PostedAt = je.PostedAt,
            PostedByUserId = je.PostedByUserId,
            IsReversed = je.IsReversed,
            ReversedEntryId = je.ReversedEntryId,
            ReversedEntryNumber = je.ReversedEntry?.EntryNumber,
            SourceDocumentType = je.SourceDocumentType,
            SourceDocumentId = je.SourceDocumentId,
            CreatedAt = je.CreatedAt,
            Lines = je.Lines.OrderBy(l => l.LineNumber).Select(l => new JournalEntryLineDto
            {
                Id = l.Id,
                JournalEntryId = l.JournalEntryId,
                AccountId = l.AccountId,
                AccountCode = l.Account?.AccountCode ?? "",
                AccountNameAr = l.Account?.NameAr ?? "",
                AccountNameEn = l.Account?.NameEn ?? "",
                LineNumber = l.LineNumber,
                Description = l.Description,
                Debit = l.Debit,
                Credit = l.Credit,
                ForeignCurrencyId = l.ForeignCurrencyId,
                ForeignCurrencyCode = l.ForeignCurrency?.Code,
                ExchangeRate = l.ExchangeRate,
                ForeignDebit = l.ForeignDebit,
                ForeignCredit = l.ForeignCredit,
                CostCenterId = l.CostCenterId,
                CostCenterName = l.CostCenter?.NameAr
            }).ToList()
        };
    }
}
