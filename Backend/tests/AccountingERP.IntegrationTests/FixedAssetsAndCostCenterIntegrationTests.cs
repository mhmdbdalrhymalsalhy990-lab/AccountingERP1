using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.DTOs.AssetAndSystem;
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
public class FixedAssetsAndCostCenterIntegrationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private (ApplicationDbContext Context,
            FixedAssetService AssetService,
            CostCenterService CostCenterService,
            CurrentTenantService TenantService) CreateServices()
    {
        var httpContextAccessor = new HttpContextAccessor();
        var currentTenantService = new CurrentTenantService(httpContextAccessor);
        var currentUserService = new CurrentUserService(httpContextAccessor);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        optionsBuilder.AddInterceptors(new AuditingAndTenantInterceptor(currentTenantService, currentUserService));

        var context = new ApplicationDbContext(optionsBuilder.Options, currentTenantService);

        var journalService = new JournalEntryService(context, currentTenantService, currentUserService, NullLogger<JournalEntryService>.Instance);
        var assetService = new FixedAssetService(context, currentTenantService, journalService, NullLogger<FixedAssetService>.Instance);
        var costCenterService = new CostCenterService(context, currentTenantService, NullLogger<CostCenterService>.Instance);

        return (context, assetService, costCenterService, currentTenantService);
    }

    [TestMethod]
    public async Task Test_01_CostCenter_Tree_And_Hierarchy()
    {
        var (context, _, costCenterService, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        // 1. Create Parent Cost Center (Level 1)
        var parent = await costCenterService.CreateAsync(new CreateCostCenterDto
        {
            Code = $"CC-P-{DateTime.UtcNow.Ticks}",
            NameAr = "قطاع تقنية المعلومات والتحول الرقمي",
            NameEn = "IT & Digital Transformation Sector"
        });

        Assert.IsNotNull(parent);
        Assert.AreEqual(1, parent.Level);

        // 2. Create Child Cost Center (Level 2)
        var child = await costCenterService.CreateAsync(new CreateCostCenterDto
        {
            ParentCostCenterId = parent.Id,
            Code = $"CC-C-{DateTime.UtcNow.Ticks}",
            NameAr = "إدارة البرمجيات والأنظمة",
            NameEn = "Software & Systems Dept"
        });

        Assert.IsNotNull(child);
        Assert.AreEqual(2, child.Level);
        Assert.AreEqual(parent.Id, child.ParentCostCenterId);

        // 3. Verify Tree
        var tree = await costCenterService.GetTreeAsync();
        Assert.IsNotNull(tree);
        var rootNode = tree.FirstOrDefault(t => t.Id == parent.Id);
        Assert.IsNotNull(rootNode);
        Assert.IsTrue(rootNode.Children.Any(c => c.Id == child.Id));
    }

    [TestMethod]
    public async Task Test_02_FixedAsset_Create_And_Validation()
    {
        var (context, assetService, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        // Purchase Cost = 24,000 SAR, Useful Life = 24 months, Salvage = 0
        // Expected Monthly Depreciation = 1,000 SAR
        var asset = await assetService.CreateAsync(new CreateFixedAssetDto
        {
            AssetCode = $"FA-TEST-{DateTime.UtcNow.Ticks}",
            NameAr = "خادم ديل باور إيدج الرئيسي",
            NameEn = "Dell PowerEdge Main Server",
            PurchaseDate = new DateTime(2026, 1, 1),
            PurchaseCost = 24000.00m,
            SalvageValue = 0m,
            UsefulLifeMonths = 24
        });

        Assert.IsNotNull(asset);
        Assert.AreEqual(24000.00m, asset.PurchaseCost);
        Assert.AreEqual(0m, asset.SalvageValue);
        Assert.AreEqual(24, asset.UsefulLifeMonths);
        Assert.AreEqual(1000.00m, asset.MonthlyDepreciationAmount);
        Assert.AreEqual(24000.00m, asset.NetBookValue);
        Assert.AreEqual("120101", asset.AssetAccountCode);
        Assert.AreEqual("120102", asset.AccumulatedDepreciationAccountCode);
        Assert.AreEqual("520301", asset.DepreciationExpenseAccountCode);
        Assert.AreEqual(FixedAssetStatus.Active, asset.Status);
    }

    [TestMethod]
    public async Task Test_03_FixedAsset_Depreciation_And_AutoGLPosting()
    {
        var (context, assetService, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        // 1. Create Asset (12,000 SAR, 12 months -> 1,000 SAR/month)
        var asset = await assetService.CreateAsync(new CreateFixedAssetDto
        {
            AssetCode = $"FA-DEP-{DateTime.UtcNow.Ticks}",
            NameAr = "جهاز حاسوب محمول للأعمال",
            NameEn = "Business Laptop",
            PurchaseDate = new DateTime(2026, 1, 1),
            PurchaseCost = 12000.00m,
            SalvageValue = 0m,
            UsefulLifeMonths = 12
        });

        // 2. Post Depreciation for Jan 2026
        var depResult = await assetService.CalculateAndPostDepreciationAsync(new PostAssetDepreciationDto
        {
            FixedAssetId = asset.Id,
            DepreciationDate = new DateTime(2026, 1, 31)
        });

        Assert.IsNotNull(depResult);
        Assert.AreEqual(1000.00m, depResult.DepreciationAmount);
        Assert.AreEqual(11000.00m, depResult.BookValueAfterDepreciation);
        Assert.IsNotNull(depResult.JournalEntryId);

        // 3. Verify Generated Balanced Journal Entry
        var entry = await context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == depResult.JournalEntryId);

        Assert.IsNotNull(entry);
        Assert.AreEqual(JournalEntryStatus.Posted, entry.Status);
        Assert.AreEqual(1000.00m, entry.TotalDebit);
        Assert.AreEqual(1000.00m, entry.TotalCredit);
        Assert.AreEqual(2, entry.Lines.Count); // Debit 520301 (Expense), Credit 120102 (Accumulated)

        // 4. Verify updated asset details
        var updated = await assetService.GetByIdAsync(asset.Id);
        Assert.AreEqual(1000.00m, updated.TotalAccumulatedDepreciation);
        Assert.AreEqual(11000.00m, updated.NetBookValue);
    }

    [TestMethod]
    public async Task Test_04_FixedAsset_Disposal_GainOnSale()
    {
        var (context, assetService, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        // Cost = 10,000 SAR, Life = 10 months -> 1,000 SAR/mo
        var asset = await assetService.CreateAsync(new CreateFixedAssetDto
        {
            AssetCode = $"FA-DISP-GAIN-{DateTime.UtcNow.Ticks}",
            NameAr = "سيارة نقل خفيفة - مكسب بيع",
            NameEn = "Light Cargo Van - Gain",
            PurchaseDate = new DateTime(2026, 1, 1),
            PurchaseCost = 10000.00m,
            SalvageValue = 0m,
            UsefulLifeMonths = 10
        });

        // Depreciate 2 months: 2,000 SAR -> Net Book Value = 8,000 SAR
        await assetService.CalculateAndPostDepreciationAsync(new PostAssetDepreciationDto
        {
            FixedAssetId = asset.Id,
            DepreciationDate = new DateTime(2026, 1, 31)
        });
        await assetService.CalculateAndPostDepreciationAsync(new PostAssetDepreciationDto
        {
            FixedAssetId = asset.Id,
            DepreciationDate = new DateTime(2026, 2, 28)
        });

        // Sell for 9,000 SAR cash (Gain of 1,000 SAR)
        var disposal = await assetService.DisposeAssetAsync(new DisposeFixedAssetDto
        {
            FixedAssetId = asset.Id,
            DisposalDate = new DateTime(2026, 3, 15),
            SaleProceeds = 9000.00m,
            Notes = "بيع سيارة النقل بمكسب رأسمالي"
        });

        Assert.IsNotNull(disposal);
        Assert.AreEqual(8000.00m, disposal.NetBookValue);
        Assert.AreEqual(9000.00m, disposal.SaleProceeds);
        Assert.AreEqual(1000.00m, disposal.GainLossOnDisposal);
        Assert.IsTrue(disposal.IsGain);

        // Verify balanced journal entry:
        // Debit Cash: 9,000
        // Debit Accum Dep: 2,000
        // Credit Asset: 10,000
        // Credit Gain on Sale: 1,000
        var entry = await context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == disposal.JournalEntryId);

        Assert.IsNotNull(entry);
        Assert.AreEqual(11000.00m, entry.TotalDebit);
        Assert.AreEqual(11000.00m, entry.TotalCredit);

        var updated = await assetService.GetByIdAsync(asset.Id);
        Assert.AreEqual(FixedAssetStatus.Disposed, updated.Status);
    }

    [TestMethod]
    public async Task Test_05_FixedAsset_Disposal_LossOnSale()
    {
        var (context, assetService, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        // Cost = 10,000 SAR, Life = 10 months -> 1,000 SAR/mo
        var asset = await assetService.CreateAsync(new CreateFixedAssetDto
        {
            AssetCode = $"FA-DISP-LOSS-{DateTime.UtcNow.Ticks}",
            NameAr = "معدة تصوير مكتبية - خسارة بيع",
            NameEn = "Copier Machine - Loss",
            PurchaseDate = new DateTime(2026, 1, 1),
            PurchaseCost = 10000.00m,
            SalvageValue = 0m,
            UsefulLifeMonths = 10
        });

        // Depreciate 3 months: 3,000 SAR -> Net Book Value = 7,000 SAR
        await assetService.CalculateAndPostDepreciationAsync(new PostAssetDepreciationDto
        {
            FixedAssetId = asset.Id,
            DepreciationDate = new DateTime(2026, 1, 31)
        });
        await assetService.CalculateAndPostDepreciationAsync(new PostAssetDepreciationDto
        {
            FixedAssetId = asset.Id,
            DepreciationDate = new DateTime(2026, 2, 28)
        });
        await assetService.CalculateAndPostDepreciationAsync(new PostAssetDepreciationDto
        {
            FixedAssetId = asset.Id,
            DepreciationDate = new DateTime(2026, 3, 31)
        });

        // Sell for 5,000 SAR cash (Loss of 2,000 SAR)
        var disposal = await assetService.DisposeAssetAsync(new DisposeFixedAssetDto
        {
            FixedAssetId = asset.Id,
            DisposalDate = new DateTime(2026, 4, 15),
            SaleProceeds = 5000.00m,
            Notes = "بيع الآلة بخسارة رأسمالية لتقادمها"
        });

        Assert.IsNotNull(disposal);
        Assert.AreEqual(7000.00m, disposal.NetBookValue);
        Assert.AreEqual(5000.00m, disposal.SaleProceeds);
        Assert.AreEqual(-2000.00m, disposal.GainLossOnDisposal);
        Assert.IsFalse(disposal.IsGain);

        // Verify balanced journal entry:
        // Debit Cash: 5,000
        // Debit Accum Dep: 3,000
        // Debit Loss on Sale: 2,000
        // Credit Asset: 10,000
        var entry = await context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == disposal.JournalEntryId);

        Assert.IsNotNull(entry);
        Assert.AreEqual(10000.00m, entry.TotalDebit);
        Assert.AreEqual(10000.00m, entry.TotalCredit);

        var updated = await assetService.GetByIdAsync(asset.Id);
        Assert.AreEqual(FixedAssetStatus.Disposed, updated.Status);
    }
}
