using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Persistence.Context;
using AccountingERP.Infrastructure.Persistence.Interceptors;
using AccountingERP.Infrastructure.Persistence.Seed;
using AccountingERP.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.IntegrationTests;

[TestClass]
public class FinancialReportIntegrationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private (ApplicationDbContext Context, FinancialReportService ReportService, JournalEntryService JournalService, CurrentTenantService TenantService) CreateServices()
    {
        var httpContextAccessor = new HttpContextAccessor();
        var currentTenantService = new CurrentTenantService(httpContextAccessor);
        var currentUserService = new CurrentUserService(httpContextAccessor);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        optionsBuilder.AddInterceptors(new AuditingAndTenantInterceptor(currentTenantService, currentUserService));

        var context = new ApplicationDbContext(optionsBuilder.Options, currentTenantService);

        var reportService = new FinancialReportService(context, currentTenantService, NullLogger<FinancialReportService>.Instance);
        var journalService = new JournalEntryService(context, currentTenantService, currentUserService, NullLogger<JournalEntryService>.Instance);

        return (context, reportService, journalService, currentTenantService);
    }

    private async Task SeedFinancialTransactionsAsync(ApplicationDbContext context, JournalEntryService journalService, int companyId)
    {
        // Check if we already have transactions
        var hasEntries = await context.JournalEntries.AnyAsync(je => je.CompanyId == companyId && je.ReferenceNumber == "SEED-CAPITAL");
        if (hasEntries) return;

        var bankAccount = await context.Accounts.FirstAsync(a => a.CompanyId == companyId && a.AccountCode == "110102"); // Bank Al Rajhi
        var cashAccount = await context.Accounts.FirstAsync(a => a.CompanyId == companyId && a.AccountCode == "110101"); // Main Cash
        var capitalAccount = await context.Accounts.FirstAsync(a => a.CompanyId == companyId && a.AccountCode == "310101"); // Capital
        var salesAccount = await context.Accounts.FirstAsync(a => a.CompanyId == companyId && a.AccountCode == "410101"); // Sales
        var rentAccount = await context.Accounts.FirstAsync(a => a.CompanyId == companyId && a.AccountCode == "520201"); // Rent Expense

        // Entry 1: Capital Deposit: Debit Bank 100,000, Credit Capital 100,000
        await journalService.CreateAndPostAsync(new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 10),
            ReferenceNumber = "SEED-CAPITAL",
            Description = "إيداع رأس مال الشركة في الحساب البنكي",
            Lines = new List<CreateJournalEntryLineDto>
            {
                new() { AccountId = bankAccount.Id, Debit = 100000m, Credit = 0m, Description = "إيداع في بنك الراجحي" },
                new() { AccountId = capitalAccount.Id, Debit = 0m, Credit = 100000m, Description = "رأس المال المدفوع" }
            }
        });

        // Entry 2: Sales Revenue: Debit Cash 20,000, Credit Sales 20,000
        await journalService.CreateAndPostAsync(new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 2, 10),
            ReferenceNumber = "SEED-SALES",
            Description = "مبيعات نقدية للعملاء",
            Lines = new List<CreateJournalEntryLineDto>
            {
                new() { AccountId = cashAccount.Id, Debit = 20000m, Credit = 0m, Description = "استلام نقدية بالصندوق" },
                new() { AccountId = salesAccount.Id, Debit = 0m, Credit = 20000m, Description = "إيراد مبيعات بضائع" }
            }
        });

        // Entry 3: Rent Expense: Debit Rent 5,000, Credit Cash 5,000
        await journalService.CreateAndPostAsync(new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 2, 20),
            ReferenceNumber = "SEED-RENT",
            Description = "سداد إيجار المقر نقداً",
            Lines = new List<CreateJournalEntryLineDto>
            {
                new() { AccountId = rentAccount.Id, Debit = 5000m, Credit = 0m, Description = "مصروف إيجار المقر" },
                new() { AccountId = cashAccount.Id, Debit = 0m, Credit = 5000m, Description = "صرف من الصندوق" }
            }
        });
    }

    [TestMethod]
    public async Task GeneralLedger_ShouldCalculateRunningBalancesAccurately()
    {
        var (context, reportService, journalService, tenantService) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        await SeedFinancialTransactionsAsync(context, journalService, company.Id);

        var cashAccount = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "110101");

        var report = await reportService.GetGeneralLedgerAsync(new GeneralLedgerRequestDto
        {
            AccountId = cashAccount.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31)
        });

        Assert.IsNotNull(report);
        Assert.AreEqual("110101", report.AccountCode);
        Assert.AreEqual(AccountNature.Debit, report.Nature);
        Assert.IsTrue(report.Transactions.Count >= 2, "Cash must have at least 2 transactions (Sales & Rent)");

        // Cash running balance
        Assert.AreEqual(20000m, report.Transactions.First(t => t.Debit == 20000m).Debit);
        Assert.AreEqual(5000m, report.Transactions.First(t => t.Credit == 5000m).Credit);
        Assert.AreEqual(report.OpeningBalance + report.TotalDebit - report.TotalCredit, report.ClosingBalance, "Closing balance must equal opening balance + debit - credit");
    }

    [TestMethod]
    public async Task TrialBalance_ShouldBeStrictlyBalanced()
    {
        var (context, reportService, journalService, tenantService) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        await SeedFinancialTransactionsAsync(context, journalService, company.Id);

        var trialBalance = await reportService.GetTrialBalanceAsync(new TrialBalanceRequestDto
        {
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            ShowZeroBalances = false
        });

        Assert.IsNotNull(trialBalance);
        Assert.IsTrue(trialBalance.IsBalanced, "Trial Balance must be strictly balanced!");
        Assert.AreEqual(trialBalance.TotalEndingDebit, trialBalance.TotalEndingCredit, "Total Ending Debit must equal Total Ending Credit");
        Assert.AreEqual(trialBalance.TotalPeriodDebit, trialBalance.TotalPeriodCredit, "Total Period Debit must equal Total Period Credit");
    }

    [TestMethod]
    public async Task IncomeStatement_ShouldCalculateNetProfitCorrectly()
    {
        var (context, reportService, journalService, tenantService) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        await SeedFinancialTransactionsAsync(context, journalService, company.Id);

        var incomeStatement = await reportService.GetIncomeStatementAsync(new IncomeStatementRequestDto
        {
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31)
        });

        Assert.IsNotNull(incomeStatement);
        Assert.IsTrue(incomeStatement.TotalRevenue >= 20000m, "Revenues should be at least 20,000 SAR");
        Assert.IsTrue(incomeStatement.TotalOperatingExpenses >= 5000m, "Operating Expenses should be at least 5,000 SAR");
        var expectedNetProfit = (incomeStatement.TotalRevenue - incomeStatement.TotalCostOfGoodsSold) - incomeStatement.TotalOperatingExpenses + incomeStatement.TotalOtherGainsAndLosses;
        Assert.AreEqual(expectedNetProfit, incomeStatement.NetProfit, "Net Profit should equal Gross Profit minus Operating Expenses plus Other Gains/Losses");
    }

    [TestMethod]
    public async Task BalanceSheet_ShouldSatisfyAccountingEquation()
    {
        var (context, reportService, journalService, tenantService) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        await SeedFinancialTransactionsAsync(context, journalService, company.Id);

        var balanceSheet = await reportService.GetBalanceSheetAsync(new BalanceSheetRequestDto
        {
            AsOfDate = new DateTime(2026, 12, 31)
        });

        Assert.IsNotNull(balanceSheet);
        Assert.IsTrue(balanceSheet.TotalAssets > 0m, "Total Assets should be positive");

        // Accounting Equation: Assets == Liabilities + Equity
        Assert.IsTrue(balanceSheet.IsBalanced, "Balance sheet must be balanced: Assets == Liabilities + Equity");
        Assert.AreEqual(balanceSheet.TotalAssets, balanceSheet.TotalLiabilitiesAndEquity);
    }

    [TestMethod]
    public async Task CashFlowStatement_ShouldReconcileCashMovements()
    {
        var (context, reportService, journalService, tenantService) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        await SeedFinancialTransactionsAsync(context, journalService, company.Id);

        var cashFlow = await reportService.GetCashFlowStatementAsync(new CashFlowStatementRequestDto
        {
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31)
        });

        Assert.IsNotNull(cashFlow);
        Assert.AreEqual(cashFlow.BeginningCashBalance + cashFlow.NetCashChange, cashFlow.EndingCashBalance, "Ending cash balance must equal beginning balance plus net cash change");
    }

    [TestMethod]
    public async Task ReportsController_Endpoints_ShouldReturnOk()
    {
        var (context, reportService, _, tenantService) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        var controller = new API.Controllers.ReportsController(reportService, NullLogger<API.Controllers.ReportsController>.Instance);

        var tbResult = await controller.GetTrialBalance(new TrialBalanceRequestDto(), CancellationToken.None);
        Assert.IsInstanceOfType(tbResult.Result, typeof(OkObjectResult));

        var isResult = await controller.GetIncomeStatement(new IncomeStatementRequestDto(), CancellationToken.None);
        Assert.IsInstanceOfType(isResult.Result, typeof(OkObjectResult));

        var bsResult = await controller.GetBalanceSheet(new BalanceSheetRequestDto(), CancellationToken.None);
        Assert.IsInstanceOfType(bsResult.Result, typeof(OkObjectResult));

        var cfResult = await controller.GetCashFlow(new CashFlowStatementRequestDto(), CancellationToken.None);
        Assert.IsInstanceOfType(cfResult.Result, typeof(OkObjectResult));
    }
}
