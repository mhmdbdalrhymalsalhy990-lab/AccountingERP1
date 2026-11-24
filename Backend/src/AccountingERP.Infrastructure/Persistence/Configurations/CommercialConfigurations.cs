using AccountingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingERP.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(30).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(c => c.CreditLimit).HasPrecision(18, 4);

        builder.HasIndex(c => new { c.CompanyId, c.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(c => c.Company)
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Key: Customer's GL account belongs to the same company
        builder.HasOne(c => c.Account)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(c => new { c.CompanyId, c.AccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).HasMaxLength(30).IsRequired();
        builder.Property(s => s.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(s => s.NameEn).HasMaxLength(150).IsRequired();

        builder.HasIndex(s => new { s.CompanyId, s.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(s => s.Company)
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Key: Supplier's GL account belongs to the same company
        builder.HasOne(s => s.Account)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(s => new { s.CompanyId, s.AccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("SalesInvoices");
        builder.HasKey(si => si.Id);

        builder.Property(si => si.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(si => si.ExchangeRate).HasPrecision(18, 6);
        builder.Property(si => si.SubTotal).HasPrecision(18, 4);
        builder.Property(si => si.TaxTotal).HasPrecision(18, 4);
        builder.Property(si => si.DiscountTotal).HasPrecision(18, 4);
        builder.Property(si => si.GrandTotal).HasPrecision(18, 4);
        builder.Property(si => si.PaidAmount).HasPrecision(18, 4);
        builder.Property(si => si.RemainingAmount).HasPrecision(18, 4);
        builder.Property(si => si.RowVersion).IsRowVersion();

        builder.HasIndex(si => new { si.CompanyId, si.InvoiceNumber }).IsUnique();

        builder.HasOne(si => si.Company)
            .WithMany()
            .HasForeignKey(si => si.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(si => si.Branch)
            .WithMany()
            .HasPrincipalKey(b => new { b.CompanyId, b.Id })
            .HasForeignKey(si => new { si.CompanyId, si.BranchId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(si => si.Customer)
            .WithMany(c => c.SalesInvoices)
            .HasForeignKey(si => si.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(si => si.JournalEntry)
            .WithMany()
            .HasPrincipalKey(je => new { je.CompanyId, je.Id })
            .HasForeignKey(si => new { si.CompanyId, si.JournalEntryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalesInvoiceItemConfiguration : IEntityTypeConfiguration<SalesInvoiceItem>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceItem> builder)
    {
        builder.ToTable("SalesInvoiceItems");
        builder.HasKey(sii => sii.Id);

        builder.Property(sii => sii.Quantity).HasPrecision(18, 4);
        builder.Property(sii => sii.UnitPrice).HasPrecision(18, 4);
        builder.Property(sii => sii.DiscountRate).HasPrecision(5, 2);
        builder.Property(sii => sii.TaxRate).HasPrecision(5, 2);
        builder.Property(sii => sii.TaxAmount).HasPrecision(18, 4);
        builder.Property(sii => sii.LineTotal).HasPrecision(18, 4);

        builder.HasOne(sii => sii.SalesInvoice)
            .WithMany(si => si.Items)
            .HasForeignKey(sii => sii.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sii => sii.Product)
            .WithMany()
            .HasForeignKey(sii => sii.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sii => sii.Warehouse)
            .WithMany()
            .HasForeignKey(sii => sii.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("PurchaseInvoices");
        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(pi => pi.VendorBillNumber).HasMaxLength(100);
        builder.Property(pi => pi.ExchangeRate).HasPrecision(18, 6);
        builder.Property(pi => pi.SubTotal).HasPrecision(18, 4);
        builder.Property(pi => pi.TaxTotal).HasPrecision(18, 4);
        builder.Property(pi => pi.GrandTotal).HasPrecision(18, 4);
        builder.Property(pi => pi.PaidAmount).HasPrecision(18, 4);
        builder.Property(pi => pi.RemainingAmount).HasPrecision(18, 4);
        builder.Property(pi => pi.RowVersion).IsRowVersion();

        builder.HasIndex(pi => new { pi.CompanyId, pi.InvoiceNumber }).IsUnique();

        builder.HasOne(pi => pi.Company)
            .WithMany()
            .HasForeignKey(pi => pi.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pi => pi.Branch)
            .WithMany()
            .HasPrincipalKey(b => new { b.CompanyId, b.Id })
            .HasForeignKey(pi => new { pi.CompanyId, pi.BranchId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pi => pi.Supplier)
            .WithMany(s => s.PurchaseInvoices)
            .HasForeignKey(pi => pi.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pi => pi.JournalEntry)
            .WithMany()
            .HasPrincipalKey(je => new { je.CompanyId, je.Id })
            .HasForeignKey(pi => new { pi.CompanyId, pi.JournalEntryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseInvoiceItemConfiguration : IEntityTypeConfiguration<PurchaseInvoiceItem>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceItem> builder)
    {
        builder.ToTable("PurchaseInvoiceItems");
        builder.HasKey(pii => pii.Id);

        builder.Property(pii => pii.Quantity).HasPrecision(18, 4);
        builder.Property(pii => pii.UnitPrice).HasPrecision(18, 4);
        builder.Property(pii => pii.TaxRate).HasPrecision(5, 2);
        builder.Property(pii => pii.TaxAmount).HasPrecision(18, 4);
        builder.Property(pii => pii.LineTotal).HasPrecision(18, 4);

        builder.HasOne(pii => pii.PurchaseInvoice)
            .WithMany(pi => pi.Items)
            .HasForeignKey(pii => pii.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pii => pii.Product)
            .WithMany()
            .HasForeignKey(pii => pii.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pii => pii.Warehouse)
            .WithMany()
            .HasForeignKey(pii => pii.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PaymentNumber).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 4);
        builder.Property(p => p.ExchangeRate).HasPrecision(18, 6);
        builder.Property(p => p.BaseAmount).HasPrecision(18, 4);
        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);
        builder.Property(p => p.ChequeNumber).HasMaxLength(50);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => new { p.CompanyId, p.PaymentNumber }).IsUnique();

        builder.HasOne(p => p.Company)
            .WithMany()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Branch)
            .WithMany()
            .HasPrincipalKey(b => new { b.CompanyId, b.Id })
            .HasForeignKey(p => new { p.CompanyId, p.BranchId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.BankAccount)
            .WithMany()
            .HasForeignKey(p => p.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.JournalEntry)
            .WithMany()
            .HasPrincipalKey(je => new { je.CompanyId, je.Id })
            .HasForeignKey(p => new { p.CompanyId, p.JournalEntryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations");
        builder.HasKey(pa => pa.Id);
        builder.Property(pa => pa.AllocatedAmount).HasPrecision(18, 4);

        builder.HasOne(pa => pa.Payment)
            .WithMany(p => p.Allocations)
            .HasForeignKey(pa => pa.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pa => pa.SalesInvoice)
            .WithMany()
            .HasForeignKey(pa => pa.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pa => pa.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(pa => pa.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
