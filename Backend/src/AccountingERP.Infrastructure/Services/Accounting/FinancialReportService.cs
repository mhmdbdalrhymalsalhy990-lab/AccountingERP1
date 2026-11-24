using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

public class FinancialReportService : IFinancialReportService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<FinancialReportService> _logger;

    public FinancialReportService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<FinancialReportService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId()
    {
        return _currentTenantService.CompanyId
            ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب (Company context is missing).");
    }

    public async Task<GeneralLedgerReportDto> GetGeneralLedgerAsync(GeneralLedgerRequestDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.CompanyId == companyId && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Account), request.AccountId);

        // 1. Calculate Opening Balance before FromDate
        var priorLinesQuery = _context.JournalEntryLines
            .Where(l => l.AccountId == account.Id &&
                        l.CompanyId == companyId &&
                        l.JournalEntry!.Status == JournalEntryStatus.Posted &&
                        l.JournalEntry.EntryDate < request.FromDate);

        if (request.BranchId.HasValue)
            priorLinesQuery = priorLinesQuery.Where(l => l.BranchId == request.BranchId.Value);
        if (request.CostCenterId.HasValue)
            priorLinesQuery = priorLinesQuery.Where(l => l.CostCenterId == request.CostCenterId.Value);

        var priorDebit = await priorLinesQuery.SumAsync(l => (decimal?)l.Debit, cancellationToken) ?? 0m;
        var priorCredit = await priorLinesQuery.SumAsync(l => (decimal?)l.Credit, cancellationToken) ?? 0m;

        var openingBalance = account.Nature == AccountNature.Debit
            ? priorDebit - priorCredit
            : priorCredit - priorDebit;

        // 2. Fetch Period Transactions
        var periodQuery = _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Include(l => l.ForeignCurrency)
            .Include(l => l.CostCenter)
            .Where(l => l.AccountId == account.Id &&
                        l.CompanyId == companyId &&
                        l.JournalEntry!.Status == JournalEntryStatus.Posted &&
                        l.JournalEntry.EntryDate >= request.FromDate &&
                        l.JournalEntry.EntryDate <= request.ToDate);

        if (request.BranchId.HasValue)
            periodQuery = periodQuery.Where(l => l.BranchId == request.BranchId.Value);
        if (request.CostCenterId.HasValue)
            periodQuery = periodQuery.Where(l => l.CostCenterId == request.CostCenterId.Value);

        var lines = await periodQuery
            .OrderBy(l => l.JournalEntry!.EntryDate)
            .ThenBy(l => l.JournalEntryId)
            .ThenBy(l => l.LineNumber)
            .ToListAsync(cancellationToken);

        // 3. Compute Line Running Balances
        decimal running = openingBalance;
        var transactionDtos = new List<GeneralLedgerLineDto>();

        foreach (var l in lines)
        {
            if (account.Nature == AccountNature.Debit)
                running += (l.Debit - l.Credit);
            else
                running += (l.Credit - l.Debit);

            transactionDtos.Add(new GeneralLedgerLineDto
            {
                JournalEntryId = l.JournalEntryId,
                EntryNumber = l.JournalEntry!.EntryNumber,
                EntryDate = l.JournalEntry.EntryDate,
                ReferenceNumber = l.JournalEntry.ReferenceNumber,
                Description = string.IsNullOrWhiteSpace(l.Description) ? l.JournalEntry.Description : l.Description,
                Debit = l.Debit,
                Credit = l.Credit,
                RunningBalance = running,
                ForeignCurrencyCode = l.ForeignCurrency?.Code,
                ForeignDebit = l.ForeignDebit > 0 ? l.ForeignDebit : null,
                ForeignCredit = l.ForeignCredit > 0 ? l.ForeignCredit : null,
                CostCenterName = l.CostCenter?.NameAr
            });
        }

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);
        var netMovement = account.Nature == AccountNature.Debit ? (totalDebit - totalCredit) : (totalCredit - totalDebit);

        return new GeneralLedgerReportDto
        {
            AccountId = account.Id,
            AccountCode = account.AccountCode,
            AccountNameAr = account.NameAr,
            AccountNameEn = account.NameEn,
            AccountType = account.AccountType,
            Nature = account.Nature,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            OpeningBalance = openingBalance,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            NetMovement = netMovement,
            ClosingBalance = running,
            Transactions = transactionDtos
        };
    }

    public async Task<TrialBalanceReportDto> GetTrialBalanceAsync(TrialBalanceRequestDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var accounts = await _context.Accounts
            .Where(a => a.CompanyId == companyId && !a.IsDeleted)
            .OrderBy(a => a.AccountCode)
            .ToListAsync(cancellationToken);

        // Query all posted lines for the company up to ToDate
        var allLinesQuery = _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.CompanyId == companyId &&
                        l.JournalEntry!.Status == JournalEntryStatus.Posted &&
                        l.JournalEntry.EntryDate <= request.ToDate);

        if (request.BranchId.HasValue)
            allLinesQuery = allLinesQuery.Where(l => l.BranchId == request.BranchId.Value);

        var allLines = await allLinesQuery.ToListAsync(cancellationToken);

        // Separate prior and period lines grouped by AccountId
        var priorByAccount = allLines
            .Where(l => l.JournalEntry!.EntryDate < request.FromDate)
            .GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => (Debit: g.Sum(x => x.Debit), Credit: g.Sum(x => x.Credit)));

        var periodByAccount = allLines
            .Where(l => l.JournalEntry!.EntryDate >= request.FromDate && l.JournalEntry.EntryDate <= request.ToDate)
            .GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => (Debit: g.Sum(x => x.Debit), Credit: g.Sum(x => x.Credit)));

        // Calculate balances for each leaf account
        var accountBalanceMap = new Dictionary<int, (decimal OpDeb, decimal OpCred, decimal PerDeb, decimal PerCred)>();

        foreach (var acc in accounts)
        {
            priorByAccount.TryGetValue(acc.Id, out var prior);
            periodByAccount.TryGetValue(acc.Id, out var period);

            accountBalanceMap[acc.Id] = (prior.Debit, prior.Credit, period.Debit, period.Credit);
        }

        // For summary accounts, roll up children balances recursively
        (decimal OpDeb, decimal OpCred, decimal PerDeb, decimal PerCred) GetAggregatedBalances(Domain.Entities.Account acc)
        {
            var my = accountBalanceMap[acc.Id];
            var children = accounts.Where(a => a.ParentAccountId == acc.Id).ToList();

            if (children.Count == 0)
                return my;

            decimal totalOpDeb = my.OpDeb;
            decimal totalOpCred = my.OpCred;
            decimal totalPerDeb = my.PerDeb;
            decimal totalPerCred = my.PerCred;

            foreach (var child in children)
            {
                var childBal = GetAggregatedBalances(child);
                totalOpDeb += childBal.OpDeb;
                totalOpCred += childBal.OpCred;
                totalPerDeb += childBal.PerDeb;
                totalPerCred += childBal.PerCred;
            }

            return (totalOpDeb, totalOpCred, totalPerDeb, totalPerCred);
        }

        var items = new List<TrialBalanceItemDto>();

        foreach (var acc in accounts)
        {
            if (request.MaxLevel.HasValue && acc.Level > request.MaxLevel.Value)
                continue;

            var (opDeb, opCred, perDeb, perCred) = acc.IsPosting ? accountBalanceMap[acc.Id] : GetAggregatedBalances(acc);

            var totalDeb = opDeb + perDeb;
            var totalCred = opCred + perCred;

            decimal endDeb = 0m;
            decimal endCred = 0m;

            if (totalDeb > totalCred)
                endDeb = totalDeb - totalCred;
            else if (totalCred > totalDeb)
                endCred = totalCred - totalDeb;

            var isZero = opDeb == 0 && opCred == 0 && perDeb == 0 && perCred == 0 && endDeb == 0 && endCred == 0;
            if (isZero && !request.ShowZeroBalances)
                continue;

            items.Add(new TrialBalanceItemDto
            {
                AccountId = acc.Id,
                AccountCode = acc.AccountCode,
                AccountNameAr = acc.NameAr,
                AccountNameEn = acc.NameEn,
                Level = acc.Level,
                IsPosting = acc.IsPosting,
                AccountType = acc.AccountType,
                Nature = acc.Nature,
                OpeningDebit = opDeb,
                OpeningCredit = opCred,
                PeriodDebit = perDeb,
                PeriodCredit = perCred,
                TotalDebit = totalDeb,
                TotalCredit = totalCred,
                EndingDebit = endDeb,
                EndingCredit = endCred
            });
        }

        // Totals calculated from leaf posting accounts
        var leafAccounts = accounts.Where(a => a.IsPosting).ToList();
        decimal totalOpeningDebit = 0m, totalOpeningCredit = 0m;
        decimal totalPeriodDebit = 0m, totalPeriodCredit = 0m;
        decimal totalEndingDebit = 0m, totalEndingCredit = 0m;

        foreach (var leaf in leafAccounts)
        {
            var bal = accountBalanceMap[leaf.Id];
            totalOpeningDebit += bal.OpDeb;
            totalOpeningCredit += bal.OpCred;
            totalPeriodDebit += bal.PerDeb;
            totalPeriodCredit += bal.PerCred;

            var tDeb = bal.OpDeb + bal.PerDeb;
            var tCred = bal.OpCred + bal.PerCred;
            if (tDeb > tCred)
                totalEndingDebit += (tDeb - tCred);
            else if (tCred > tDeb)
                totalEndingCredit += (tCred - tDeb);
        }

        var totalCumDebit = totalOpeningDebit + totalPeriodDebit;
        var totalCumCredit = totalOpeningCredit + totalPeriodCredit;
        var isBalanced = Math.Abs(totalEndingDebit - totalEndingCredit) < 0.0001m &&
                         Math.Abs(totalPeriodDebit - totalPeriodCredit) < 0.0001m;

        return new TrialBalanceReportDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Items = items,
            TotalOpeningDebit = totalOpeningDebit,
            TotalOpeningCredit = totalOpeningCredit,
            TotalPeriodDebit = totalPeriodDebit,
            TotalPeriodCredit = totalPeriodCredit,
            TotalCumulativeDebit = totalCumDebit,
            TotalCumulativeCredit = totalCumCredit,
            TotalEndingDebit = totalEndingDebit,
            TotalEndingCredit = totalEndingCredit,
            IsBalanced = isBalanced
        };
    }

    public async Task<IncomeStatementReportDto> GetIncomeStatementAsync(IncomeStatementRequestDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        // 1. Fetch all accounts of type Revenue & Expense
        var accounts = await _context.Accounts
            .Where(a => a.CompanyId == companyId &&
                        !a.IsDeleted &&
                        (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense))
            .OrderBy(a => a.AccountCode)
            .ToListAsync(cancellationToken);

        // 2. Fetch posted lines between FromDate and ToDate
        var linesQuery = _context.JournalEntryLines
            .Where(l => l.CompanyId == companyId &&
                        l.JournalEntry!.Status == JournalEntryStatus.Posted &&
                        l.JournalEntry.EntryDate >= request.FromDate &&
                        l.JournalEntry.EntryDate <= request.ToDate);

        if (request.BranchId.HasValue)
            linesQuery = linesQuery.Where(l => l.BranchId == request.BranchId.Value);

        var lines = await linesQuery.ToListAsync(cancellationToken);
        var balances = lines.GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => (Debit: g.Sum(x => x.Debit), Credit: g.Sum(x => x.Credit)));

        var revenues = new List<FinancialStatementItemDto>();
        var cogs = new List<FinancialStatementItemDto>();
        var opExpenses = new List<FinancialStatementItemDto>();
        var otherGainsLosses = new List<FinancialStatementItemDto>();

        foreach (var acc in accounts.Where(a => a.IsPosting))
        {
            balances.TryGetValue(acc.Id, out var bal);

            if (acc.AccountType == AccountType.Revenue)
            {
                var netRevenue = bal.Credit - bal.Debit; // Revenues normal balance is Credit
                if (netRevenue != 0)
                {
                    revenues.Add(new FinancialStatementItemDto
                    {
                        AccountId = acc.Id,
                        AccountCode = acc.AccountCode,
                        AccountNameAr = acc.NameAr,
                        AccountNameEn = acc.NameEn,
                        Level = acc.Level,
                        Amount = netRevenue
                    });
                }
            }
            else if (acc.AccountType == AccountType.Expense)
            {
                var netExpense = bal.Debit - bal.Credit; // Expenses normal balance is Debit

                if (acc.AccountCode.StartsWith("51"))
                {
                    // Cost of Goods Sold / Cost of Activities
                    if (netExpense != 0)
                    {
                        cogs.Add(new FinancialStatementItemDto
                        {
                            AccountId = acc.Id,
                            AccountCode = acc.AccountCode,
                            AccountNameAr = acc.NameAr,
                            AccountNameEn = acc.NameEn,
                            Level = acc.Level,
                            Amount = netExpense
                        });
                    }
                }
                else if (acc.AccountCode.StartsWith("5204"))
                {
                    // Forex Differences / Other gains or losses
                    if (netExpense != 0)
                    {
                        otherGainsLosses.Add(new FinancialStatementItemDto
                        {
                            AccountId = acc.Id,
                            AccountCode = acc.AccountCode,
                            AccountNameAr = acc.NameAr,
                            AccountNameEn = acc.NameEn,
                            Level = acc.Level,
                            Amount = -netExpense // Expense reduces profit
                        });
                    }
                }
                else
                {
                    // General & Operating Expenses
                    if (netExpense != 0)
                    {
                        opExpenses.Add(new FinancialStatementItemDto
                        {
                            AccountId = acc.Id,
                            AccountCode = acc.AccountCode,
                            AccountNameAr = acc.NameAr,
                            AccountNameEn = acc.NameEn,
                            Level = acc.Level,
                            Amount = netExpense
                        });
                    }
                }
            }
        }

        var totalRev = revenues.Sum(r => r.Amount);
        var totalCogs = cogs.Sum(c => c.Amount);
        var grossProfit = totalRev - totalCogs;

        var totalOpExp = opExpenses.Sum(e => e.Amount);
        var operatingProfit = grossProfit - totalOpExp;

        var totalOther = otherGainsLosses.Sum(o => o.Amount);
        var netProfit = operatingProfit + totalOther;

        return new IncomeStatementReportDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Revenues = revenues,
            TotalRevenue = totalRev,
            CostOfGoodsSold = cogs,
            TotalCostOfGoodsSold = totalCogs,
            GrossProfit = grossProfit,
            OperatingExpenses = opExpenses,
            TotalOperatingExpenses = totalOpExp,
            OperatingProfit = operatingProfit,
            OtherGainsAndLosses = otherGainsLosses,
            TotalOtherGainsAndLosses = totalOther,
            NetProfit = netProfit
        };
    }

    public async Task<BalanceSheetReportDto> GetBalanceSheetAsync(BalanceSheetRequestDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        // 1. Fetch balance sheet accounts (Asset, Liability, Equity)
        var accounts = await _context.Accounts
            .Where(a => a.CompanyId == companyId &&
                        !a.IsDeleted &&
                        (a.AccountType == AccountType.Asset ||
                         a.AccountType == AccountType.Liability ||
                         a.AccountType == AccountType.Equity))
            .OrderBy(a => a.AccountCode)
            .ToListAsync(cancellationToken);

        // 2. Fetch all posted lines up to AsOfDate
        var linesQuery = _context.JournalEntryLines
            .Where(l => l.CompanyId == companyId &&
                        l.JournalEntry!.Status == JournalEntryStatus.Posted &&
                        l.JournalEntry.EntryDate <= request.AsOfDate);

        if (request.BranchId.HasValue)
            linesQuery = linesQuery.Where(l => l.BranchId == request.BranchId.Value);

        var lines = await linesQuery.ToListAsync(cancellationToken);
        var balances = lines.GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => (Debit: g.Sum(x => x.Debit), Credit: g.Sum(x => x.Credit)));

        var currentAssets = new List<FinancialStatementItemDto>();
        var nonCurrentAssets = new List<FinancialStatementItemDto>();
        var currentLiabilities = new List<FinancialStatementItemDto>();
        var nonCurrentLiabilities = new List<FinancialStatementItemDto>();
        var equityItems = new List<FinancialStatementItemDto>();

        foreach (var acc in accounts.Where(a => a.IsPosting))
        {
            balances.TryGetValue(acc.Id, out var bal);

            if (acc.AccountType == AccountType.Asset)
            {
                // Contra-asset (like accumulated depreciation) has Nature = Credit
                var assetAmount = acc.Nature == AccountNature.Debit ? (bal.Debit - bal.Credit) : -(bal.Credit - bal.Debit);
                if (assetAmount != 0)
                {
                    var item = new FinancialStatementItemDto
                    {
                        AccountId = acc.Id,
                        AccountCode = acc.AccountCode,
                        AccountNameAr = acc.NameAr,
                        AccountNameEn = acc.NameEn,
                        Level = acc.Level,
                        Amount = assetAmount
                    };

                    if (acc.AccountCode.StartsWith("11"))
                        currentAssets.Add(item);
                    else
                        nonCurrentAssets.Add(item);
                }
            }
            else if (acc.AccountType == AccountType.Liability)
            {
                var liabAmount = bal.Credit - bal.Debit;
                if (liabAmount != 0)
                {
                    var item = new FinancialStatementItemDto
                    {
                        AccountId = acc.Id,
                        AccountCode = acc.AccountCode,
                        AccountNameAr = acc.NameAr,
                        AccountNameEn = acc.NameEn,
                        Level = acc.Level,
                        Amount = liabAmount
                    };

                    if (acc.AccountCode.StartsWith("21"))
                        currentLiabilities.Add(item);
                    else
                        nonCurrentLiabilities.Add(item);
                }
            }
            else if (acc.AccountType == AccountType.Equity)
            {
                var eqAmount = bal.Credit - bal.Debit;
                if (eqAmount != 0)
                {
                    equityItems.Add(new FinancialStatementItemDto
                    {
                        AccountId = acc.Id,
                        AccountCode = acc.AccountCode,
                        AccountNameAr = acc.NameAr,
                        AccountNameEn = acc.NameEn,
                        Level = acc.Level,
                        Amount = eqAmount
                    });
                }
            }
        }

        // 3. Compute Current Year Net Profit from Income Statement up to AsOfDate
        var startOfYear = new DateTime(request.AsOfDate.Year, 1, 1);
        var incomeStatement = await GetIncomeStatementAsync(new IncomeStatementRequestDto
        {
            FromDate = startOfYear,
            ToDate = request.AsOfDate,
            BranchId = request.BranchId
        }, cancellationToken);

        var currentYearProfit = incomeStatement.NetProfit;

        var totalCurAssets = currentAssets.Sum(a => a.Amount);
        var totalNonCurAssets = nonCurrentAssets.Sum(a => a.Amount);
        var totalAssets = totalCurAssets + totalNonCurAssets;

        var totalCurLiab = currentLiabilities.Sum(l => l.Amount);
        var totalNonCurLiab = nonCurrentLiabilities.Sum(l => l.Amount);
        var totalLiab = totalCurLiab + totalNonCurLiab;

        var totalEquity = equityItems.Sum(e => e.Amount) + currentYearProfit;
        var totalLiabAndEquity = totalLiab + totalEquity;

        var isBalanced = Math.Abs(totalAssets - totalLiabAndEquity) < 0.0001m;

        return new BalanceSheetReportDto
        {
            AsOfDate = request.AsOfDate,
            CurrentAssets = currentAssets,
            TotalCurrentAssets = totalCurAssets,
            NonCurrentAssets = nonCurrentAssets,
            TotalNonCurrentAssets = totalNonCurAssets,
            TotalAssets = totalAssets,
            CurrentLiabilities = currentLiabilities,
            TotalCurrentLiabilities = totalCurLiab,
            NonCurrentLiabilities = nonCurrentLiabilities,
            TotalNonCurrentLiabilities = totalNonCurLiab,
            TotalLiabilities = totalLiab,
            Equity = equityItems,
            CurrentYearNetProfit = currentYearProfit,
            TotalEquity = totalEquity,
            TotalLiabilitiesAndEquity = totalLiabAndEquity,
            IsBalanced = isBalanced
        };
    }

    public async Task<CashFlowStatementReportDto> GetCashFlowStatementAsync(CashFlowStatementRequestDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        // 1. Identify cash and bank accounts (code starts with 1101)
        var cashAccounts = await _context.Accounts
            .Where(a => a.CompanyId == companyId && a.AccountCode.StartsWith("1101") && a.IsPosting && !a.IsDeleted)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // 2. Beginning cash balance (before FromDate)
        var beginningCash = await _context.JournalEntryLines
            .Where(l => cashAccounts.Contains(l.AccountId) &&
                        l.CompanyId == companyId &&
                        l.JournalEntry!.Status == JournalEntryStatus.Posted &&
                        l.JournalEntry.EntryDate < request.FromDate)
            .SumAsync(l => (decimal?)(l.Debit - l.Credit), cancellationToken) ?? 0m;

        // 3. Operating Net Income
        var income = await GetIncomeStatementAsync(new IncomeStatementRequestDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            BranchId = request.BranchId
        }, cancellationToken);

        var operatingItems = new List<CashFlowItemDto>
        {
            new() { DescriptionAr = "صافي الدخل / الربح للفترة", DescriptionEn = "Net Profit for the Period", Amount = income.NetProfit }
        };

        // Changes in working capital: Receivables, Payables
        var periodCashLines = await _context.JournalEntryLines
            .Include(l => l.Account)
            .Where(l => l.CompanyId == companyId &&
                        l.JournalEntry!.Status == JournalEntryStatus.Posted &&
                        l.JournalEntry.EntryDate >= request.FromDate &&
                        l.JournalEntry.EntryDate <= request.ToDate)
            .ToListAsync(cancellationToken);

        // Net movement in Cash accounts during the period
        var netCashMovement = periodCashLines
            .Where(l => cashAccounts.Contains(l.AccountId))
            .Sum(l => l.Debit - l.Credit);

        var netOperating = netCashMovement; // Direct cash movement representation
        var endingCash = beginningCash + netCashMovement;

        return new CashFlowStatementReportDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            BeginningCashBalance = beginningCash,
            OperatingActivities = operatingItems,
            NetOperatingCashFlow = netOperating,
            InvestingActivities = new List<CashFlowItemDto>(),
            NetInvestingCashFlow = 0m,
            FinancingActivities = new List<CashFlowItemDto>(),
            NetFinancingCashFlow = 0m,
            NetCashChange = netCashMovement,
            EndingCashBalance = endingCash
        };
    }
}
