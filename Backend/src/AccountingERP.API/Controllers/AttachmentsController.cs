using AccountingERP.Application.DTOs.AssetAndSystem;
using AccountingERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(IAttachmentService attachmentService, ILogger<AttachmentsController> logger)
    {
        _attachmentService = attachmentService;
        _logger = logger;
    }

    [HttpGet("{entityName}/{entityId}")]
    public async Task<ActionResult<List<AttachmentDto>>> GetByEntity(string entityName, string entityId, CancellationToken cancellationToken)
    {
        var result = await _attachmentService.GetByEntityAsync(entityName, entityId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<AttachmentDto>> Upload([FromBody] CreateAttachmentDto request, CancellationToken cancellationToken)
    {
        var result = await _attachmentService.UploadAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _attachmentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
