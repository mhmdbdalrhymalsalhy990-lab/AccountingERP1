using AccountingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingERP.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(c => c.TaxNumber).HasMaxLength(50);
        builder.Property(c => c.CommercialRegistration).HasMaxLength(50);
        builder.Property(c => c.LogoUrl).HasMaxLength(500);
        builder.Property(c => c.Address).HasMaxLength(250);
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.Email).HasMaxLength(100);

        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(c => c.Country)
            .WithMany()
            .HasForeignKey(c => c.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.BaseCurrency)
            .WithMany()
            .HasForeignKey(c => c.BaseCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Settings)
            .WithOne(s => s.Company)
            .HasForeignKey<CompanySettings>(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(b => b.Id);

        // Required alternate key for composite foreign keys from journal entries
        builder.HasAlternateKey(b => new { b.CompanyId, b.Id });

        builder.Property(b => b.Code).HasMaxLength(20).IsRequired();
        builder.Property(b => b.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(b => b.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(b => b.Address).HasMaxLength(250);
        builder.Property(b => b.Phone).HasMaxLength(50);

        builder.HasIndex(b => new { b.CompanyId, b.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(b => b.Company)
            .WithMany(c => c.Branches)
            .HasForeignKey(b => b.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CompanySettingsConfiguration : IEntityTypeConfiguration<CompanySettings>
{
    public void Configure(EntityTypeBuilder<CompanySettings> builder)
    {
        builder.ToTable("CompanySettings");
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.CompanyId).IsUnique();

        // Composite Foreign Keys ensuring system accounts belong to the same company
        builder.HasOne(s => s.RetainedEarningsAccount)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(s => new { s.CompanyId, s.RetainedEarningsAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.CurrentYearEarningsAccount)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(s => new { s.CompanyId, s.CurrentYearEarningsAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ForexGainLossAccount)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(s => new { s.CompanyId, s.ForexGainLossAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
