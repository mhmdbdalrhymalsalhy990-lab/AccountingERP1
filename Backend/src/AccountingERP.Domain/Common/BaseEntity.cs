namespace AccountingERP.Domain.Common;

public abstract class BaseEntity<TKey>
{
    public TKey Id { get; set; } = default!;
}

public abstract class AuditableEntity<TKey> : BaseEntity<TKey>, IAuditableEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedByUserId { get; set; }
}

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    string? CreatedByUserId { get; set; }
    DateTime? LastModifiedAt { get; set; }
    string? LastModifiedByUserId { get; set; }
}

public interface ITenantEntity
{
    int CompanyId { get; set; }
}

public interface IBranchEntity
{
    int BranchId { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedByUserId { get; set; }
}

public interface IConcurrencyAware
{
    byte[]? RowVersion { get; set; }
}
