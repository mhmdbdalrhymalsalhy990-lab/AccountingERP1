using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class JournalEntriesController : ControllerBase
{
    private readonly IJournalEntryService _journalEntryService;
    private readonly ILogger<JournalEntriesController> _logger;

    public JournalEntriesController(
        IJournalEntryService journalEntryService,
        ILogger<JournalEntriesController> logger)
    {
        _journalEntryService = journalEntryService;
        _logger = logger;
    }

    /// <summary>
    /// Search and filter journal entries with pagination
    /// </summary>
    [HttpGet]
    [HasPermission("JournalEntries.View")]
    public async Task<ActionResult<PagedResult<JournalEntryDto>>> GetPaged([FromQuery] JournalEntryFilterRequest filter, CancellationToken cancellationToken)
    {
        var result = await _journalEntryService.GetPagedAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get single journal entry by its primary key ID
    /// </summary>
    [HttpGet("{id:long}")]
    [HasPermission("JournalEntries.View")]
    public async Task<ActionResult<JournalEntryDto>> GetById(long id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _journalEntryService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get single journal entry by its unique entry number
    /// </summary>
    [HttpGet("by-number/{entryNumber}")]
    [HasPermission("JournalEntries.View")]
    public async Task<ActionResult<JournalEntryDto>> GetByEntryNumber(string entryNumber, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _journalEntryService.GetByEntryNumberAsync(entryNumber, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create a new draft journal entry (unposted, editable)
    /// </summary>
    [HttpPost]
    [HttpPost("draft")]
    [HasPermission("JournalEntries.Create")]
    public async Task<ActionResult<JournalEntryDto>> CreateDraft([FromBody] CreateJournalEntryDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _journalEntryService.CreateDraftAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing draft journal entry
    /// </summary>
    [HttpPut("draft/{id:long}")]
    [HasPermission("JournalEntries.Create")]
    public async Task<ActionResult<JournalEntryDto>> UpdateDraft(long id, [FromBody] UpdateJournalEntryDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _journalEntryService.UpdateDraftAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a draft journal entry (drafts only; posted entries cannot be deleted)
    /// </summary>
    [HttpDelete("draft/{id:long}")]
    [HasPermission("JournalEntries.Create")]
    public async Task<IActionResult> DeleteDraft(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _journalEntryService.DeleteDraftAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Post a draft journal entry and generate sequential gapless entry number
    /// </summary>
    [HttpPost("{id:long}/post")]
    [HasPermission("JournalEntries.Post")]
    public async Task<ActionResult<JournalEntryDto>> Post(long id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _journalEntryService.PostAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create and post a journal entry in a single atomic transaction
    /// </summary>
    [HttpPost("create-and-post")]
    [HasPermission("JournalEntries.Post")]
    public async Task<ActionResult<JournalEntryDto>> CreateAndPost([FromBody] CreateJournalEntryDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _journalEntryService.CreateAndPostAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reverse an existing posted journal entry with an inverted balanced reversal entry
    /// </summary>
    [HttpPost("{id:long}/reverse")]
    [HasPermission("JournalEntries.Reverse")]
    public async Task<ActionResult<JournalEntryDto>> Reverse(long id, [FromBody] ReverseJournalEntryRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _journalEntryService.ReverseAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
