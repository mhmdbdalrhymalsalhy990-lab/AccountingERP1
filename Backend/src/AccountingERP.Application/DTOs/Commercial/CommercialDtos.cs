using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.DTOs.Commercial;

// --- 1. Customer DTOs ---
public class CustomerDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public int AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public decimal CreditLimit { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public decimal CurrentBalance { get; set; }
}

public class CreateCustomerDto
{
    public string? Code { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public int? AccountId { get; set; } // If null, uses default customer control account
    public decimal CreditLimit { get; set; } = 0m;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateCustomerDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public decimal CreditLimit { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}

// --- 2. Supplier DTOs ---
public class SupplierDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public int AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public decimal CurrentBalance { get; set; }
}

public class CreateSupplierDto
{
    public string? Code { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public int? AccountId { get; set; } // If null, uses default supplier control account
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSupplierDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}

// --- 3. Sales Invoice DTOs ---
public class SalesInvoiceDto
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public int CustomerId { get; set; }
    public string CustomerNameAr { get; set; } = string.Empty;
    public string CustomerNameEn { get; set; } = string.Empty;
    public int CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public InvoiceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public long? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
    public List<SalesInvoiceItemDto> Items { get; set; } = new();
}

public class SalesInvoiceItemDto
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; }
    public int? TaxTypeId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreateSalesInvoiceDto
{
    public int? BranchId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public int CustomerId { get; set; }
    public int? CurrencyId { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal DiscountTotal { get; set; } = 0m;
    public List<CreateInvoiceItemDto> Items { get; set; } = new();
}

public class CreateInvoiceItemDto
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; } = 0m;
    public int? TaxTypeId { get; set; }
}

// --- 4. Purchase Invoice DTOs ---
public class PurchaseInvoiceDto
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? VendorBillNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public int SupplierId { get; set; }
    public string SupplierNameAr { get; set; } = string.Empty;
    public string SupplierNameEn { get; set; } = string.Empty;
    public int CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public InvoiceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public long? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
    public List<PurchaseInvoiceItemDto> Items { get; set; } = new();
}

public class PurchaseInvoiceItemDto
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int? TaxTypeId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreatePurchaseInvoiceDto
{
    public int? BranchId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string? VendorBillNumber { get; set; }
    public int SupplierId { get; set; }
    public int? CurrencyId { get; set; }
    public decimal? ExchangeRate { get; set; }
    public List<CreateInvoiceItemDto> Items { get; set; } = new();
}

// --- 5. Payment & Allocation DTOs ---
public class PaymentDto
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string PaymentNumber { get; set; } = string.Empty;
    public PaymentType PaymentType { get; set; }
    public string PaymentTypeName => PaymentType.ToString();
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime PaymentDate { get; set; }
    public int BankAccountId { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal Amount { get; set; }
    public int CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal BaseAmount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }
    public InvoiceStatus Status { get; set; }
    public long? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
    public List<PaymentAllocationDto> Allocations { get; set; } = new();
}

public class PaymentAllocationDto
{
    public long Id { get; set; }
    public long PaymentId { get; set; }
    public long? SalesInvoiceId { get; set; }
    public string? SalesInvoiceNumber { get; set; }
    public long? PurchaseInvoiceId { get; set; }
    public string? PurchaseInvoiceNumber { get; set; }
    public decimal AllocatedAmount { get; set; }
}

public class CreatePaymentDto
{
    public int? BranchId { get; set; }
    public PaymentType PaymentType { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public int BankAccountId { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public decimal Amount { get; set; }
    public int? CurrencyId { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }
    public List<CreatePaymentAllocationDto> Allocations { get; set; } = new();
}

public class CreatePaymentAllocationDto
{
    public long? SalesInvoiceId { get; set; }
    public long? PurchaseInvoiceId { get; set; }
    public decimal AllocatedAmount { get; set; }
}
