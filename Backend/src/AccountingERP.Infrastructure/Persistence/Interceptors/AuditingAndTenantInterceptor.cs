using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Common;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AccountingERP.Infrastructure.Persistence.Interceptors;

public class AuditingAndTenantInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;

    public AuditingAndTenantInterceptor(
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService)
    {
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result, 
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        var currentCompanyId = _currentTenantService.CompanyId;
        var currentUserId = _currentUserService.UserId;
        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // 1. Prevent Modifying/Deleting Posted Journal Entries
            if (entry.Entity is JournalEntry journalEntry)
            {
                var statusProp = entry.Property(nameof(JournalEntry.Status));
                var isPostingTransition = statusProp.IsModified && 
                                          (JournalEntryStatus)statusProp.OriginalValue! == JournalEntryStatus.Draft && 
                                          journalEntry.Status == JournalEntryStatus.Posted;

                if (journalEntry.Status == JournalEntryStatus.Posted && !isPostingTransition)
                {
                    if (entry.State == EntityState.Deleted)
                    {
                        throw new CannotModifyPostedEntryException(journalEntry.EntryNumber);
                    }
                    if (entry.State == EntityState.Modified)
                    {
                        // Allow only marking as reversed during reversal engine execution
                        var isReversedModified = entry.Property(nameof(JournalEntry.IsReversed)).IsModified;
                        var modifiedPropsCount = entry.Properties.Count(p => p.IsModified);
                        
                        // Modified props may include IsReversed, ReversedEntryId, and LastModifiedAt/LastModifiedByUserId
                        if (!isReversedModified || modifiedPropsCount > 4)
                        {
                            throw new CannotModifyPostedEntryException(journalEntry.EntryNumber);
                        }
                    }
                }
            }

            // 2. Prevent Modifying/Deleting Lines of Posted Journal Entries
            if (entry.Entity is JournalEntryLine line && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                // Verify the entry state if attached or loaded
                if (line.JournalEntry != null && line.JournalEntry.Status == JournalEntryStatus.Posted)
                {
                    throw new CannotModifyPostedEntryException(line.JournalEntry.EntryNumber);
                }
            }

            // 3. Multi-Tenancy Enforcement (Fail-Closed)
            if (entry.Entity is ITenantEntity tenantEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    if (currentCompanyId.HasValue)
                    {
                        if (tenantEntity.CompanyId == 0)
                        {
                            tenantEntity.CompanyId = currentCompanyId.Value;
                        }
                        else if (tenantEntity.CompanyId != currentCompanyId.Value)
                        {
                            throw new UnauthorizedAccessException($"Security breach: Cannot insert data for company {tenantEntity.CompanyId} while authorized for company {currentCompanyId.Value}.");
                        }
                    }
                    else if (tenantEntity.CompanyId <= 0)
                    {
                        throw new UnauthorizedAccessException("Multi-Tenancy Security (Fail Closed): Cannot create tenant entity without an authorized tenant context or valid CompanyId.");
                    }
                }
                else if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                {
                    if (!currentCompanyId.HasValue)
                    {
                        throw new UnauthorizedAccessException("Multi-Tenancy Security (Fail Closed): Cannot modify or delete tenant entities without an active authorized tenant context.");
                    }

                    if (tenantEntity.CompanyId != currentCompanyId.Value)
                    {
                        throw new UnauthorizedAccessException($"Security breach: Cannot modify/delete data belonging to company {tenantEntity.CompanyId} while authorized for company {currentCompanyId.Value}.");
                    }
                }
            }

            // 4. Auditing
            if (entry.Entity is IAuditableEntity auditableEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    auditableEntity.CreatedAt = now;
                    auditableEntity.CreatedByUserId = currentUserId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditableEntity.LastModifiedAt = now;
                    auditableEntity.LastModifiedByUserId = currentUserId;
                }
            }

            // 5. Soft Delete
            if (entry.Entity is ISoftDelete softDeleteEntity && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                softDeleteEntity.IsDeleted = true;
                softDeleteEntity.DeletedAt = now;
                softDeleteEntity.DeletedByUserId = currentUserId;
            }
        }
    }
}
