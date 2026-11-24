using AccountingERP.Domain.Common;
using AccountingERP.Domain.Enums;

namespace AccountingERP.Domain.Entities;

public class FixedAsset : AuditableEntity<int>, ITenantEntity, IBranchEntity
{
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal SalvageValue { get; set; }
    public int UsefulLifeMonths { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public int AssetAccountId { get; set; }
    public int AccumulatedDepreciationAccountId { get; set; }
    public int DepreciationExpenseAccountId { get; set; }
    public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Active;

    // Navigation
    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
    public Account? AssetAccount { get; set; }
    public Account? AccumulatedDepreciationAccount { get; set; }
    public Account? DepreciationExpenseAccount { get; set; }
    public ICollection<AssetDepreciation> Depreciations { get; set; } = new List<AssetDepreciation>();
}

public class AssetDepreciation : AuditableEntity<long>, ITenantEntity
{
    public int CompanyId { get; set; }
    public int FixedAssetId { get; set; }
    public int FiscalPeriodId { get; set; }
    public DateTime DepreciationDate { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal BookValueAfterDepreciation { get; set; }
    public long? JournalEntryId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public FixedAsset? FixedAsset { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}

public class AuditLog : BaseEntity<long>, ITenantEntity
{
    public int CompanyId { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, Post, Reverse
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }

    // Navigation
    public Company? Company { get; set; }
}

public class Attachment : BaseEntity<long>, ITenantEntity
{
    public int CompanyId { get; set; }
    public string EntityName { get; set; } = string.Empty; // e.g. SalesInvoice, JournalEntry
    public string EntityId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Company? Company { get; set; }
}
