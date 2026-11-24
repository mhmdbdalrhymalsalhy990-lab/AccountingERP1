namespace AccountingERP.Application.DTOs.TreasuryAndInventory;

// ==========================================
// 1. Bank Account & Cash Box DTOs
// ==========================================
public class BankAccountDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public int CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public bool IsCash { get; set; }
    public bool IsActive { get; set; }
    public decimal BookBalance { get; set; }
}

public class CreateBankAccountDto
{
    public int? BranchId { get; set; }
    public string? Code { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public int? CurrencyId { get; set; }
    public int AccountId { get; set; } // Linked GL Account
    public bool IsCash { get; set; } = false;
    public bool IsActive { get; set; } = true;
}

public class UpdateBankAccountDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public bool IsActive { get; set; }
}

// ==========================================
// 2. Bank Reconciliation DTOs
// ==========================================
public class BankReconciliationDto
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BankAccountId { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
    public DateTime ReconciliationDate { get; set; }
    public decimal StatementEndingBalance { get; set; }
    public decimal BookBalance { get; set; }
    public decimal Difference { get; set; }
    public bool IsReconciled { get; set; }
    public DateTime? ReconciledAt { get; set; }
}

public class CreateBankReconciliationDto
{
    public int BankAccountId { get; set; }
    public DateTime ReconciliationDate { get; set; } = DateTime.UtcNow;
    public decimal StatementEndingBalance { get; set; }
}
