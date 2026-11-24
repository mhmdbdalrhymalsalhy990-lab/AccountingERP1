using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.DTOs.Accounting;

public class CreateJournalEntryDto
{
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public DateTime? Date
    {
        get => EntryDate;
        set { if (value.HasValue) EntryDate = value.Value; }
    }
    public string? ReferenceNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public JournalEntryType EntryType { get; set; } = JournalEntryType.Manual;
    public SourceDocumentType? SourceDocumentType { get; set; }
    public string? SourceDocumentId { get; set; }
    public List<CreateJournalEntryLineDto> Lines { get; set; } = new();
}

public class CreateJournalEntryLineDto
{
    public int AccountId { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? ForeignCurrencyId { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal? ForeignDebit { get; set; }
    public decimal? ForeignCredit { get; set; }
    public int? CostCenterId { get; set; }
}

public class UpdateJournalEntryDto
{
    public DateTime EntryDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public List<CreateJournalEntryLineDto> Lines { get; set; } = new();
}

public class JournalEntryDto
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int FiscalPeriodId { get; set; }
    public string FiscalPeriodName { get; set; } = string.Empty;
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public JournalEntryType EntryType { get; set; }
    public string EntryTypeName => EntryType.ToString();
    public JournalEntryStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? PostedByUserId { get; set; }
    public bool IsReversed { get; set; }
    public long? ReversedEntryId { get; set; }
    public string? ReversedEntryNumber { get; set; }
    public SourceDocumentType? SourceDocumentType { get; set; }
    public string? SourceDocumentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<JournalEntryLineDto> Lines { get; set; } = new();
}

public class JournalEntryLineDto
{
    public long Id { get; set; }
    public long JournalEntryId { get; set; }
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? ForeignCurrencyId { get; set; }
    public string? ForeignCurrencyCode { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal ForeignDebit { get; set; }
    public decimal ForeignCredit { get; set; }
    public int? CostCenterId { get; set; }
    public string? CostCenterName { get; set; }
}

public class JournalEntryFilterRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public JournalEntryStatus? Status { get; set; }
    public JournalEntryType? EntryType { get; set; }
    public int? BranchId { get; set; }
    public int? FiscalPeriodId { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ReverseJournalEntryRequestDto
{
    public DateTime ReversalDate { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
}
