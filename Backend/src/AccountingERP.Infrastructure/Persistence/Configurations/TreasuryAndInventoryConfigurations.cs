using AccountingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingERP.Infrastructure.Persistence.Configurations;

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccounts");
        builder.HasKey(ba => ba.Id);

        builder.Property(ba => ba.Code).HasMaxLength(30).IsRequired();
        builder.Property(ba => ba.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(ba => ba.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(ba => ba.AccountNumber).HasMaxLength(50);

        builder.HasIndex(ba => new { ba.CompanyId, ba.Code }).IsUnique();

        builder.HasOne(ba => ba.Company)
            .WithMany()
            .HasForeignKey(ba => ba.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ba => ba.Branch)
            .WithMany()
            .HasPrincipalKey(b => new { b.CompanyId, b.Id })
            .HasForeignKey(ba => new { ba.CompanyId, ba.BranchId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ba => ba.Currency)
            .WithMany()
            .HasForeignKey(ba => ba.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Key: Bank account GL account must belong to the same company
        builder.HasOne(ba => ba.Account)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(ba => new { ba.CompanyId, ba.AccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> builder)
    {
        builder.ToTable("BankReconciliations");
        builder.HasKey(br => br.Id);

        builder.Property(br => br.StatementEndingBalance).HasPrecision(18, 4);
        builder.Property(br => br.BookBalance).HasPrecision(18, 4);
        builder.Property(br => br.Difference).HasPrecision(18, 4);

        builder.HasOne(br => br.Company)
            .WithMany()
            .HasForeignKey(br => br.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(br => br.BankAccount)
            .WithMany(ba => ba.Reconciliations)
            .HasForeignKey(br => br.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Code).HasMaxLength(30).IsRequired();
        builder.Property(w => w.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(w => w.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(w => w.Location).HasMaxLength(250);

        builder.HasIndex(w => new { w.CompanyId, w.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(w => w.Company)
            .WithMany()
            .HasForeignKey(w => w.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Branch)
            .WithMany()
            .HasPrincipalKey(b => new { b.CompanyId, b.Id })
            .HasForeignKey(w => new { w.CompanyId, w.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();
        builder.Property(p => p.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(p => p.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Barcode).HasMaxLength(100);
        builder.Property(p => p.UnitName).HasMaxLength(30).IsRequired();
        builder.Property(p => p.CostPrice).HasPrecision(18, 4);
        builder.Property(p => p.SellingPrice).HasPrecision(18, 4);

        builder.HasIndex(p => new { p.CompanyId, p.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(p => p.Company)
            .WithMany()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Keys for Product Accounts
        builder.HasOne(p => p.InventoryAccount)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(p => new { p.CompanyId, p.InventoryAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.CogsAccount)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(p => new { p.CompanyId, p.CogsAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.SalesAccount)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(p => new { p.CompanyId, p.SalesAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.ToTable("StockTransactions");
        builder.HasKey(st => st.Id);

        builder.Property(st => st.Quantity).HasPrecision(18, 4);
        builder.Property(st => st.UnitCost).HasPrecision(18, 4);
        builder.Property(st => st.TotalCost).HasPrecision(18, 4);
        builder.Property(st => st.ReferenceDocumentId).HasMaxLength(100);

        builder.HasOne(st => st.Company)
            .WithMany()
            .HasForeignKey(st => st.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(st => st.Warehouse)
            .WithMany(w => w.StockTransactions)
            .HasForeignKey(st => st.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(st => st.Product)
            .WithMany(p => p.StockTransactions)
            .HasForeignKey(st => st.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(st => st.JournalEntry)
            .WithMany()
            .HasPrincipalKey(je => new { je.CompanyId, je.Id })
            .HasForeignKey(st => new { st.CompanyId, st.JournalEntryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
