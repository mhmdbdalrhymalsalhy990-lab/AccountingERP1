using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.Interfaces;

public interface IAccountService
{
    Task<AccountDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AccountDto> GetByCodeAsync(string accountCode, CancellationToken cancellationToken = default);
    Task<List<AccountTreeDto>> GetAccountTreeAsync(AccountType? type = null, CancellationToken cancellationToken = default);
    Task<PagedResult<AccountDto>> GetAccountsPagedAsync(AccountFilterRequest filter, CancellationToken cancellationToken = default);
    Task<List<AccountLookupDto>> GetPostingAccountsLookupAsync(AccountType? type = null, CancellationToken cancellationToken = default);
    Task<string> GenerateNextAccountCodeAsync(int parentAccountId, CancellationToken cancellationToken = default);
    Task<AccountDto> CreateAccountAsync(CreateAccountDto request, CancellationToken cancellationToken = default);
    Task<AccountDto> UpdateAccountAsync(int id, UpdateAccountDto request, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ToggleAccountStatusAsync(int id, CancellationToken cancellationToken = default);
}
