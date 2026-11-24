using AccountingERP.Application.DTOs.TreasuryAndInventory;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

// ==========================================
// 1. Bank Account & Cash Box Service
// ==========================================
public class BankAccountService : IBankAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<BankAccountService> _logger;

    public BankAccountService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<BankAccountService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<List<BankAccountDto>> GetAllAsync(bool? isCash = null, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.BankAccounts
            .Include(b => b.Branch)
            .Include(b => b.Currency)
            .Include(b => b.Account)
            .Where(b => b.CompanyId == companyId);

        if (isCash.HasValue)
            query = query.Where(b => b.IsCash == isCash.Value);

        var list = await query.OrderBy(b => b.Code).ToListAsync(cancellationToken);

        var dtos = new List<BankAccountDto>();
        foreach (var b in list)
        {
            var bal = await CalculateBookBalanceAsync(companyId, b.AccountId, cancellationToken);
            dtos.Add(MapToDto(b, bal));
        }

        return dtos;
    }

    public async Task<BankAccountDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var bankAccount = await _context.BankAccounts
            .Include(b => b.Branch)
            .Include(b => b.Currency)
            .Include(b => b.Account)
            .FirstOrDefaultAsync(b => b.Id == id && b.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(BankAccount), id);

        var bal = await CalculateBookBalanceAsync(companyId, bankAccount.AccountId, cancellationToken);
        return MapToDto(bankAccount, bal);
    }

    public async Task<BankAccountDto> CreateAsync(CreateBankAccountDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var branchId = request.BranchId ?? _currentTenantService.BranchId
            ?? (await _context.Branches.Where(b => b.CompanyId == companyId).Select(b => b.Id).FirstOrDefaultAsync(cancellationToken));

        if (branchId == 0)
            throw new BusinessRuleViolationException("لا يوجد فرع محدد لربط الحساب البنكي / الصندوق به.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.AccountId);

        if (!account.IsPosting)
            throw new HeaderAccountPostingException(account.AccountCode);

        int currencyId = request.CurrencyId ?? 0;
        if (currencyId == 0)
        {
            var company = await _context.Companies.FirstAsync(c => c.Id == companyId, cancellationToken);
            currencyId = company.BaseCurrencyId;
        }

        string code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            var count = await _context.BankAccounts.CountAsync(b => b.CompanyId == companyId, cancellationToken);
            code = request.IsCash ? $"CSH-{(count + 1):D2}" : $"BA-{(count + 1):D2}";
        }

        var bankAccount = new BankAccount
        {
            CompanyId = companyId,
            BranchId = branchId,
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            AccountNumber = request.AccountNumber?.Trim(),
            CurrencyId = currencyId,
            AccountId = account.Id,
            IsCash = request.IsCash,
            IsActive = request.IsActive
        };

        await _context.BankAccounts.AddAsync(bankAccount, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bank/Cash Account '{Code}' ({NameAr}) created successfully.", bankAccount.Code, bankAccount.NameAr);
        return await GetByIdAsync(bankAccount.Id, cancellationToken);
    }

    public async Task<BankAccountDto> UpdateAsync(int id, UpdateBankAccountDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(b => b.Id == id && b.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(BankAccount), id);

        bankAccount.NameAr = request.NameAr.Trim();
        bankAccount.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim();
        bankAccount.AccountNumber = request.AccountNumber?.Trim();
        bankAccount.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(bankAccount.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(b => b.Id == id && b.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(BankAccount), id);

        var hasPayments = await _context.Payments.AnyAsync(p => p.BankAccountId == id, cancellationToken);
        var hasReconciliations = await _context.BankReconciliations.AnyAsync(r => r.BankAccountId == id, cancellationToken);

        if (hasPayments || hasReconciliations)
            throw new BusinessRuleViolationException($"لا يمكن حذف الحساب '{bankAccount.NameAr}' لوجود سندات مالية أو مذكرات تسوية بنكية مرتبطة به.");

        _context.BankAccounts.Remove(bankAccount);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bank Account {Id} deleted.", id);
    }

    public async Task<decimal> GetBookBalanceAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(b => b.Id == id && b.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(BankAccount), id);

        return await CalculateBookBalanceAsync(companyId, bankAccount.AccountId, cancellationToken);
    }

    private async Task<decimal> CalculateBookBalanceAsync(int companyId, int glAccountId, CancellationToken cancellationToken)
    {
        var query = _context.JournalEntryLines
            .Where(l => l.AccountId == glAccountId &&
                        l.JournalEntry != null &&
                        l.JournalEntry.CompanyId == companyId &&
                        l.JournalEntry.Status == JournalEntryStatus.Posted);

        var debitSum = await query.SumAsync(l => (decimal?)l.Debit, cancellationToken) ?? 0m;
        var creditSum = await query.SumAsync(l => (decimal?)l.Credit, cancellationToken) ?? 0m;

        // Cash and Bank accounts have Normal Balance = Debit
        return debitSum - creditSum;
    }

    private static BankAccountDto MapToDto(BankAccount b, decimal bookBalance)
    {
        return new BankAccountDto
        {
            Id = b.Id,
            CompanyId = b.CompanyId,
            BranchId = b.BranchId,
            BranchName = b.Branch?.NameAr ?? string.Empty,
            Code = b.Code,
            NameAr = b.NameAr,
            NameEn = b.NameEn,
            AccountNumber = b.AccountNumber,
            CurrencyId = b.CurrencyId,
            CurrencyCode = b.Currency?.Code ?? string.Empty,
            AccountId = b.AccountId,
            AccountCode = b.Account?.AccountCode ?? string.Empty,
            AccountName = b.Account?.NameAr ?? string.Empty,
            IsCash = b.IsCash,
            IsActive = b.IsActive,
            BookBalance = bookBalance
        };
    }
}

