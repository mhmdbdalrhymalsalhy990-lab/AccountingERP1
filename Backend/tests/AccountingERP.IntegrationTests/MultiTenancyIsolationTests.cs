using System.Security.Claims;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Persistence.Context;
using AccountingERP.Infrastructure.Persistence.Interceptors;
using AccountingERP.Infrastructure.Services;
using AccountingERP.API.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.IntegrationTests;

[TestClass]
public class MultiTenancyIsolationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private (ApplicationDbContext Context, CurrentTenantService TenantService, CurrentUserService UserService) CreateContext(int? explicitCompanyId = null)
    {
        var httpContextAccessor = new HttpContextAccessor();
        var tenantService = new CurrentTenantService(httpContextAccessor);
        var userService = new CurrentUserService(httpContextAccessor);

        if (explicitCompanyId.HasValue)
        {
            tenantService.SetTenant(explicitCompanyId.Value);
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        optionsBuilder.AddInterceptors(new AuditingAndTenantInterceptor(tenantService, userService));

        var context = new ApplicationDbContext(optionsBuilder.Options, tenantService);
        return (context, tenantService, userService);
    }

    private static async Task<Company> EnsureSecondCompanyExistsAsync(ApplicationDbContext context)
    {
        var comp2 = await context.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Code == "COMP02");
        if (comp2 == null)
        {
            var saCountry = await context.Countries.FirstAsync(c => c.Code == "SA");
            var sarCurrency = await context.Currencies.FirstAsync(c => c.Code == "SAR");

            comp2 = new Company
            {
                Code = "COMP02",
                NameAr = "شركة البهاء للتجارة الدولية",
                NameEn = "Al-Baha International Trading Co.",
                TaxNumber = "311122233300003",
                CommercialRegistration = "1010998877",
                CountryId = saCountry.Id,
                BaseCurrencyId = sarCurrency.Id,
                IsActive = true
            };
            await context.Companies.AddAsync(comp2);
            await context.SaveChangesAsync();
        }
        return comp2;
    }

    [TestMethod]
    public async Task QueryFilter_WhenCompanyIdIsNull_ShouldReturnZeroTenantRecords_FailClosed()
    {
        // Arrange: DbContext with NO active tenant (CurrentCompanyId == null)
        var (context, _, _) = CreateContext(explicitCompanyId: null);

        // Act: Query tenant entities without tenant context
        var accounts = await context.Accounts.ToListAsync();
        var costCenters = await context.CostCenters.ToListAsync();
        var journalEntries = await context.JournalEntries.ToListAsync();

        // Assert: Fail-Closed principle guarantees 0 records returned to prevent data leakage
        Assert.AreEqual(0, accounts.Count, "Accounts query must return 0 records when tenant is null (Fail-Closed)");
        Assert.AreEqual(0, costCenters.Count, "CostCenters query must return 0 records when tenant is null (Fail-Closed)");
        Assert.AreEqual(0, journalEntries.Count, "JournalEntries query must return 0 records when tenant is null (Fail-Closed)");
    }

    [TestMethod]
    public async Task QueryFilter_WhenTenantIsCompany1_ShouldReturnOnlyCompany1Records()
    {
        // Arrange
        var (context, _, _) = CreateContext(explicitCompanyId: 1);

        // Act
        var accounts = await context.Accounts.ToListAsync();

        // Assert
        Assert.IsTrue(accounts.Count > 0, "Company 1 must have seeded accounts");
        Assert.IsTrue(accounts.All(a => a.CompanyId == 1), "All accounts returned must strictly belong to Company 1");
    }

    [TestMethod]
    public async Task CrossTenantDataIsolation_TwoCompanies_DataNeverCrosses()
    {
        // Arrange: Prepare Company 1 and Company 2 contexts
        var (setupContext, _, _) = CreateContext(explicitCompanyId: 1);
        var comp2 = await EnsureSecondCompanyExistsAsync(setupContext);

        var (context1, _, _) = CreateContext(explicitCompanyId: 1);
        var (context2, _, _) = CreateContext(explicitCompanyId: comp2.Id);

        var testSuffix = Guid.NewGuid().ToString("N")[..6];
        var cc1Code = $"CC1-{testSuffix}";
        var cc2Code = $"CC2-{testSuffix}";

        // Act 1: Add CostCenter in Company 1
        var cc1 = new CostCenter
        {
            Code = cc1Code,
            NameAr = $"مركز تكلفة شركة 1 - {testSuffix}",
            NameEn = $"CostCenter Co 1 - {testSuffix}",
            IsActive = true
        };
        context1.CostCenters.Add(cc1);
        await context1.SaveChangesAsync();

        // Act 2: Add CostCenter in Company 2
        var cc2 = new CostCenter
        {
            Code = cc2Code,
            NameAr = $"مركز تكلفة شركة 2 - {testSuffix}",
            NameEn = $"CostCenter Co 2 - {testSuffix}",
            IsActive = true
        };
        context2.CostCenters.Add(cc2);
        await context2.SaveChangesAsync();

        // Assert 1: Query from Company 1 context
        var company1CCs = await context1.CostCenters.ToListAsync();
        Assert.IsTrue(company1CCs.Any(c => c.Code == cc1Code), "Company 1 must see its own cost center");
        Assert.IsFalse(company1CCs.Any(c => c.Code == cc2Code), "Security Breach: Company 1 must NOT see Company 2 cost center!");

        // Assert 2: Query from Company 2 context
        var company2CCs = await context2.CostCenters.ToListAsync();
        Assert.IsTrue(company2CCs.Any(c => c.Code == cc2Code), "Company 2 must see its own cost center");
        Assert.IsFalse(company2CCs.Any(c => c.Code == cc1Code), "Security Breach: Company 2 must NOT see Company 1 cost center!");
    }

    [TestMethod]
    public async Task Interceptor_WhenCreatingEntityForDifferentTenant_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange: Tenant is 1
        var (context, _, _) = CreateContext(explicitCompanyId: 1);

        // Act & Assert: Attempt to insert an entity explicitly set to CompanyId 999
        var rogueCC = new CostCenter
        {
            CompanyId = 999,
            Code = $"ROG-{Guid.NewGuid():N}"[..12],
            NameAr = "مركز تكلفة اختراق",
            NameEn = "Rogue Cost Center"
        };
        context.CostCenters.Add(rogueCC);

        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(async () =>
        {
            await context.SaveChangesAsync();
        }, "AuditingAndTenantInterceptor must block inserting records for a different tenant");
    }

    [TestMethod]
    public async Task Interceptor_WhenModifyingEntityOfDifferentTenant_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange: Prepare Company 2 cost center
        var (setupContext, _, _) = CreateContext(explicitCompanyId: 1);
        var comp2 = await EnsureSecondCompanyExistsAsync(setupContext);

        var (context2, _, _) = CreateContext(explicitCompanyId: comp2.Id);
        var originalCC = new CostCenter
        {
            Code = $"MOD-{Guid.NewGuid():N}"[..12],
            NameAr = "مركز تكلفة شركة 2 للتعديل",
            NameEn = "Co 2 CC for Edit"
        };
        context2.CostCenters.Add(originalCC);
        await context2.SaveChangesAsync();

        // Act & Assert: In Company 1 context, attempt to modify Company 2 entity
        var (context1, _, _) = CreateContext(explicitCompanyId: 1);
        
        // Load entity using IgnoreQueryFilters to simulate entity tracking of another tenant
        var ccToTamper = await context1.CostCenters.IgnoreQueryFilters().FirstAsync(c => c.Id == originalCC.Id);
        ccToTamper.NameAr = "محاولة تعديل غير مصرحة";

        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(async () =>
        {
            await context1.SaveChangesAsync();
        }, "AuditingAndTenantInterceptor must block modifying records of a different tenant");
    }

    [TestMethod]
    public async Task TenantMiddleware_WhenUnauthenticatedWithHeader_ShouldNotSetTenant()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Company-Id"] = "1";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Unauthenticated

        var tenantService = new CurrentTenantService(new HttpContextAccessor { HttpContext = httpContext });
        var middleware = new TenantMiddleware(
            next: (innerCtx) => Task.CompletedTask,
            logger: NullLogger<TenantMiddleware>.Instance
        );

        // Act
        await middleware.InvokeAsync(httpContext, tenantService);

        // Assert: Fail-Closed guarantees that unauthenticated requests cannot set tenant context
        Assert.IsNull(tenantService.CompanyId, "Unauthenticated request with X-Company-Id header must NOT set tenant context");
    }

    [TestMethod]
    public async Task TenantMiddleware_WhenAuthenticatedWithMismatchedHeader_ShouldReturn403Forbidden()
    {
        // Arrange: Authenticated user with Token claim company_id = 1
        var httpContext = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim("company_id", "1")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        httpContext.User = new ClaimsPrincipal(identity);

        // Client sends mismatched X-Company-Id = 2
        httpContext.Request.Headers["X-Company-Id"] = "2";

        var tenantService = new CurrentTenantService(new HttpContextAccessor { HttpContext = httpContext });
        var nextExecuted = false;
        var middleware = new TenantMiddleware(
            next: (innerCtx) =>
            {
                nextExecuted = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantMiddleware>.Instance
        );

        // Act
        await middleware.InvokeAsync(httpContext, tenantService);

        // Assert: Request blocked with 403 Forbidden
        Assert.IsFalse(nextExecuted, "Pipeline must stop and not execute next delegate on tenant mismatch");
        Assert.AreEqual(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode, "Response status must be 403 Forbidden");
    }
}
