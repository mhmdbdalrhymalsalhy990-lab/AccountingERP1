using System.Linq.Expressions;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Common;
using AccountingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingERP.Infrastructure.Persistence.Context;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentTenantService _currentTenantService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentTenantService currentTenantService)
        : base(options)
    {
        _currentTenantService = currentTenantService;
    }

    public int? CurrentCompanyId => _currentTenantService.CompanyId;

    // 1. Organization & Identity
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<ApplicationRole> Roles => Set<ApplicationRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<CompanyUser> CompanyUsers => Set<CompanyUser>();

    // 2. Master Data, Currencies & Taxes
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<TaxType> TaxTypes => Set<TaxType>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();

    // 3. General Ledger & Accounting
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();

    // 4. Commercial & Sub-Ledger
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    // 5. Treasury & Banking
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankReconciliation> BankReconciliations => Set<BankReconciliation>();

    // 6. Inventory & Warehousing
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

    // 7. Fixed Assets & Depreciation
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<AssetDepreciation> AssetDepreciations => Set<AssetDepreciation>();

    // 8. Audit & Attachments
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Apply Global Query Filters dynamically
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            var isTenant = typeof(ITenantEntity).IsAssignableFrom(clrType);
            var isSoftDelete = typeof(ISoftDelete).IsAssignableFrom(clrType);

            if (isTenant && isSoftDelete)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ConfigureTenantAndSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
            else if (isTenant)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ConfigureTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
            else if (isSoftDelete)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ConfigureSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ConfigureTenantAndSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity, ISoftDelete
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => 
            !e.IsDeleted && e.CompanyId == CurrentCompanyId);
    }

    private void ConfigureTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => 
            e.CompanyId == CurrentCompanyId);
    }

    private void ConfigureSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDelete
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}
