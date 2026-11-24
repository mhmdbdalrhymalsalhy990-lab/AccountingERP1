using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.AssetAndSystem;

namespace AccountingERP.Application.Interfaces;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogFilterRequest filter, CancellationToken cancellationToken = default);
    Task LogAsync(CreateAuditLogDto request, CancellationToken cancellationToken = default);
}

public interface IAttachmentService
{
    Task<List<AttachmentDto>> GetByEntityAsync(string entityName, string entityId, CancellationToken cancellationToken = default);
    Task<AttachmentDto> UploadAsync(CreateAttachmentDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}
