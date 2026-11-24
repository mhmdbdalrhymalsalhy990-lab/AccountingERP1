using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Accounting;

namespace AccountingERP.Application.Interfaces;

public interface IJournalEntryService
{
    Task<JournalEntryDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<JournalEntryDto> GetByEntryNumberAsync(string entryNumber, CancellationToken cancellationToken = default);
    Task<PagedResult<JournalEntryDto>> GetPagedAsync(JournalEntryFilterRequest filter, CancellationToken cancellationToken = default);
    Task<JournalEntryDto> CreateDraftAsync(CreateJournalEntryDto request, CancellationToken cancellationToken = default);
    Task<JournalEntryDto> UpdateDraftAsync(long id, UpdateJournalEntryDto request, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(long id, CancellationToken cancellationToken = default);
    Task<JournalEntryDto> PostAsync(long id, CancellationToken cancellationToken = default);
    Task<JournalEntryDto> CreateAndPostAsync(CreateJournalEntryDto request, CancellationToken cancellationToken = default);
    Task<JournalEntryDto> ReverseAsync(long id, ReverseJournalEntryRequestDto request, CancellationToken cancellationToken = default);
}
