using AccountingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingERP.Infrastructure.Persistence.Configurations;

public class FixedAssetConfiguration : IEntityTypeConfiguration<FixedAsset>
{
    public void Configure(EntityTypeBuilder<FixedAsset> builder)
    {
        builder.ToTable("FixedAssets");
        builder.HasKey(fa => fa.Id);

        builder.Property(fa => fa.AssetCode).HasMaxLength(50).IsRequired();
        builder.Property(fa => fa.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(fa => fa.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(fa => fa.PurchaseCost).HasPrecision(18, 4);
        builder.Property(fa => fa.SalvageValue).HasPrecision(18, 4);

        builder.HasIndex(fa => new { fa.CompanyId, fa.AssetCode }).IsUnique();

        builder.HasOne(fa => fa.Company)
            .WithMany()
            .HasForeignKey(fa => fa.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(fa => fa.Branch)
            .WithMany()
            .HasPrincipalKey(b => new { b.CompanyId, b.Id })
            .HasForeignKey(fa => new { fa.CompanyId, fa.BranchId })
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Keys for Asset GL Accounts
        builder.HasOne(fa => fa.AssetAccount)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(fa => new { fa.CompanyId, fa.AssetAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(fa => fa.AccumulatedDepreciationAccount)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(fa => new { fa.CompanyId, fa.AccumulatedDepreciationAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(fa => fa.DepreciationExpenseAccount)
            .WithMany()
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(fa => new { fa.CompanyId, fa.DepreciationExpenseAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AssetDepreciationConfiguration : IEntityTypeConfiguration<AssetDepreciation>
{
    public void Configure(EntityTypeBuilder<AssetDepreciation> builder)
    {
        builder.ToTable("AssetDepreciations");
        builder.HasKey(ad => ad.Id);

        builder.Property(ad => ad.DepreciationAmount).HasPrecision(18, 4);
        builder.Property(ad => ad.BookValueAfterDepreciation).HasPrecision(18, 4);

        builder.HasOne(ad => ad.Company)
            .WithMany()
            .HasForeignKey(ad => ad.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ad => ad.FixedAsset)
            .WithMany(fa => fa.Depreciations)
            .HasForeignKey(ad => ad.FixedAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ad => ad.FiscalPeriod)
            .WithMany()
            .HasPrincipalKey(fp => new { fp.CompanyId, fp.Id })
            .HasForeignKey(ad => new { ad.CompanyId, ad.FiscalPeriodId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ad => ad.JournalEntry)
            .WithMany()
            .HasPrincipalKey(je => new { je.CompanyId, je.Id })
            .HasForeignKey(ad => new { ad.CompanyId, ad.JournalEntryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(al => al.Id);

        builder.Property(al => al.Action).HasMaxLength(50).IsRequired();
        builder.Property(al => al.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(al => al.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(al => al.IpAddress).HasMaxLength(50);

        builder.HasIndex(al => new { al.CompanyId, al.Timestamp });
        builder.HasIndex(al => new { al.EntityName, al.EntityId });

        builder.HasOne(al => al.Company)
            .WithMany()
            .HasForeignKey(al => al.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(at => at.Id);

        builder.Property(at => at.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(at => at.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(at => at.FileName).HasMaxLength(255).IsRequired();
        builder.Property(at => at.FileExtension).HasMaxLength(20).IsRequired();
        builder.Property(at => at.FilePath).HasMaxLength(500).IsRequired();

        builder.HasIndex(at => new { at.CompanyId, at.EntityName, at.EntityId });

        builder.HasOne(at => at.Company)
            .WithMany()
            .HasForeignKey(at => at.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
