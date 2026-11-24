using AccountingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingERP.Infrastructure.Persistence.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(10).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(c => c.PhoneCode).HasMaxLength(10);

        builder.HasIndex(c => c.Code).IsUnique();
    }
}

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currencies");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(5).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Symbol).HasMaxLength(10).IsRequired();

        builder.HasIndex(c => c.Code).IsUnique();
    }
}

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Rate).HasPrecision(18, 6);

        builder.HasIndex(r => new { r.CompanyId, r.FromCurrencyId, r.ToCurrencyId, r.RateDate })
            .IsUnique();

        builder.HasOne(r => r.Company)
            .WithMany()
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.FromCurrency)
            .WithMany()
            .HasForeignKey(r => r.FromCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ToCurrency)
            .WithMany()
            .HasForeignKey(r => r.ToCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TaxTypeConfiguration : IEntityTypeConfiguration<TaxType>
{
    public void Configure(EntityTypeBuilder<TaxType> builder)
    {
        builder.ToTable("TaxTypes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Code).HasMaxLength(20).IsRequired();
        builder.Property(t => t.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(t => t.NameEn).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => new { t.CompanyId, t.Code }).IsUnique();

        builder.HasOne(t => t.Company)
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Key: Tax account must belong to the same company
        builder.HasOne(t => t.Account)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(t => new { t.CompanyId, t.AccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("TaxRates");
        builder.HasKey(tr => tr.Id);
        builder.Property(tr => tr.Rate).HasPrecision(5, 2);

        builder.HasOne(tr => tr.Company)
            .WithMany()
            .HasForeignKey(tr => tr.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tr => tr.TaxType)
            .WithMany(t => t.TaxRates)
            .HasForeignKey(tr => tr.TaxTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
