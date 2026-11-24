using AccountingERP.Application.DTOs.Auth;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Persistence.Context;
using AccountingERP.Infrastructure.Persistence.Interceptors;
using AccountingERP.Infrastructure.Persistence.Seed;
using AccountingERP.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.IntegrationTests;

[TestClass]
public class AuthenticationIntegrationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private (ApplicationDbContext Context, AuthService AuthService, IPasswordHasher Hasher, IJwtTokenService JwtService) CreateServices()
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

        return (context, authService, hasher, jwtService);
    }

    [TestMethod]
    public async Task LoginAsync_WithValidAdminCredentials_ShouldReturnValidAuthResponse()
    {
        var (context, authService, _, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var loginRequest = new LoginRequestDto
        {
            UserName = "admin",
            Password = "Admin@123456"
        };

        var response = await authService.LoginAsync(loginRequest);

        Assert.IsNotNull(response);
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.AccessToken), "AccessToken must not be empty");
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.RefreshToken), "RefreshToken must not be empty");
        Assert.AreEqual("admin", response.User.UserName);
        Assert.AreEqual("SuperAdmin", response.Role);
        Assert.AreEqual("COMP01", response.ActiveCompany.Code);
        Assert.IsNotNull(response.ActiveBranch);
        Assert.AreEqual("BR01", response.ActiveBranch.Code);
        Assert.IsTrue(response.Permissions.Count >= 20, "SuperAdmin must receive all system permissions");
        Assert.IsTrue(response.AvailableCompanies.Count >= 1, "Must have at least 1 available company");
    }

    [TestMethod]
    public async Task LoginAsync_WithInvalidPassword_ShouldThrowUnauthorizedAccessException()
    {
        var (context, authService, _, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var loginRequest = new LoginRequestDto
        {
            UserName = "admin",
            Password = "WrongPassword#999"
        };

        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() => authService.LoginAsync(loginRequest));
    }

    [TestMethod]
    public async Task RefreshTokenAsync_WithValidTokens_ShouldGenerateNewTokensSuccessfully()
    {
        var (context, authService, _, _) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        // 1. First login
        var loginResponse = await authService.LoginAsync(new LoginRequestDto
        {
            UserName = "admin",
            Password = "Admin@123456"
        });

        // 2. Refresh token
        var refreshResponse = await authService.RefreshTokenAsync(new RefreshTokenRequestDto
        {
            AccessToken = loginResponse.AccessToken,
            RefreshToken = loginResponse.RefreshToken
        });

        Assert.IsNotNull(refreshResponse);
        Assert.IsFalse(string.IsNullOrWhiteSpace(refreshResponse.AccessToken));
        Assert.IsFalse(string.IsNullOrWhiteSpace(refreshResponse.RefreshToken));
        Assert.AreEqual(loginResponse.ActiveCompany.Id, refreshResponse.ActiveCompany.Id);
    }

    [TestMethod]
    public async Task AuthController_LoginAndGetProfile_ShouldReturnOkWithFullContext()
    {
        var (context, authService, hasher, jwtService) = CreateServices();
        await DbInitializer.SeedAsync(context, NullLogger.Instance);

        var httpContextAccessor = new HttpContextAccessor();
        var currentTenantService = new CurrentTenantService(httpContextAccessor);
        var controller = new API.Controllers.AuthController(authService, currentTenantService, NullLogger<API.Controllers.AuthController>.Instance);

        // 1. Controller Login
        var loginResult = await controller.Login(new LoginRequestDto
        {
            UserName = "admin",
            Password = "Admin@123456"
        }, CancellationToken.None);

        Assert.IsInstanceOfType(loginResult.Result, typeof(Microsoft.AspNetCore.Mvc.OkObjectResult));
        var okResult = (Microsoft.AspNetCore.Mvc.OkObjectResult)loginResult.Result!;
        var authData = (AuthResponseDto)okResult.Value!;
        Assert.AreEqual("admin", authData.User.UserName);
        Assert.AreEqual("COMP01", authData.ActiveCompany.Code);

        // 2. Set HttpContext User
        var principal = jwtService.GetPrincipalFromExpiredToken(authData.AccessToken);
        var httpContext = new DefaultHttpContext { User = principal! };
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = httpContext };

        // 3. Controller GetProfile
        var profileResult = await controller.GetProfile(CancellationToken.None);
        Assert.IsInstanceOfType(profileResult.Result, typeof(Microsoft.AspNetCore.Mvc.OkObjectResult));
        var profileOk = (Microsoft.AspNetCore.Mvc.OkObjectResult)profileResult.Result!;
        var profileData = (AuthResponseDto)profileOk.Value!;
        Assert.AreEqual("admin", profileData.User.UserName);

        // 4. TestPermission Action
        var testResult = controller.TestPermission();
        Assert.IsInstanceOfType(testResult, typeof(Microsoft.AspNetCore.Mvc.OkObjectResult));
    }
}
