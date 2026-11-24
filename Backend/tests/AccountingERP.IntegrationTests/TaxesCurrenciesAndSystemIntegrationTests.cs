using AccountingERP.Application.DTOs.AssetAndSystem;
using AccountingERP.Application.DTOs.Master;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Persistence.Context;
using AccountingERP.Infrastructure.Persistence.Interceptors;
using AccountingERP.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.IntegrationTests;

[TestClass]
public class TaxesCurrenciesAndSystemIntegrationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private (ApplicationDbContext Context,
            TaxService TaxService,
            CurrencyService CurrencyService,
            AuditLogService AuditLogService,
            AttachmentService AttachmentService,
            CurrentTenantService TenantService) CreateServices()
    {
        var httpContextAccessor = new HttpContextAccessor();
        var currentTenantService = new CurrentTenantService(httpContextAccessor);
        var currentUserService = new CurrentUserService(httpContextAccessor);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        optionsBuilder.AddInterceptors(new AuditingAndTenantInterceptor(currentTenantService, currentUserService));

        var context = new ApplicationDbContext(optionsBuilder.Options, currentTenantService);

        var taxService = new TaxService(context, currentTenantService, NullLogger<TaxService>.Instance);
        var currencyService = new CurrencyService(context, currentTenantService, NullLogger<CurrencyService>.Instance);
        var auditLogService = new AuditLogService(context, currentTenantService, currentUserService, NullLogger<AuditLogService>.Instance);
        var attachmentService = new AttachmentService(context, currentTenantService, currentUserService, NullLogger<AttachmentService>.Instance);

        return (context, taxService, currencyService, auditLogService, attachmentService, currentTenantService);
    }

    [TestMethod]
    public async Task Test_01_TaxType_And_Rate_Schedules()
    {
        var (context, taxService, _, _, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var vatAccount = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "210201");

        // 1. Create Tax Type with 5% initial rate
        var taxType = await taxService.CreateTaxTypeAsync(new CreateTaxTypeDto
        {
            Code = $"WHT{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            NameAr = "ضريبة الاستقطاع 5%",
            NameEn = "Withholding Tax 5%",
            Category = TaxCategory.Withholding,
            AccountId = vatAccount.Id,
            InitialRate = 5.00m,
            EffectiveFrom = new DateTime(2026, 1, 1)
        });

        Assert.IsNotNull(taxType);
        Assert.AreEqual(5.00m, taxType.CurrentRate);
        Assert.AreEqual(1, taxType.TaxRates.Count);

        // 2. Add New Rate Schedule starting July 1, 2026 (7%)
        var newRate = await taxService.AddTaxRateAsync(taxType.Id, new CreateTaxRateDto
        {
            Rate = 7.00m,
            EffectiveFrom = new DateTime(2026, 7, 1)
        });

        Assert.IsNotNull(newRate);
        Assert.AreEqual(7.00m, newRate.Rate);

        // 3. Verify Effective Rate by Date
        var rateInMarch = await taxService.GetEffectiveRateForDateAsync(taxType.Id, new DateTime(2026, 3, 15));
        var rateInAugust = await taxService.GetEffectiveRateForDateAsync(taxType.Id, new DateTime(2026, 8, 15));

        Assert.AreEqual(5.00m, rateInMarch, "Effective rate in March must be 5%");
        Assert.AreEqual(7.00m, rateInAugust, "Effective rate in August must be 7%");
    }

    [TestMethod]
    public async Task Test_02_Currencies_And_Historical_Exchange_Rates()
    {
        var (context, _, currencyService, _, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var sar = await context.Currencies.FirstAsync(c => c.Code == "SAR");
        var usd = await context.Currencies.FirstAsync(c => c.Code == "USD");

        // 1. Set Historical Rate 1: 2026-01-01 -> 1 USD = 3.750000 SAR
        await currencyService.SetExchangeRateAsync(new SetExchangeRateDto
        {
            FromCurrencyId = usd.Id,
            ToCurrencyId = sar.Id,
            RateDate = new DateTime(2026, 1, 1),
            Rate = 3.750000m
        });

        // 2. Set Historical Rate 2: 2026-03-01 -> 1 USD = 3.755000 SAR
        await currencyService.SetExchangeRateAsync(new SetExchangeRateDto
        {
            FromCurrencyId = usd.Id,
            ToCurrencyId = sar.Id,
            RateDate = new DateTime(2026, 3, 1),
            Rate = 3.755000m
        });

        // 3. Query Effective Rates
        var rateFeb = await currencyService.GetEffectiveExchangeRateAsync(usd.Id, sar.Id, new DateTime(2026, 2, 10));
        var rateMarch = await currencyService.GetEffectiveExchangeRateAsync(usd.Id, sar.Id, new DateTime(2026, 3, 15));

        Assert.AreEqual(3.750000m, rateFeb);
        Assert.AreEqual(3.755000m, rateMarch);

        // 4. Reverse Rate (SAR to USD) on Jan 15
        var reverseRate = await currencyService.GetEffectiveExchangeRateAsync(sar.Id, usd.Id, new DateTime(2026, 1, 15));
        Assert.AreEqual(Math.Round(1m / 3.75m, 6), reverseRate);

        // 5. Same Currency Rate (SAR to SAR)
        var sameRate = await currencyService.GetEffectiveExchangeRateAsync(sar.Id, sar.Id, DateTime.UtcNow);
        Assert.AreEqual(1.000000m, sameRate);
    }

    [TestMethod]
    public async Task Test_03_AuditLog_Recording_And_Querying()
    {
        var (context, _, _, auditLogService, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var testInvoiceId = $"INV-AUDIT-{DateTime.UtcNow.Ticks}";

        // 1. Record Audit Log
        await auditLogService.LogAsync(new CreateAuditLogDto
        {
            Action = "PostToGL",
            EntityName = "SalesInvoice",
            EntityId = testInvoiceId,
            OldValues = "{\"Status\": \"Draft\"}",
            NewValues = "{\"Status\": \"PostedToGL\"}",
            IpAddress = "192.168.1.100"
        });

        // 2. Query Logs
        var result = await auditLogService.GetPagedAsync(new AuditLogFilterRequest
        {
            EntityName = "SalesInvoice",
            EntityId = testInvoiceId
        });

        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalCount >= 1);
        var log = result.Items.First(l => l.EntityId == testInvoiceId);
        Assert.AreEqual("PostToGL", log.Action);
        Assert.AreEqual("SalesInvoice", log.EntityName);
        Assert.AreEqual("192.168.1.100", log.IpAddress);
    }

    [TestMethod]
    public async Task Test_04_Document_Attachments_Upload_And_Delete()
    {
        var (context, _, _, _, attachmentService, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var testEntityId = $"INV-ATTACH-{DateTime.UtcNow.Ticks}";

        // 1. Upload Attachment Metadata
        var uploaded = await attachmentService.UploadAsync(new CreateAttachmentDto
        {
            EntityName = "SalesInvoice",
            EntityId = testEntityId,
            FileName = "ZakatAndTax_EInvoice.pdf",
            FileExtension = "pdf",
            FileSizeBytes = 1048576, // 1 MB
            FilePath = "/storage/invoices/2026/01/ZakatAndTax_EInvoice.pdf"
        });

        Assert.IsNotNull(uploaded);
        Assert.IsTrue(uploaded.Id > 0);
        Assert.AreEqual("ZakatAndTax_EInvoice.pdf", uploaded.FileName);
        Assert.AreEqual("pdf", uploaded.FileExtension);

        // 2. Get Attachments by Entity
        var list = await attachmentService.GetByEntityAsync("SalesInvoice", testEntityId);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(uploaded.Id, list[0].Id);

        // 3. Delete Attachment
        await attachmentService.DeleteAsync(uploaded.Id);

        var listAfterDelete = await attachmentService.GetByEntityAsync("SalesInvoice", testEntityId);
        Assert.AreEqual(0, listAfterDelete.Count);
    }
}
