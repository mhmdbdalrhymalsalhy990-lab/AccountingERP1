using AccountingERP.Domain.Common;

namespace AccountingERP.Domain.Entities;

public class Company : AuditableEntity<int>, ISoftDelete
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public string? CommercialRegistration { get; set; }
    public int CountryId { get; set; }
    public int BaseCurrencyId { get; set; }
    public string? LogoUrl { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Country? Country { get; set; }
    public Currency? BaseCurrency { get; set; }
    public CompanySettings? Settings { get; set; }
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<FiscalYear> FiscalYears { get; set; } = new List<FiscalYear>();
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();
}

public class Branch : AuditableEntity<int>, ITenantEntity, ISoftDelete
{
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsMainBranch { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Company? Company { get; set; }
}

public class CompanySettings : BaseEntity<int>, ITenantEntity
{
    public int CompanyId { get; set; }
    public int RetainedEarningsAccountId { get; set; }
    public int CurrentYearEarningsAccountId { get; set; }
    public int ForexGainLossAccountId { get; set; }
    public int? DefaultVatAccountId { get; set; }
    public int? SalesRevenueAccountId { get; set; }
    public int? PurchaseExpenseAccountId { get; set; }
    public int? InventoryAssetAccountId { get; set; }
    public int? CogsAccountId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Account? RetainedEarningsAccount { get; set; }
    public Account? CurrentYearEarningsAccount { get; set; }
    public Account? ForexGainLossAccount { get; set; }
    public Account? DefaultVatAccount { get; set; }
    public Account? SalesRevenueAccount { get; set; }
    public Account? PurchaseExpenseAccount { get; set; }
    public Account? InventoryAssetAccount { get; set; }
    public Account? CogsAccount { get; set; }
}
