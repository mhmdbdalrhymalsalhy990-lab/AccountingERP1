using AccountingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingERP.Infrastructure.Persistence.Configurations;

public class IdentityConfigurations : 
    IEntityTypeConfiguration<ApplicationUser>,
    IEntityTypeConfiguration<ApplicationRole>,
    IEntityTypeConfiguration<Permission>,
    IEntityTypeConfiguration<RolePermission>,
    IEntityTypeConfiguration<CompanyUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.UserName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.RefreshToken).HasMaxLength(256);

        builder.HasIndex(u => u.UserName).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
    }

    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(250);

        builder.HasIndex(r => r.Name).IsUnique();
    }

    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).HasMaxLength(100).IsRequired();
        builder.Property(p => p.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(p => p.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Module).HasMaxLength(50).IsRequired();

        builder.HasIndex(p => p.Code).IsUnique();
    }

    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(rp => rp.Id);

        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<CompanyUser> builder)
    {
        builder.ToTable("CompanyUsers");
        builder.HasKey(cu => cu.Id);

        builder.HasIndex(cu => new { cu.UserId, cu.CompanyId }).IsUnique();

        builder.HasOne(cu => cu.User)
            .WithMany(u => u.CompanyUsers)
            .HasForeignKey(cu => cu.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cu => cu.Company)
            .WithMany(c => c.CompanyUsers)
            .HasForeignKey(cu => cu.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cu => cu.Role)
            .WithMany()
            .HasForeignKey(cu => cu.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
