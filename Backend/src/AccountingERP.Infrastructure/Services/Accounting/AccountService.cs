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

public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<AccountService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId()
    {
        return _currentTenantService.CompanyId 
            ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب (Company context is missing).");
    }

    public async Task<AccountDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var account = await _context.Accounts
            .Include(a => a.ParentAccount)
            .Include(a => a.Currency)
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        return MapToDto(account);
    }

    public async Task<AccountDto> GetByCodeAsync(string accountCode, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var account = await _context.Accounts
            .Include(a => a.ParentAccount)
            .Include(a => a.Currency)
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.AccountCode == accountCode && a.CompanyId == companyId && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), accountCode);

        return MapToDto(account);
    }

    public async Task<List<AccountTreeDto>> GetAccountTreeAsync(AccountType? type = null, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.Accounts
            .Where(a => a.CompanyId == companyId && !a.IsDeleted);

        if (type.HasValue)
        {
            query = query.Where(a => a.AccountType == type.Value);
        }

        var allAccounts = await query
            .OrderBy(a => a.AccountCode)
            .ToListAsync(cancellationToken);

        // Group by ParentAccountId to build tree efficiently in-memory
        var lookup = allAccounts.ToLookup(a => a.ParentAccountId);

        List<AccountTreeDto> BuildTree(int? parentId)
        {
            return lookup[parentId].Select(a => new AccountTreeDto
            {
                Id = a.Id,
                ParentAccountId = a.ParentAccountId,
                AccountCode = a.AccountCode,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                AccountType = a.AccountType,
                Nature = a.Nature,
                Level = a.Level,
                IsPosting = a.IsPosting,
                IsActive = a.IsActive,
                CurrencyId = a.CurrencyId,
                Children = BuildTree(a.Id)
            }).ToList();
        }

        return BuildTree(null);
    }

    public async Task<List<AccountLookupDto>> GetPostingAccountsLookupAsync(AccountType? type = null, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.Accounts
            .Where(a => a.CompanyId == companyId && a.IsPosting && a.IsActive && !a.IsDeleted);

        if (type.HasValue)
        {
            query = query.Where(a => a.AccountType == type.Value);
        }

        return await query
            .OrderBy(a => a.AccountCode)
            .Select(a => new AccountLookupDto
            {
                Id = a.Id,
                AccountCode = a.AccountCode,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                AccountType = a.AccountType,
                Nature = a.Nature,
                CurrencyId = a.CurrencyId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AccountDto>> GetAccountsPagedAsync(AccountFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.Accounts
            .Include(a => a.ParentAccount)
            .Include(a => a.Currency)
            .Include(a => a.SubAccounts)
            .Where(a => a.CompanyId == companyId && !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            query = query.Where(a => a.AccountCode.Contains(search) || a.NameAr.Contains(search) || a.NameEn.Contains(search));
        }

        if (filter.AccountType.HasValue)
            query = query.Where(a => a.AccountType == filter.AccountType.Value);

        if (filter.Level.HasValue)
            query = query.Where(a => a.Level == filter.Level.Value);

        if (filter.IsPosting.HasValue)
            query = query.Where(a => a.IsPosting == filter.IsPosting.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(a => a.IsActive == filter.IsActive.Value);

        if (filter.ParentAccountId.HasValue)
            query = query.Where(a => a.ParentAccountId == filter.ParentAccountId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var items = await query
            .OrderBy(a => a.AccountCode)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<AccountDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<string> GenerateNextAccountCodeAsync(int parentAccountId, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var parent = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == parentAccountId && a.CompanyId == companyId && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), parentAccountId);

        var siblingCodes = await _context.Accounts
            .Where(a => a.ParentAccountId == parentAccountId && a.CompanyId == companyId && !a.IsDeleted)
            .Select(a => a.AccountCode)
            .ToListAsync(cancellationToken);

        if (siblingCodes.Count == 0)
        {
            // Level 1 -> Level 2: parent "1" -> "11"
            if (parent.Level == 1) return $"{parent.AccountCode}1";
            // Level 2 -> Level 3: parent "11" -> "1101"
            if (parent.Level == 2) return $"{parent.AccountCode}01";
            // Level 3 -> Level 4: parent "1101" -> "110101"
            return $"{parent.AccountCode}01";
        }

        // Find highest existing sibling number
        int maxSuffix = 0;
        int suffixLength = 2;

        foreach (var code in siblingCodes)
        {
            if (code.StartsWith(parent.AccountCode) && code.Length > parent.AccountCode.Length)
            {
                var suffixStr = code[parent.AccountCode.Length..];
                if (int.TryParse(suffixStr, out var num))
                {
                    if (num > maxSuffix)
                    {
                        maxSuffix = num;
                        suffixLength = Math.Max(suffixLength, suffixStr.Length);
                    }
                }
            }
        }

        var nextNumber = maxSuffix + 1;
        return $"{parent.AccountCode}{nextNumber.ToString($"D{suffixLength}")}";
    }

    public async Task<AccountDto> CreateAccountAsync(CreateAccountDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        Account? parent = null;
        int level = 1;
        AccountType accountType;
        AccountNature nature;

        if (request.ParentAccountId.HasValue)
        {
            parent = await _context.Accounts
                .Include(p => p.JournalEntryLines)
                .FirstOrDefaultAsync(a => a.Id == request.ParentAccountId.Value && a.CompanyId == companyId && !a.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(Account), request.ParentAccountId.Value);

            // Rule: Cannot add sub-accounts to an account that has active journal entries
            var hasEntries = await _context.JournalEntryLines.AnyAsync(jel => jel.AccountId == parent.Id && jel.CompanyId == companyId, cancellationToken);
            if (hasEntries)
            {
                throw new InvalidOperationException($"لا يمكن إنشاء حساب فرعي تحت الحساب '{parent.AccountCode}' نظراً لاحتوائه على قيود يومية وحركات محاسبية مرحّلة أو مسجلة.");
            }

            // If parent was marked as posting, automatically switch it to summary account (IsPosting = false)
            if (parent.IsPosting)
            {
                parent.IsPosting = false;
                _logger.LogInformation("Account {AccountCode} automatically converted to summary account (IsPosting = false) because a sub-account is added.", parent.AccountCode);
            }

            level = parent.Level + 1;
            accountType = parent.AccountType;
            nature = request.Nature ?? parent.Nature;
        }
        else
        {
            // Root Account
            if (!request.AccountType.HasValue)
                throw new ArgumentException("نوع الحساب مطلوب عند إنشاء حساب رئيسي من المستوى الأول.", nameof(request.AccountType));

            accountType = request.AccountType.Value;
            nature = request.Nature ?? (accountType is AccountType.Asset or AccountType.Expense ? AccountNature.Debit : AccountNature.Credit);
        }

        // Code determination
        string code = request.AccountCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            if (parent == null)
            {
                throw new ArgumentException("يجب تحديد رقم الحساب للحسابات الرئيسية من المستوى الأول.", nameof(request.AccountCode));
            }
            code = await GenerateNextAccountCodeAsync(parent.Id, cancellationToken);
        }

        // Code uniqueness check in company
        var codeExists = await _context.Accounts.AnyAsync(a => a.AccountCode == code && a.CompanyId == companyId && !a.IsDeleted, cancellationToken);
        if (codeExists)
        {
            throw new InvalidOperationException($"رقم الحساب '{code}' مسجل بالفعل في هذه الشركة.");
        }

        // Currency validation
        if (request.CurrencyId.HasValue)
        {
            var currencyExists = await _context.Currencies.AnyAsync(c => c.Id == request.CurrencyId.Value && c.IsActive, cancellationToken);
            if (!currencyExists)
                throw new NotFoundException(nameof(Currency), request.CurrencyId.Value);
        }

        var account = new Account
        {
            CompanyId = companyId,
            ParentAccountId = parent?.Id,
            AccountCode = code,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            AccountType = accountType,
            Nature = nature,
            Level = level,
            IsPosting = request.IsPosting,
            CurrencyId = request.CurrencyId,
            IsActive = request.IsActive
        };

        await _context.Accounts.AddAsync(account, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Account created successfully: {AccountCode} - {NameAr} (Id: {Id})", account.AccountCode, account.NameAr, account.Id);

        return MapToDto(account);
    }

    public async Task<AccountDto> UpdateAccountAsync(int id, UpdateAccountDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var account = await _context.Accounts
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        if (!request.IsActive && account.IsActive)
        {
            // Check if account has active sub-accounts
            var hasActiveSub = account.SubAccounts.Any(s => s.IsActive && !s.IsDeleted);
            if (hasActiveSub)
            {
                throw new InvalidOperationException($"لا يمكن تعطيل الحساب '{account.AccountCode}' لوجود حسابات فرعية نشطة تابعة له. قم بتعطيل الحسابات الفرعية أولاً.");
            }
        }

        if (request.CurrencyId.HasValue)
        {
            var currencyExists = await _context.Currencies.AnyAsync(c => c.Id == request.CurrencyId.Value && c.IsActive, cancellationToken);
            if (!currencyExists)
                throw new NotFoundException(nameof(Currency), request.CurrencyId.Value);
        }

        account.NameAr = request.NameAr.Trim();
        account.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim();
        account.Nature = request.Nature;
        account.CurrencyId = request.CurrencyId;
        account.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(account);
    }

    public async Task DeleteAccountAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var account = await _context.Accounts
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        // Rule 1: Cannot delete if has sub-accounts
        if (account.SubAccounts.Any(s => !s.IsDeleted))
        {
            throw new InvalidOperationException($"لا يمكن حذف الحساب '{account.AccountCode}' نظراً لاحتوائه على حسابات فرعية تابعة له. يجب حذف الحسابات الفرعية أولاً.");
        }

        // Rule 2: Cannot delete if has journal entry lines
        var hasEntries = await _context.JournalEntryLines.AnyAsync(jel => jel.AccountId == id && jel.CompanyId == companyId, cancellationToken);
        if (hasEntries)
        {
            throw new InvalidOperationException($"لا يمكن حذف الحساب '{account.AccountCode}' لوجود حركات وقيود محاسبية مسجلة عليه.");
        }

        // Rule 3: Cannot delete if configured in CompanySettings
        var isUsedInSettings = await _context.CompanySettings.AnyAsync(s => s.CompanyId == companyId &&
            (s.RetainedEarningsAccountId == id ||
             s.CurrentYearEarningsAccountId == id ||
             s.ForexGainLossAccountId == id ||
             s.DefaultVatAccountId == id ||
             s.SalesRevenueAccountId == id ||
             s.PurchaseExpenseAccountId == id ||
             s.InventoryAssetAccountId == id ||
             s.CogsAccountId == id), cancellationToken);

        if (isUsedInSettings)
        {
            throw new InvalidOperationException($"لا يمكن حذف الحساب '{account.AccountCode}' لأنه مستخدم في الإعدادات المالية العامة للشركة.");
        }

        // Rule 4: Cannot delete if linked to BankAccount or TaxType
        var isLinkedBank = await _context.BankAccounts.AnyAsync(ba => ba.AccountId == id && ba.CompanyId == companyId, cancellationToken);
        if (isLinkedBank)
        {
            throw new InvalidOperationException($"لا يمكن حذف الحساب '{account.AccountCode}' لأنه مرتبط بحساب بنكي أو صندوق نقدي.");
        }

        var isLinkedTax = await _context.TaxTypes.AnyAsync(tt => tt.AccountId == id && tt.CompanyId == companyId, cancellationToken);
        if (isLinkedTax)
        {
            throw new InvalidOperationException($"لا يمكن حذف الحساب '{account.AccountCode}' لأنه مرتبط بإعدادات ضريبية.");
        }

        // Soft delete
        account.IsDeleted = true;
        account.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Account deleted (soft-delete): {AccountCode} (Id: {Id})", account.AccountCode, account.Id);
    }

    public async Task<bool> ToggleAccountStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var account = await _context.Accounts
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        if (account.IsActive)
        {
            // Deactivating: check active sub-accounts
            if (account.SubAccounts.Any(s => s.IsActive && !s.IsDeleted))
            {
                throw new InvalidOperationException($"لا يمكن تعطيل الحساب '{account.AccountCode}' لوجود حسابات فرعية نشطة تابعة له.");
            }
            account.IsActive = false;
        }
        else
        {
            account.IsActive = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return account.IsActive;
    }

    private static AccountDto MapToDto(Account a)
    {
        return new AccountDto
        {
            Id = a.Id,
            CompanyId = a.CompanyId,
            ParentAccountId = a.ParentAccountId,
            ParentAccountName = a.ParentAccount?.NameAr,
            ParentAccountCode = a.ParentAccount?.AccountCode,
            AccountCode = a.AccountCode,
            NameAr = a.NameAr,
            NameEn = a.NameEn,
            AccountType = a.AccountType,
            Nature = a.Nature,
            Level = a.Level,
            IsPosting = a.IsPosting,
            CurrencyId = a.CurrencyId,
            CurrencyCode = a.Currency?.Code,
            IsActive = a.IsActive,
            HasSubAccounts = a.SubAccounts.Any(s => !s.IsDeleted),
            CreatedAt = a.CreatedAt
        };
    }
}
