using System.Security.Claims;

namespace AccountingERP.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(
        string userId,
        string userName,
        string email,
        int companyId,
        int? branchId,
        string role,
        IEnumerable<string> permissions);

    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
