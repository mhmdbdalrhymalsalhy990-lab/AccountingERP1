using AccountingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingERP.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);

        // Required alternate key for composite foreign keys
        builder.HasAlternateKey(a => new { a.CompanyId, a.Id });

        builder.Property(a => a.AccountCode).HasMaxLength(30).IsRequired();
        builder.Property(a => a.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(a => a.NameEn).HasMaxLength(150).IsRequired();

        builder.HasIndex(a => new { a.CompanyId, a.AccountCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(a => a.Company)
            .WithMany(c => c.Accounts)
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-Referencing Composite Foreign Key: Parent and Child MUST belong to the same company
        builder.HasOne(a => a.ParentAccount)
            .WithMany(a => a.SubAccounts)
            .HasPrincipalKey(p => new { p.CompanyId, p.Id })
            .HasForeignKey(a => new { a.CompanyId, a.ParentAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Currency)
            .WithMany()
            .HasForeignKey(a => a.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.ToTable("FiscalYears");
        builder.HasKey(fy => fy.Id);
        builder.Property(fy => fy.Name).HasMaxLength(50).IsRequired();
        builder.Property(fy => fy.RowVersion).IsRowVersion();

        builder.HasIndex(fy => new { fy.CompanyId, fy.Name }).IsUnique();

        builder.HasOne(fy => fy.Company)
            .WithMany(c => c.FiscalYears)
            .HasForeignKey(fy => fy.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Closing Journal Entry reference
        builder.HasOne(fy => fy.ClosingJournalEntry)
            .WithMany()
            .HasPrincipalKey(je => new { je.CompanyId, je.Id })
            .HasForeignKey(fy => new { fy.CompanyId, fy.ClosingJournalEntryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
{
    public void Configure(EntityTypeBuilder<FiscalPeriod> builder)
    {
        builder.ToTable("FiscalPeriods");
        builder.HasKey(fp => fp.Id);

        // Required alternate key for composite foreign keys from journal entries
        builder.HasAlternateKey(fp => new { fp.CompanyId, fp.Id });

        builder.Property(fp => fp.NameAr).HasMaxLength(50).IsRequired();
        builder.Property(fp => fp.NameEn).HasMaxLength(50).IsRequired();

        builder.HasIndex(fp => new { fp.CompanyId, fp.FiscalYearId, fp.PeriodNumber }).IsUnique();

        builder.HasOne(fp => fp.Company)
            .WithMany()
            .HasForeignKey(fp => fp.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(fp => fp.FiscalYear)
            .WithMany(fy => fy.Periods)
            .HasForeignKey(fp => fp.FiscalYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("DocumentSequences");
        builder.HasKey(ds => ds.Id);
        builder.Property(ds => ds.Prefix).HasMaxLength(10).IsRequired();

        builder.HasIndex(ds => new { ds.CompanyId, ds.FiscalYearId, ds.DocumentType }).IsUnique();

        builder.HasOne(ds => ds.Company)
            .WithMany()
            .HasForeignKey(ds => ds.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ds => ds.FiscalYear)
            .WithMany()
            .HasForeignKey(ds => ds.FiscalYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries");
        builder.HasKey(je => je.Id);

        // Required alternate key for composite foreign keys with lines
        builder.HasAlternateKey(je => new { je.CompanyId, je.Id });

        builder.Property(je => je.EntryNumber).HasMaxLength(50).IsRequired();
        builder.Property(je => je.ReferenceNumber).HasMaxLength(100);
        builder.Property(je => je.Description).HasMaxLength(500).IsRequired();
        builder.Property(je => je.SourceDocumentId).HasMaxLength(100);
        builder.Property(je => je.TotalDebit).HasPrecision(18, 4);
        builder.Property(je => je.TotalCredit).HasPrecision(18, 4);
        builder.Property(je => je.RowVersion).IsRowVersion();

        builder.HasIndex(je => new { je.CompanyId, je.EntryNumber }).IsUnique();

        // Idempotency: Prevent duplicate posting of same source document
        builder.HasIndex(je => new { je.CompanyId, je.SourceDocumentType, je.SourceDocumentId })
            .IsUnique()
            .HasFilter("[SourceDocumentId] IS NOT NULL");

        // Prevent reversing the same entry multiple times
        builder.HasIndex(je => new { je.CompanyId, je.ReversedEntryId })
            .IsUnique()
            .HasFilter("[ReversedEntryId] IS NOT NULL");

        builder.HasOne(je => je.Company)
            .WithMany()
            .HasForeignKey(je => je.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Key for Branch
        builder.HasOne(je => je.Branch)
            .WithMany()
            .HasPrincipalKey(b => new { b.CompanyId, b.Id })
            .HasForeignKey(je => new { je.CompanyId, je.BranchId })
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Key for FiscalPeriod
        builder.HasOne(je => je.FiscalPeriod)
            .WithMany(fp => fp.JournalEntries)
            .HasPrincipalKey(fp => new { fp.CompanyId, fp.Id })
            .HasForeignKey(je => new { je.CompanyId, je.FiscalPeriodId })
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Key for Reversal Entry
        builder.HasOne(je => je.ReversedEntry)
            .WithMany()
            .HasPrincipalKey(orig => new { orig.CompanyId, orig.Id })
            .HasForeignKey(je => new { je.CompanyId, je.ReversedEntryId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("JournalEntryLines");
        builder.HasKey(jel => jel.Id);

        builder.Property(jel => jel.Description).HasMaxLength(500);
        builder.Property(jel => jel.Debit).HasPrecision(18, 4);
        builder.Property(jel => jel.Credit).HasPrecision(18, 4);
        builder.Property(jel => jel.ForeignDebit).HasPrecision(18, 4);
        builder.Property(jel => jel.ForeignCredit).HasPrecision(18, 4);
        builder.Property(jel => jel.ExchangeRate).HasPrecision(18, 6);

        // Composite Foreign Key to JournalEntry WITH NO ACTION (Prevents accidental cascade deletion of posted lines)
        builder.HasOne(jel => jel.JournalEntry)
            .WithMany(je => je.Lines)
            .HasPrincipalKey(je => new { je.CompanyId, je.Id })
            .HasForeignKey(jel => new { jel.CompanyId, jel.JournalEntryId })
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Foreign Key to Account: Line account must belong to the same company
        builder.HasOne(jel => jel.Account)
            .WithMany(a => a.JournalEntryLines)
            .HasPrincipalKey(a => new { a.CompanyId, a.Id })
            .HasForeignKey(jel => new { jel.CompanyId, jel.AccountId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(jel => jel.ForeignCurrency)
            .WithMany()
            .HasForeignKey(jel => jel.ForeignCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(jel => jel.CostCenter)
            .WithMany()
            .HasForeignKey(jel => jel.CostCenterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ToTable("CostCenters");
        builder.HasKey(cc => cc.Id);

        builder.HasAlternateKey(cc => new { cc.CompanyId, cc.Id });

        builder.Property(cc => cc.Code).HasMaxLength(30).IsRequired();
        builder.Property(cc => cc.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(cc => cc.NameEn).HasMaxLength(150).IsRequired();

        builder.HasIndex(cc => new { cc.CompanyId, cc.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(cc => cc.Company)
            .WithMany()
            .HasForeignKey(cc => cc.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing composite FK: parent and child belong to the same company
        builder.HasOne(cc => cc.ParentCostCenter)
            .WithMany(cc => cc.SubCostCenters)
            .HasPrincipalKey(p => new { p.CompanyId, p.Id })
            .HasForeignKey(cc => new { cc.CompanyId, cc.ParentCostCenterId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
