using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Persistence.Context;
using AccountingERP.Infrastructure.Persistence.Interceptors;
using AccountingERP.Infrastructure.Persistence.Seed;
using AccountingERP.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.IntegrationTests;

[TestClass]
public class DatabaseSeedingIntegrationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private ApplicationDbContext CreateDbContext()
    {
        var httpContextAccessor = new HttpContextAccessor();
        var currentTenantService = new CurrentTenantService(httpContextAccessor);
        var currentUserService = new CurrentUserService(httpContextAccessor);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        optionsBuilder.AddInterceptors(new AuditingAndTenantInterceptor(currentTenantService, currentUserService));

        return new ApplicationDbContext(optionsBuilder.Options, currentTenantService);
    }

    [TestMethod]
    public async Task SeedAsync_ShouldSeedAllFoundationalDataSuccessfully()
    {
        using var context = CreateDbContext();

        // Execute Seeder
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        // 1. Verify Countries
        var countries = await context.Countries.ToListAsync();
        Assert.IsTrue(countries.Count >= 4, "Countries count should be at least 4");
        Assert.IsTrue(countries.Any(c => c.Code == "SA"), "Saudi Arabia must exist");
        Assert.IsTrue(countries.Any(c => c.Code == "YE"), "Yemen must exist");

        // 2. Verify Currencies
        var currencies = await context.Currencies.ToListAsync();
        Assert.IsTrue(currencies.Count >= 4, "Currencies count should be at least 4");
        Assert.IsTrue(currencies.Any(c => c.Code == "SAR"), "SAR currency must exist");
        Assert.IsTrue(currencies.Any(c => c.Code == "USD"), "USD currency must exist");
        Assert.IsTrue(currencies.Any(c => c.Code == "YER"), "YER currency must exist");

        // 3. Verify Company
        var company = await context.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Code == "COMP01");
        Assert.IsNotNull(company, "Default company COMP01 must exist");
        Assert.AreEqual("300012345600003", company.TaxNumber);

        // 4. Verify Branch
        var branch = await context.Branches.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.CompanyId == company.Id && b.Code == "BR01");
        Assert.IsNotNull(branch, "Main branch BR01 must exist");
        Assert.IsTrue(branch.IsMainBranch, "BR01 must be flagged as main branch");

        // 5. Verify Chart of Accounts (Levels 1 to 4)
        var accounts = await context.Accounts.IgnoreQueryFilters().Where(a => a.CompanyId == company.Id && !a.IsDeleted).ToListAsync();
        var level1 = accounts.Where(a => a.Level == 1).ToList();
        var level2 = accounts.Where(a => a.Level == 2).ToList();
        var level3 = accounts.Where(a => a.Level == 3).ToList();
        var level4 = accounts.Where(a => a.Level == 4).ToList();

        Assert.AreEqual(5, level1.Count, "Level 1 accounts must be 5 (Assets, Liabilities, Equity, Revenue, Expense)");
        Assert.AreEqual(8, level2.Count, "Level 2 accounts must be 8");
        Assert.AreEqual(15, level3.Count, "Level 3 accounts must be 15");
        Assert.AreEqual(19, level4.Count, "Level 4 posting accounts must be 19");
        Assert.IsTrue(level4.All(a => a.IsPosting), "All Level 4 accounts must have IsPosting = true");
        Assert.IsTrue(level1.All(a => !a.IsPosting), "Level 1 accounts must NOT be posting accounts");

        // 6. Verify Company Settings
        var settings = await context.CompanySettings.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CompanyId == company.Id);
        Assert.IsNotNull(settings, "Company settings must be configured");
        Assert.IsTrue(settings.RetainedEarningsAccountId > 0);
        Assert.IsTrue(settings.DefaultVatAccountId > 0);
        Assert.IsTrue(settings.SalesRevenueAccountId > 0);

        // 7. Verify Tax Types & Rates
        var taxType = await context.TaxTypes.IgnoreQueryFilters().Include(t => t.TaxRates).FirstOrDefaultAsync(t => t.CompanyId == company.Id && t.Code == "VAT15");
        Assert.IsNotNull(taxType, "VAT15 tax type must exist");
        Assert.AreEqual(1, taxType.TaxRates.Count, "VAT15 must have 1 tax rate");
        Assert.AreEqual(15.00m, taxType.TaxRates.First().Rate, "VAT rate must be 15%");

        // 8. Verify Fiscal Year & 13 Periods
        var fiscalYear = await context.FiscalYears.IgnoreQueryFilters().Include(fy => fy.Periods).FirstOrDefaultAsync(fy => fy.CompanyId == company.Id);
        Assert.IsNotNull(fiscalYear, "Fiscal year 2026 must exist");
        Assert.AreEqual(13, fiscalYear.Periods.Count, "Must have exactly 13 periods (12 monthly + 1 period 13)");
        Assert.IsTrue(fiscalYear.Periods.Any(p => p.PeriodNumber == 13 && p.PeriodType == FiscalPeriodType.YearEndAdjustment));

        // 9. Verify Document Sequence
        var sequence = await context.DocumentSequences.IgnoreQueryFilters().FirstOrDefaultAsync(ds => ds.CompanyId == company.Id && ds.Prefix == "JV");
        Assert.IsNotNull(sequence, "JV Document sequence must exist");

        // 10. Verify Cost Centers
        var costCenters = await context.CostCenters.IgnoreQueryFilters().Where(cc => cc.CompanyId == company.Id).ToListAsync();
        Assert.IsTrue(costCenters.Count >= 3, "Must have at least 3 initial cost centers");

        // 11. Verify Bank Account & Warehouse
        var bankAccounts = await context.BankAccounts.IgnoreQueryFilters().Where(ba => ba.CompanyId == company.Id).ToListAsync();
        Assert.IsTrue(bankAccounts.Count >= 2, "Must have at least 2 bank/cash accounts (Bank & Cash)");
        Assert.IsTrue(bankAccounts.Any(ba => ba.Code == "BA01"), "Bank account BA01 must exist");
        Assert.IsTrue(bankAccounts.Any(ba => ba.Code == "CSH01"), "Cash box CSH01 must exist");
        var warehouse = await context.Warehouses.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.CompanyId == company.Id && w.Code == "WH01");
        Assert.IsNotNull(warehouse, "Central warehouse WH01 must exist");

        // 12. Verify Permissions & Roles & Admin User
        var permissions = await context.Permissions.ToListAsync();
        Assert.IsTrue(permissions.Count >= 20, "Permissions must be at least 20");

        var roles = await context.Roles.Include(r => r.RolePermissions).ToListAsync();
        var superAdmin = roles.FirstOrDefault(r => r.Name == "SuperAdmin");
        Assert.IsNotNull(superAdmin, "SuperAdmin role must exist");
        Assert.AreEqual(permissions.Count, superAdmin.RolePermissions.Count, "SuperAdmin must have all permissions assigned");

        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.UserName == "admin");
        Assert.IsNotNull(adminUser, "Admin user must exist");

        var companyUser = await context.CompanyUsers.IgnoreQueryFilters().FirstOrDefaultAsync(cu => cu.CompanyId == company.Id && cu.UserId == adminUser.Id);
        Assert.IsNotNull(companyUser, "Admin must be associated with company COMP01");
        Assert.AreEqual(superAdmin.Id, companyUser.RoleId);
    }
}
