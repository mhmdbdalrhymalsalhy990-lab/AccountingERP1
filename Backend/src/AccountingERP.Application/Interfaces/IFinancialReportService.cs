using AccountingERP.Application.DTOs.Accounting;

namespace AccountingERP.Application.Interfaces;

public interface IFinancialReportService
{
    Task<GeneralLedgerReportDto> GetGeneralLedgerAsync(GeneralLedgerRequestDto request, CancellationToken cancellationToken = default);
    Task<TrialBalanceReportDto> GetTrialBalanceAsync(TrialBalanceRequestDto request, CancellationToken cancellationToken = default);
    Task<IncomeStatementReportDto> GetIncomeStatementAsync(IncomeStatementRequestDto request, CancellationToken cancellationToken = default);
    Task<BalanceSheetReportDto> GetBalanceSheetAsync(BalanceSheetRequestDto request, CancellationToken cancellationToken = default);
    Task<CashFlowStatementReportDto> GetCashFlowStatementAsync(CashFlowStatementRequestDto request, CancellationToken cancellationToken = default);
}
