using AccountingERP.Domain.Common;
using AccountingERP.Domain.Enums;

namespace AccountingERP.Domain.Entities;

public class Customer : AuditableEntity<int>, ITenantEntity, ISoftDelete
{
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public int AccountId { get; set; }
    public decimal CreditLimit { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Account? Account { get; set; }
    public ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
}

public class Supplier : AuditableEntity<int>, ITenantEntity, ISoftDelete
{
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public int AccountId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Account? Account { get; set; }
    public ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();
}

public class SalesInvoice : AuditableEntity<long>, ITenantEntity, IBranchEntity, IConcurrencyAware
{
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public int CustomerId { get; set; }
    public int CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1.000000m;
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public long? JournalEntryId { get; set; }
    public byte[]? RowVersion { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
    public Customer? Customer { get; set; }
    public Currency? Currency { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}

public class SalesInvoiceItem : BaseEntity<long>
{
    public long SalesInvoiceId { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; }
    public int? TaxTypeId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }

    // Navigation
    public SalesInvoice? SalesInvoice { get; set; }
    public Product? Product { get; set; }
    public Warehouse? Warehouse { get; set; }
    public TaxType? TaxType { get; set; }
}

public class PurchaseInvoice : AuditableEntity<long>, ITenantEntity, IBranchEntity, IConcurrencyAware
{
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? VendorBillNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public int SupplierId { get; set; }
    public int CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1.000000m;
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public long? JournalEntryId { get; set; }
    public byte[]? RowVersion { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
    public Supplier? Supplier { get; set; }
    public Currency? Currency { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
}

public class PurchaseInvoiceItem : BaseEntity<long>
{
    public long PurchaseInvoiceId { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int? TaxTypeId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }

    // Navigation
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public Product? Product { get; set; }
    public Warehouse? Warehouse { get; set; }
    public TaxType? TaxType { get; set; }
}

public class Payment : AuditableEntity<long>, ITenantEntity, IBranchEntity, IConcurrencyAware
{
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public PaymentType PaymentType { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public DateTime PaymentDate { get; set; }
    public int BankAccountId { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public decimal Amount { get; set; }
    public int CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1.000000m;
    public decimal BaseAmount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public long? JournalEntryId { get; set; }
    public byte[]? RowVersion { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
    public BankAccount? BankAccount { get; set; }
    public Customer? Customer { get; set; }
    public Supplier? Supplier { get; set; }
    public Currency? Currency { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}

public class PaymentAllocation : BaseEntity<long>
{
    public long PaymentId { get; set; }
    public long? SalesInvoiceId { get; set; }
    public long? PurchaseInvoiceId { get; set; }
    public decimal AllocatedAmount { get; set; }

    // Navigation
    public Payment? Payment { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }
}
