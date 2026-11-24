using AccountingERP.Domain.Common;

namespace AccountingERP.Domain.Entities;

public class ApplicationUser : BaseEntity<string>
{
    public ApplicationUser()
    {
        Id = Guid.NewGuid().ToString();
    }

    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();
}

public class ApplicationRole : BaseEntity<string>
{
    public ApplicationRole()
    {
        Id = Guid.NewGuid().ToString();
    }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Permission : BaseEntity<int>
{
    public string Code { get; set; } = string.Empty; // e.g. "JournalEntries.Post"
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty; // GeneralLedger, Sales, Purchases...
}

public class RolePermission : BaseEntity<int>
{
    public string RoleId { get; set; } = string.Empty;
    public int PermissionId { get; set; }

    public ApplicationRole? Role { get; set; }
    public Permission? Permission { get; set; }
}

public class CompanyUser : AuditableEntity<long>, ITenantEntity
{
    public string UserId { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public int? DefaultBranchId { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ApplicationUser? User { get; set; }
    public Company? Company { get; set; }
    public Branch? DefaultBranch { get; set; }
    public ApplicationRole? Role { get; set; }
}
