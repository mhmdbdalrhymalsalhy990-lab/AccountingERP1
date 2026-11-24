using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using AccountingERP.Infrastructure.Persistence.Interceptors;
using AccountingERP.Infrastructure.Persistence.Seed;
using AccountingERP.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.IntegrationTests;

[TestClass]
public class JournalEntryIntegrationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private (ApplicationDbContext Context, JournalEntryService JournalService, CurrentTenantService TenantService, CurrentUserService UserService) CreateServices()
    {
        var httpContextAccessor = new HttpContextAccessor();
        var currentTenantService = new CurrentTenantService(httpContextAccessor);
        var currentUserService = new CurrentUserService(httpContextAccessor);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        optionsBuilder.AddInterceptors(new AuditingAndTenantInterceptor(currentTenantService, currentUserService));

        var context = new ApplicationDbContext(optionsBuilder.Options, currentTenantService);

        var journalService = new JournalEntryService(context, currentTenantService, currentUserService, NullLogger<JournalEntryService>.Instance);

        return (context, journalService, currentTenantService, currentUserService);
    }

    [TestMethod]
    public async Task CreateDraft_Post_And_Reverse_FullAccountingLifecycle()
    {
        var (context, journalService, tenantService, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        // Get two posting accounts: Cash (110101) and Sales Revenue (410101)
        var cashAccount = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "110101");
        var salesAccount = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "410101");

        // 1. Create Draft Journal Entry
        var createRequest = new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 3, 15),
            ReferenceNumber = "INV-2026-001",
            Description = "إثبات مبيعات نقدية للعملاء",
            Lines = new List<CreateJournalEntryLineDto>
            {
                new() { AccountId = cashAccount.Id, Debit = 5000.00m, Credit = 0m, Description = "استلام نقدية بالصندوق" },
                new() { AccountId = salesAccount.Id, Debit = 0m, Credit = 5000.00m, Description = "إيراد مبيعات بضائع" }
            }
        };

        var draft = await journalService.CreateDraftAsync(createRequest);

        Assert.IsNotNull(draft);
        Assert.IsTrue(draft.EntryNumber.StartsWith("DRAFT-"));
        Assert.AreEqual(JournalEntryStatus.Draft, draft.Status);
        Assert.AreEqual(5000.00m, draft.TotalDebit);
        Assert.AreEqual(5000.00m, draft.TotalCredit);
        Assert.AreEqual(2, draft.Lines.Count);

        // 2. Post the Draft Entry
        var posted = await journalService.PostAsync(draft.Id);

        Assert.IsNotNull(posted);
        Assert.AreEqual(JournalEntryStatus.Posted, posted.Status);
        Assert.IsTrue(posted.EntryNumber.StartsWith("JV-2026-"), "Must receive a sequential gapless JV number");
        Assert.IsNotNull(posted.PostedAt);

        // 3. Verify Immutability (Cannot delete or update a posted entry)
        await Assert.ThrowsExceptionAsync<CannotModifyPostedEntryException>(() => journalService.DeleteDraftAsync(posted.Id));
        await Assert.ThrowsExceptionAsync<CannotModifyPostedEntryException>(() => journalService.UpdateDraftAsync(posted.Id, new UpdateJournalEntryDto
        {
            EntryDate = posted.EntryDate,
            Description = "محاولة تعديل قيد مرحل",
            Lines = createRequest.Lines
        }));

        // 4. Reverse the Posted Entry
        var reversal = await journalService.ReverseAsync(posted.Id, new ReverseJournalEntryRequestDto
        {
            ReversalDate = new DateTime(2026, 3, 20),
            Reason = "إلغاء العملية بناءً على طلب الإدارة"
        });

        Assert.IsNotNull(reversal);
        Assert.AreEqual(JournalEntryStatus.Posted, reversal.Status);
        Assert.AreEqual(JournalEntryType.Reversal, reversal.EntryType);
        Assert.IsTrue(reversal.EntryNumber.StartsWith("JV-2026-"));
        Assert.AreEqual(posted.Id, reversal.ReversedEntryId);

        // Verify reversal lines are inverted: Cash was Debit 5000, now Credit 5000. Sales was Credit 5000, now Debit 5000.
        var reversedCashLine = reversal.Lines.First(l => l.AccountId == cashAccount.Id);
        Assert.AreEqual(0m, reversedCashLine.Debit);
        Assert.AreEqual(5000.00m, reversedCashLine.Credit);

        var reversedSalesLine = reversal.Lines.First(l => l.AccountId == salesAccount.Id);
        Assert.AreEqual(5000.00m, reversedSalesLine.Debit);
        Assert.AreEqual(0m, reversedSalesLine.Credit);

        // 5. Verify Original Entry is flagged as Reversed
        var updatedOriginal = await journalService.GetByIdAsync(posted.Id);
        Assert.IsTrue(updatedOriginal.IsReversed);

        // 6. Verify Idempotency: Cannot reverse the same entry twice
        await Assert.ThrowsExceptionAsync<EntryAlreadyReversedException>(() => journalService.ReverseAsync(posted.Id, new ReverseJournalEntryRequestDto
        {
            ReversalDate = new DateTime(2026, 3, 21),
            Reason = "محاولة عكس ثانية"
        }));

        // 7. Verify Cannot reverse a reversal entry
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => journalService.ReverseAsync(reversal.Id, new ReverseJournalEntryRequestDto
        {
            ReversalDate = new DateTime(2026, 3, 21),
            Reason = "محاولة عكس قيد عكسي"
        }));
    }

    [TestMethod]
    public async Task ValidationRules_Unbalanced_SummaryAccount_ClosedPeriod()
    {
        var (context, journalService, tenantService, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        var cashAccount = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "110101");
        var salesAccount = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "410101");
        var summaryAccount = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "11"); // Level 2 Summary Account

        // 1. Unbalanced Entry Check (Debit 1000 vs Credit 800)
        var unbalancedRequest = new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 15),
            Description = "قيد غير متوازن",
            Lines = new List<CreateJournalEntryLineDto>
            {
                new() { AccountId = cashAccount.Id, Debit = 1000.00m, Credit = 0m },
                new() { AccountId = salesAccount.Id, Debit = 0m, Credit = 800.00m }
            }
        };
        await Assert.ThrowsExceptionAsync<UnbalancedJournalEntryException>(() => journalService.CreateDraftAsync(unbalancedRequest));

        // 2. Summary Account Check (Header Account is not allowed in journal lines)
        var summaryAccountRequest = new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 15),
            Description = "قيد يحتوي على حساب تجميعي",
            Lines = new List<CreateJournalEntryLineDto>
            {
                new() { AccountId = summaryAccount.Id, Debit = 1000.00m, Credit = 0m },
                new() { AccountId = salesAccount.Id, Debit = 0m, Credit = 1000.00m }
            }
        };
        await Assert.ThrowsExceptionAsync<HeaderAccountPostingException>(() => journalService.CreateDraftAsync(summaryAccountRequest));

        // 3. Closed / Out-of-Range Fiscal Period Check (Date in year 2020)
        var outOfRangeRequest = new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2020, 1, 1),
            Description = "قيد في تاريخ لا توجد به فترة مالية مفتوحة",
            Lines = new List<CreateJournalEntryLineDto>
            {
                new() { AccountId = cashAccount.Id, Debit = 1000.00m, Credit = 0m },
                new() { AccountId = salesAccount.Id, Debit = 0m, Credit = 1000.00m }
            }
        };
        await Assert.ThrowsExceptionAsync<ClosedFiscalPeriodException>(() => journalService.CreateDraftAsync(outOfRangeRequest));
    }

    [TestMethod]
    public async Task JournalEntriesController_GetPaged_ShouldReturnOk()
    {
        var (context, journalService, tenantService, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        var controller = new API.Controllers.JournalEntriesController(journalService, NullLogger<API.Controllers.JournalEntriesController>.Instance);

        var result = await controller.GetPaged(new JournalEntryFilterRequest(), CancellationToken.None);
        Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
    }
}
