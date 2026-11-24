using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.DTOs.Auth;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Enums;
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
public class AccountIntegrationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private (ApplicationDbContext Context, AccountService AccountService, CurrentTenantService TenantService, AuthService AuthService) CreateServices()
    {
        var httpContextAccessor = new HttpContextAccessor();
        var currentTenantService = new CurrentTenantService(httpContextAccessor);
        var currentUserService = new CurrentUserService(httpContextAccessor);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        optionsBuilder.AddInterceptors(new AuditingAndTenantInterceptor(currentTenantService, currentUserService));

        var context = new ApplicationDbContext(optionsBuilder.Options, currentTenantService);

        var myConfiguration = new Dictionary<string, string?>
        {
            {"JwtSettings:Secret", "AccountingERP_Super_Secret_Key_For_Jwt_Token_Generation_2026_SecureKey!#$%"},
            {"JwtSettings:Issuer", "AccountingERP.API"},
            {"JwtSettings:Audience", "AccountingERP.Client"},
            {"JwtSettings:AccessTokenExpirationMinutes", "60"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        var hasher = new PasswordHasher();
        var jwtService = new JwtTokenService(configuration);
        var authService = new AuthService(context, hasher, jwtService, configuration, NullLogger<AuthService>.Instance);
        var accountService = new AccountService(context, currentTenantService, NullLogger<AccountService>.Instance);

        return (context, accountService, currentTenantService, authService);
    }

    [TestMethod]
    public async Task GetAccountTreeAsync_ShouldReturnHierarchicalTreeWithAllRoots()
    {
        var (context, accountService, tenantService, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        var tree = await accountService.GetAccountTreeAsync();

        Assert.IsNotNull(tree);
        Assert.AreEqual(5, tree.Count, "Tree must have 5 root accounts (Level 1: Assets, Liabilities, Equity, Revenue, Expense)");

        var assetsNode = tree.FirstOrDefault(n => n.AccountCode == "1");
        Assert.IsNotNull(assetsNode);
        Assert.AreEqual("الأصول", assetsNode.NameAr);
        Assert.IsFalse(assetsNode.IsPosting);
        Assert.IsTrue(assetsNode.Children.Count >= 2, "Assets must contain Current and Non-Current Assets");

        var currentAssetsNode = assetsNode.Children.FirstOrDefault(n => n.AccountCode == "11");
        Assert.IsNotNull(currentAssetsNode);
        Assert.IsTrue(currentAssetsNode.Children.Count >= 3, "Current Assets must contain Cash, Receivables, Inventory");
    }

    [TestMethod]
    public async Task GetPostingAccountsLookupAsync_ShouldReturnOnlyLeafPostingAccounts()
    {
        var (context, accountService, tenantService, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        var lookup = await accountService.GetPostingAccountsLookupAsync();

        Assert.IsNotNull(lookup);
        Assert.IsTrue(lookup.Count >= 19, "Must return all Level 4 posting accounts");
        Assert.IsTrue(lookup.All(a => a.AccountCode.Length >= 6), "Posting accounts must be at leaf level");
    }

    [TestMethod]
    public async Task GenerateNextAccountCodeAsync_UnderCashParent_ShouldGenerateCorrectNextCode()
    {
        var (context, accountService, tenantService, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        var cashParent = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "1101");

        // Existing children of 1101 are 110101 and 110102. The next sequential code must be 110103.
        var nextCode = await accountService.GenerateNextAccountCodeAsync(cashParent.Id);

        Assert.AreEqual("110103", nextCode);
    }

    [TestMethod]
    public async Task CreateAccountAsync_And_DeleteAccountAsync_ValidationRules()
    {
        var (context, accountService, tenantService, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        var cashParent = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "1101");

        // 1. Create a new sub-account
        var createDto = new CreateAccountDto
        {
            ParentAccountId = cashParent.Id,
            NameAr = "بنك الرياض - حساب إضافي",
            NameEn = "Riyad Bank - Additional Account",
            IsPosting = true
        };

        var createdAccount = await accountService.CreateAccountAsync(createDto);

        Assert.IsNotNull(createdAccount);
        Assert.AreEqual("110103", createdAccount.AccountCode);
        Assert.AreEqual(4, createdAccount.Level);
        Assert.AreEqual(AccountType.Asset, createdAccount.AccountType);
        Assert.AreEqual(AccountNature.Debit, createdAccount.Nature);
        Assert.IsTrue(createdAccount.IsPosting);

        // 2. Rule: Cannot delete parent account when sub-accounts exist
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => accountService.DeleteAccountAsync(cashParent.Id));

        // 3. Rule: Cannot delete system account configured in CompanySettings (e.g. 310201 Retained Earnings)
        var retainedEarnings = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "310201");
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => accountService.DeleteAccountAsync(retainedEarnings.Id));

        // 4. Rule: Cannot delete account linked to a BankAccount (e.g. 110102 Al Rajhi)
        var bankGL = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "110102");
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => accountService.DeleteAccountAsync(bankGL.Id));

        // 5. Delete the newly created account (leaf with no transactions or mappings) - Should succeed
        await accountService.DeleteAccountAsync(createdAccount.Id);

        var deleted = await context.Accounts.IgnoreQueryFilters().FirstAsync(a => a.Id == createdAccount.Id);
        Assert.IsTrue(deleted.IsDeleted, "Account must be soft-deleted");

        // Cleanup test record permanently
        context.Accounts.Remove(deleted);
        await context.SaveChangesAsync();
    }

    [TestMethod]
    public async Task AccountsController_TreeAndLookup_ShouldReturnOk()
    {
        var (context, accountService, tenantService, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var company = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id);

        var controller = new API.Controllers.AccountsController(accountService, NullLogger<API.Controllers.AccountsController>.Instance);

        var treeResult = await controller.GetAccountTree(null, CancellationToken.None);
        Assert.IsInstanceOfType(treeResult.Result, typeof(OkObjectResult));

        var lookupResult = await controller.GetPostingAccountsLookup(AccountType.Asset, CancellationToken.None);
        Assert.IsInstanceOfType(lookupResult.Result, typeof(OkObjectResult));
    }
}
