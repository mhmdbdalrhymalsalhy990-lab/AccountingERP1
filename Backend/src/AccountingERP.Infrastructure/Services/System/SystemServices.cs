using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.AssetAndSystem;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService,
        ILogger<AuditLogService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.AuditLogs
            .Where(a => a.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
            query = query.Where(a => a.EntityName == filter.EntityName.Trim());

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
            query = query.Where(a => a.EntityId == filter.EntityId.Trim());

        if (!string.IsNullOrWhiteSpace(filter.UserId))
            query = query.Where(a => a.UserId == filter.UserId.Trim());

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.Timestamp >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.Timestamp <= filter.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(a => new AuditLogDto
        {
            Id = a.Id,
            CompanyId = a.CompanyId,
            UserId = a.UserId,
            Action = a.Action,
            EntityName = a.EntityName,
            EntityId = a.EntityId,
            OldValues = a.OldValues,
            NewValues = a.NewValues,
            Timestamp = a.Timestamp,
            IpAddress = a.IpAddress
        }).ToList();

        return new PagedResult<AuditLogDto>(dtos, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task LogAsync(CreateAuditLogDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var log = new AuditLog
        {
            CompanyId = companyId,
            UserId = _currentUserService.UserId,
            Action = request.Action,
            EntityName = request.EntityName,
            EntityId = request.EntityId,
            OldValues = request.OldValues,
            NewValues = request.NewValues,
            Timestamp = DateTime.UtcNow,
            IpAddress = request.IpAddress
        };

        await _context.AuditLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class AttachmentService : IAttachmentService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService,
        ILogger<AttachmentService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<List<AttachmentDto>> GetByEntityAsync(string entityName, string entityId, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var list = await _context.Attachments
            .Where(a => a.CompanyId == companyId && a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync(cancellationToken);

        return list.Select(a => new AttachmentDto
        {
            Id = a.Id,
            CompanyId = a.CompanyId,
            EntityName = a.EntityName,
            EntityId = a.EntityId,
            FileName = a.FileName,
            FileExtension = a.FileExtension,
            FileSizeBytes = a.FileSizeBytes,
            FilePath = a.FilePath,
            UploadedByUserId = a.UploadedByUserId,
            UploadedAt = a.UploadedAt
        }).ToList();
    }

    public async Task<AttachmentDto> UploadAsync(CreateAttachmentDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var attachment = new Attachment
        {
            CompanyId = companyId,
            EntityName = request.EntityName.Trim(),
            EntityId = request.EntityId.Trim(),
            FileName = request.FileName.Trim(),
            FileExtension = request.FileExtension.Trim().ToLower(),
            FileSizeBytes = request.FileSizeBytes,
            FilePath = request.FilePath.Trim(),
            UploadedByUserId = _currentUserService.UserId,
            UploadedAt = DateTime.UtcNow
        };

        await _context.Attachments.AddAsync(attachment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment '{FileName}' uploaded for {EntityName}:{EntityId}.", attachment.FileName, attachment.EntityName, attachment.EntityId);

        return new AttachmentDto
        {
            Id = attachment.Id,
            CompanyId = attachment.CompanyId,
            EntityName = attachment.EntityName,
            EntityId = attachment.EntityId,
            FileName = attachment.FileName,
            FileExtension = attachment.FileExtension,
            FileSizeBytes = attachment.FileSizeBytes,
            FilePath = attachment.FilePath,
            UploadedByUserId = attachment.UploadedByUserId,
            UploadedAt = attachment.UploadedAt
        };
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Attachment), id);

        _context.Attachments.Remove(attachment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment {Id} deleted.", id);
    }
}
