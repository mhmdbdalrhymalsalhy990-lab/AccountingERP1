using AccountingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingERP.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<CompanySettings> CompanySettings { get; }
    DbSet<Branch> Branches { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<ApplicationRole> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<CompanyUser> CompanyUsers { get; }
    DbSet<Country> Countries { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<TaxType> TaxTypes { get; }
    DbSet<TaxRate> TaxRates { get; }
    DbSet<Account> Accounts { get; }
    DbSet<FiscalYear> FiscalYears { get; }
    DbSet<FiscalPeriod> FiscalPeriods { get; }
    DbSet<DocumentSequence> DocumentSequences { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<SalesInvoice> SalesInvoices { get; }
    DbSet<SalesInvoiceItem> SalesInvoiceItems { get; }
    DbSet<PurchaseInvoice> PurchaseInvoices { get; }
    DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentAllocation> PaymentAllocations { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<BankReconciliation> BankReconciliations { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Product> Products { get; }
    DbSet<StockTransaction> StockTransactions { get; }
    DbSet<FixedAsset> FixedAssets { get; }
    DbSet<AssetDepreciation> AssetDepreciations { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Attachment> Attachments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
