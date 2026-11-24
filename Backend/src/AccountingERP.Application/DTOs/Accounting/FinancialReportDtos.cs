using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.DTOs.Accounting;

public class GeneralLedgerRequestDto
{
    public int AccountId { get; set; }
    public DateTime FromDate { get; set; } = new(DateTime.UtcNow.Year, 1, 1);
    public DateTime ToDate { get; set; } = DateTime.UtcNow;
    public int? BranchId { get; set; }
    public int? CostCenterId { get; set; }
}

public class GeneralLedgerReportDto
{
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal NetMovement { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<GeneralLedgerLineDto> Transactions { get; set; } = new();
}

public class GeneralLedgerLineDto
{
    public long JournalEntryId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
    public string? ForeignCurrencyCode { get; set; }
    public decimal? ForeignDebit { get; set; }
    public decimal? ForeignCredit { get; set; }
    public string? CostCenterName { get; set; }
}

public class TrialBalanceRequestDto
{
    public DateTime FromDate { get; set; } = new(DateTime.UtcNow.Year, 1, 1);
    public DateTime ToDate { get; set; } = DateTime.UtcNow;
    public int? BranchId { get; set; }
    public int? MaxLevel { get; set; }
    public bool ShowZeroBalances { get; set; } = false;
}

public class TrialBalanceReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<TrialBalanceItemDto> Items { get; set; } = new();
    public decimal TotalOpeningDebit { get; set; }
    public decimal TotalOpeningCredit { get; set; }
    public decimal TotalPeriodDebit { get; set; }
    public decimal TotalPeriodCredit { get; set; }
    public decimal TotalCumulativeDebit { get; set; }
    public decimal TotalCumulativeCredit { get; set; }
    public decimal TotalEndingDebit { get; set; }
    public decimal TotalEndingCredit { get; set; }
    public bool IsBalanced { get; set; }
}

public class TrialBalanceItemDto
{
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsPosting { get; set; }
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal EndingDebit { get; set; }
    public decimal EndingCredit { get; set; }
}

public class IncomeStatementRequestDto
{
    public DateTime FromDate { get; set; } = new(DateTime.UtcNow.Year, 1, 1);
    public DateTime ToDate { get; set; } = DateTime.UtcNow;
    public int? BranchId { get; set; }
}

public class IncomeStatementReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<FinancialStatementItemDto> Revenues { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public List<FinancialStatementItemDto> CostOfGoodsSold { get; set; } = new();
    public decimal TotalCostOfGoodsSold { get; set; }
    public decimal GrossProfit { get; set; }
    public List<FinancialStatementItemDto> OperatingExpenses { get; set; } = new();
    public decimal TotalOperatingExpenses { get; set; }
    public decimal OperatingProfit { get; set; }
    public List<FinancialStatementItemDto> OtherGainsAndLosses { get; set; } = new();
    public decimal TotalOtherGainsAndLosses { get; set; }
    public decimal NetProfit { get; set; }
}

public class FinancialStatementItemDto
{
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public int Level { get; set; }
    public decimal Amount { get; set; }
}

public class BalanceSheetRequestDto
{
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
    public int? BranchId { get; set; }
}

public class BalanceSheetReportDto
{
    public DateTime AsOfDate { get; set; }
    public List<FinancialStatementItemDto> CurrentAssets { get; set; } = new();
    public decimal TotalCurrentAssets { get; set; }
    public List<FinancialStatementItemDto> NonCurrentAssets { get; set; } = new();
    public decimal TotalNonCurrentAssets { get; set; }
    public decimal TotalAssets { get; set; }
    public List<FinancialStatementItemDto> CurrentLiabilities { get; set; } = new();
    public decimal TotalCurrentLiabilities { get; set; }
    public List<FinancialStatementItemDto> NonCurrentLiabilities { get; set; } = new();
    public decimal TotalNonCurrentLiabilities { get; set; }
    public decimal TotalLiabilities { get; set; }
    public List<FinancialStatementItemDto> Equity { get; set; } = new();
    public decimal CurrentYearNetProfit { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal TotalLiabilitiesAndEquity { get; set; }
    public bool IsBalanced { get; set; }
}

public class CashFlowStatementRequestDto
{
    public DateTime FromDate { get; set; } = new(DateTime.UtcNow.Year, 1, 1);
    public DateTime ToDate { get; set; } = DateTime.UtcNow;
    public int? BranchId { get; set; }
}

public class CashFlowStatementReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal BeginningCashBalance { get; set; }
    public List<CashFlowItemDto> OperatingActivities { get; set; } = new();
    public decimal NetOperatingCashFlow { get; set; }
    public List<CashFlowItemDto> InvestingActivities { get; set; } = new();
    public decimal NetInvestingCashFlow { get; set; }
    public List<CashFlowItemDto> FinancingActivities { get; set; } = new();
    public decimal NetFinancingCashFlow { get; set; }
    public decimal NetCashChange { get; set; }
    public decimal EndingCashBalance { get; set; }
}

public class CashFlowItemDto
{
    public string DescriptionAr { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