// ==========================================
// 2. Bank Reconciliation Service Implementation
// ==========================================
public class BankReconciliationService : IBankReconciliationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<BankReconciliationService> _logger;

    public BankReconciliationService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<BankReconciliationService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<List<BankReconciliationDto>> GetByBankAccountIdAsync(int bankAccountId, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var list = await _context.BankReconciliations
            .Include(r => r.BankAccount)
            .Where(r => r.CompanyId == companyId && r.BankAccountId == bankAccountId)
            .OrderByDescending(r => r.ReconciliationDate)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    public async Task<BankReconciliationDto> PerformReconciliationAsync(CreateBankReconciliationDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var bankAccount = await _context.BankAccounts
            .Include(b => b.Account)
            .FirstOrDefaultAsync(b => b.Id == request.BankAccountId && b.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(BankAccount), request.BankAccountId);

        // Compute book balance up to ReconciliationDate
        var query = _context.JournalEntryLines
            .Where(l => l.AccountId == bankAccount.AccountId &&
                        l.JournalEntry != null &&
                        l.JournalEntry.CompanyId == companyId &&
                        l.JournalEntry.Status == JournalEntryStatus.Posted &&
                        l.JournalEntry.EntryDate <= request.ReconciliationDate);

        var debitSum = await query.SumAsync(l => (decimal?)l.Debit, cancellationToken) ?? 0m;
        var creditSum = await query.SumAsync(l => (decimal?)l.Credit, cancellationToken) ?? 0m;
        var bookBalance = debitSum - creditSum;

        var difference = request.StatementEndingBalance - bookBalance;
        var isReconciled = Math.Abs(difference) < 0.001m;

        var reconciliation = new BankReconciliation
        {
            CompanyId = companyId,
            BankAccountId = bankAccount.Id,
            ReconciliationDate = request.ReconciliationDate,
            StatementEndingBalance = request.StatementEndingBalance,
            BookBalance = bookBalance,
            Difference = difference,
            IsReconciled = isReconciled,
            ReconciledAt = isReconciled ? DateTime.UtcNow : null
        };

        await _context.BankReconciliations.AddAsync(reconciliation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bank Reconciliation performed for Account {AccountCode}. Statement: {Statement}, Book: {Book}, Diff: {Diff}, Reconciled: {IsReconciled}",
            bankAccount.Code, request.StatementEndingBalance, bookBalance, difference, isReconciled);

        return MapToDto(reconciliation);
    }

    private static BankReconciliationDto MapToDto(BankReconciliation r)
    {
        return new BankReconciliationDto
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            BankAccountId = r.BankAccountId,
            BankAccountName = r.BankAccount?.NameAr ?? string.Empty,
            ReconciliationDate = r.ReconciliationDate,
            StatementEndingBalance = r.StatementEndingBalance,
            BookBalance = r.BookBalance,
            Difference = r.Difference,
            IsReconciled = r.IsReconciled,
            ReconciledAt = r.ReconciledAt
        };
    }
}
