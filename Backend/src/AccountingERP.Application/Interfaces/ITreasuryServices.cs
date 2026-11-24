using AccountingERP.Application.DTOs.TreasuryAndInventory;

namespace AccountingERP.Application.Interfaces;

public interface IBankAccountService
{
    Task<List<BankAccountDto>> GetAllAsync(bool? isCash = null, CancellationToken cancellationToken = default);
    Task<BankAccountDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<BankAccountDto> CreateAsync(CreateBankAccountDto request, CancellationToken cancellationToken = default);
    Task<BankAccountDto> UpdateAsync(int id, UpdateBankAccountDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<decimal> GetBookBalanceAsync(int id, CancellationToken cancellationToken = default);
}

public interface IBankReconciliationService
{
    Task<List<BankReconciliationDto>> GetByBankAccountIdAsync(int bankAccountId, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> PerformReconciliationAsync(CreateBankReconciliationDto request, CancellationToken cancellationToken = default);
}
