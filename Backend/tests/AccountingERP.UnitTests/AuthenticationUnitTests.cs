using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AccountingERP.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.UnitTests;

[TestClass]
public class AuthenticationUnitTests
{
    private readonly PasswordHasher _passwordHasher = new();
    private readonly JwtTokenService _jwtTokenService;

    public AuthenticationUnitTests()
    {
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

        _jwtTokenService = new JwtTokenService(configuration);
    }

    [TestMethod]
    public void HashPassword_ShouldGenerateValidSaltedHash_AndVerifySuccessfully()
    {
        const string password = "Admin@SecurePassword2026!";

        var hash = _passwordHasher.HashPassword(password);

        Assert.IsFalse(string.IsNullOrWhiteSpace(hash));
        Assert.IsTrue(hash.Contains(':'), "Hash must contain salt and hash separated by ':'");

        var isValid = _passwordHasher.VerifyPassword(password, hash);
        Assert.IsTrue(isValid, "VerifyPassword must succeed for the correct password");

        var isInvalid = _passwordHasher.VerifyPassword("WrongPassword123!", hash);
        Assert.IsFalse(isInvalid, "VerifyPassword must fail for a wrong password");
    }

    [TestMethod]
    public void GenerateAccessToken_ShouldIncludeAllTenantAndPermissionClaims()
    {
        const string userId = "user-123";
        const string userName = "accounting_admin";
        const string email = "admin@example.com";
        const int companyId = 1;
        const int branchId = 5;
        const string role = "SuperAdmin";
        var permissions = new List<string> { "COA.Manage", "JournalEntries.Post", "Reports.View" };

        var tokenString = _jwtTokenService.GenerateAccessToken(
            userId,
            userName,
            email,
            companyId,
            branchId,
            role,
            permissions
        );

        Assert.IsFalse(string.IsNullOrWhiteSpace(tokenString));

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        Assert.AreEqual(userId, token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.AreEqual(userName, token.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.AreEqual("1", token.Claims.First(c => c.Type == "company_id").Value);
        Assert.AreEqual("5", token.Claims.First(c => c.Type == "branch_id").Value);
        Assert.AreEqual(role, token.Claims.First(c => c.Type == ClaimTypes.Role).Value);

        var tokenPermissions = token.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        Assert.AreEqual(3, tokenPermissions.Count);
        CollectionAssert.Contains(tokenPermissions, "COA.Manage");
        CollectionAssert.Contains(tokenPermissions, "JournalEntries.Post");
        CollectionAssert.Contains(tokenPermissions, "Reports.View");
    }

    [TestMethod]
    public void GenerateRefreshToken_ShouldProduceCryptographicallyRandomString()
    {
        var token1 = _jwtTokenService.GenerateRefreshToken();
        var token2 = _jwtTokenService.GenerateRefreshToken();

        Assert.IsFalse(string.IsNullOrWhiteSpace(token1));
        Assert.IsFalse(string.IsNullOrWhiteSpace(token2));
        Assert.AreNotEqual(token1, token2, "Two refresh tokens generated sequentially must not be equal");
    }
}
