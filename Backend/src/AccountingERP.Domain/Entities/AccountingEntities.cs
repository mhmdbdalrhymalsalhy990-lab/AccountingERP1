using AccountingERP.Domain.Common;
using AccountingERP.Domain.Enums;

namespace AccountingERP.Domain.Entities;

public class Account : AuditableEntity<int>, ITenantEntity, ISoftDelete
{
    public int CompanyId { get; set; }
    public int? ParentAccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }
    public int Level { get; set; }
    public bool IsPosting { get; set; }
    public int? CurrencyId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Account? ParentAccount { get; set; }
    public Currency? Currency { get; set; }
    public ICollection<Account> SubAccounts { get; set; } = new List<Account>();
    public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
}

public class FiscalYear : AuditableEntity<int>, ITenantEntity, IConcurrencyAware
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "FY 2026"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedByUserId { get; set; }
    public long? ClosingJournalEntryId { get; set; }
    public byte[]? RowVersion { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public JournalEntry? ClosingJournalEntry { get; set; }
    public ICollection<FiscalPeriod> Periods { get; set; } = new List<FiscalPeriod>();
}

public class FiscalPeriod : AuditableEntity<int>, ITenantEntity
{
    public int CompanyId { get; set; }
    public int FiscalYearId { get; set; }
    public int PeriodNumber { get; set; } // 1..12 or 13
    public FiscalPeriodType PeriodType { get; set; } = FiscalPeriodType.Regular;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public FiscalPeriodStatus Status { get; set; } = FiscalPeriodStatus.Open;

    // Navigation
    public Company? Company { get; set; }
    public FiscalYear? FiscalYear { get; set; }
    public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
}

public class DocumentSequence : BaseEntity<int>, ITenantEntity
{
    public int CompanyId { get; set; }
    public int FiscalYearId { get; set; }
    public int DocumentType { get; set; } // e.g. 1 = JournalEntry, 2 = SalesInvoice...
    public string Prefix { get; set; } = "JV";
    public long LastNumber { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public FiscalYear? FiscalYear { get; set; }
}

public class JournalEntry : AuditableEntity<long>, ITenantEntity, IBranchEntity, IConcurrencyAware
{
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int FiscalPeriodId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public JournalEntryType EntryType { get; set; } = JournalEntryType.Manual;
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? PostedByUserId { get; set; }
    public bool IsReversed { get; set; }
    public long? ReversedEntryId { get; set; }
    public SourceDocumentType? SourceDocumentType { get; set; }
    public string? SourceDocumentId { get; set; }
    public byte[]? RowVersion { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }
    public JournalEntry? ReversedEntry { get; set; }
    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}

public class JournalEntryLine : BaseEntity<long>, ITenantEntity, IBranchEntity
{
    public long JournalEntryId { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int AccountId { get; set; }
    public int LineNumber { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? ForeignCurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1.000000m;
    public decimal ForeignDebit { get; set; }
    public decimal ForeignCredit { get; set; }
    public int? CostCenterId { get; set; }

    // Navigation
    public JournalEntry? JournalEntry { get; set; }
    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
    public Account? Account { get; set; }
    public Currency? ForeignCurrency { get; set; }
    public CostCenter? CostCenter { get; set; }
}

public class CostCenter : AuditableEntity<int>, ITenantEntity, ISoftDelete
{
    public int CompanyId { get; set; }
    public int? ParentCostCenterId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public CostCenter? ParentCostCenter { get; set; }
    public ICollection<CostCenter> SubCostCenters { get; set; } = new List<CostCenter>();
}
