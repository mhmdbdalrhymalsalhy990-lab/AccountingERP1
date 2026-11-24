using AccountingERP.Domain.Common;
using AccountingERP.Domain.Enums;

namespace AccountingERP.Domain.Entities;

public class BankAccount : AuditableEntity<int>, ITenantEntity, IBranchEntity
{
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? AccountNumber { get; set; } // IBAN / Bank Account #
    public int CurrencyId { get; set; }
    public int AccountId { get; set; } // Linked GL Account
    public bool IsCash { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
    public Currency? Currency { get; set; }
    public Account? Account { get; set; }
    public ICollection<BankReconciliation> Reconciliations { get; set; } = new List<BankReconciliation>();
}

public class BankReconciliation : AuditableEntity<long>, ITenantEntity
{
    public int CompanyId { get; set; }
    public int BankAccountId { get; set; }
    public DateTime ReconciliationDate { get; set; }
    public decimal StatementEndingBalance { get; set; }
    public decimal BookBalance { get; set; }
    public decimal Difference { get; set; }
    public bool IsReconciled { get; set; }
    public DateTime? ReconciledAt { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public BankAccount? BankAccount { get; set; }
}

public class Warehouse : AuditableEntity<int>, ITenantEntity, IBranchEntity, ISoftDelete
{
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();
}

public class Product : AuditableEntity<int>, ITenantEntity, ISoftDelete
{
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public ProductType ProductType { get; set; } = ProductType.Storable;
    public string? Barcode { get; set; }
    public string UnitName { get; set; } = "Unit";
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int? InventoryAccountId { get; set; }
    public int? CogsAccountId { get; set; }
    public int? SalesAccountId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Account? InventoryAccount { get; set; }
    public Account? CogsAccount { get; set; }
    public Account? SalesAccount { get; set; }
    public ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();
}

public class StockTransaction : AuditableEntity<long>, ITenantEntity
{
    public int CompanyId { get; set; }
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public StockTransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public SourceDocumentType? ReferenceDocumentType { get; set; }
    public string? ReferenceDocumentId { get; set; }
    public long? JournalEntryId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Product? Product { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
