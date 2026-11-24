using System.Security.Claims;
using AccountingERP.Application.DTOs.Auth;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AccountingERP.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var identifier = !string.IsNullOrWhiteSpace(request.UserNameOrEmail)
            ? request.UserNameOrEmail.Trim()
            : (!string.IsNullOrWhiteSpace(request.UserName)
                ? request.UserName.Trim()
                : (!string.IsNullOrWhiteSpace(request.Email) ? request.Email.Trim() : string.Empty));

        _logger.LogInformation("Attempting login for user: {Identifier}", identifier);

        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.CompanyUsers)
                .ThenInclude(cu => cu.Company!)
                    .ThenInclude(c => c.Branches)
            .Include(u => u.CompanyUsers)
                .ThenInclude(cu => cu.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.UserName == identifier || 
                                      u.Email == identifier ||
                                      (identifier.ToLower() == "admin@erp.com" && u.UserName.ToLower() == "admin") ||
                                      (identifier.ToLower() == "admin" && u.Email.ToLower() == "admin@accounting-erp.local"), cancellationToken);

        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Login failed: User not found or inactive ({Identifier})", identifier);
            throw new UnauthorizedAccessException("اسم المستخدم أو كلمة المرور غير صحيحة.");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: Invalid password for user {UserName}", request.UserName);
            throw new UnauthorizedAccessException("اسم المستخدم أو كلمة المرور غير صحيحة.");
        }

        var activeCompanyUsers = user.CompanyUsers
            .Where(cu => cu.IsActive && cu.Company != null && cu.Company.IsActive && !cu.Company.IsDeleted)
            .ToList();

        if (activeCompanyUsers.Count == 0)
        {
            _logger.LogWarning("User {UserName} has no assigned active companies.", request.UserName);
            throw new UnauthorizedAccessException("المستخدم ليس لديه أي شركات مفعلة مصرح له بالوصول إليها.");
        }

        // Determine company to log in to
        CompanyUser selectedCompanyUser;
        if (request.CompanyId.HasValue)
        {
            selectedCompanyUser = activeCompanyUsers.FirstOrDefault(cu => cu.CompanyId == request.CompanyId.Value)
                ?? throw new UnauthorizedAccessException("غير مصرح لك بالوصول إلى الشركة المحددة.");
        }
        else
        {
            selectedCompanyUser = activeCompanyUsers[0];
        }

        // Determine branch
        var companyBranches = selectedCompanyUser.Company!.Branches
            .Where(b => b.IsActive && !b.IsDeleted)
            .ToList();

        Branch? selectedBranch = null;
        if (request.BranchId.HasValue)
        {
            selectedBranch = companyBranches.FirstOrDefault(b => b.Id == request.BranchId.Value);
        }

        selectedBranch ??= companyBranches.FirstOrDefault(b => b.Id == selectedCompanyUser.DefaultBranchId)
                       ?? companyBranches.FirstOrDefault(b => b.IsMainBranch)
                       ?? companyBranches.FirstOrDefault();

        // Permissions
        var roleName = selectedCompanyUser.Role?.Name ?? "User";
        List<string> permissions;

        if (roleName == "SuperAdmin")
        {
            // SuperAdmin has all permissions
            permissions = await _context.Permissions.Select(p => p.Code).ToListAsync(cancellationToken);
        }
        else
        {
            permissions = selectedCompanyUser.Role?.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Code)
                .ToList() ?? new List<string>();
        }

        // Generate Tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.UserName,
            user.Email,
            selectedCompanyUser.CompanyId,
            selectedBranch?.Id,
            roleName,
            permissions
        );

        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var expirationMinutes = int.TryParse(_configuration["JwtSettings:AccessTokenExpirationMinutes"], out var exp) ? exp : 60;
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        // Update User with RefreshToken
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserName} logged in successfully to Company {CompanyId}", user.UserName, selectedCompanyUser.CompanyId);

        return BuildAuthResponse(user, selectedCompanyUser, selectedBranch, roleName, permissions, activeCompanyUsers, accessToken, refreshToken, expiresAt);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var principal = _jwtTokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
        {
            throw new SecurityTokenException("رمز التوثيق غير صالح.");
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new SecurityTokenException("معرف المستخدم مفقود من التوكن.");

        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.CompanyUsers)
                .ThenInclude(cu => cu.Company!)
                    .ThenInclude(c => c.Branches)
            .Include(u => u.CompanyUsers)
                .ThenInclude(cu => cu.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || !user.IsActive || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new SecurityTokenException("رمز التحديث غير صالح أو منتهي الصلاحية.");
        }

        var companyIdClaim = principal.FindFirst("company_id")?.Value;
        if (!int.TryParse(companyIdClaim, out var companyId))
        {
            throw new SecurityTokenException("معرف الشركة مفقود من التوكن.");
        }

        var activeCompanyUsers = user.CompanyUsers
            .Where(cu => cu.IsActive && cu.Company != null && cu.Company.IsActive && !cu.Company.IsDeleted)
            .ToList();

        var selectedCompanyUser = activeCompanyUsers.FirstOrDefault(cu => cu.CompanyId == companyId)
            ?? throw new UnauthorizedAccessException("لم يعد مصرحاً للمستخدم بالوصول إلى هذه الشركة.");

        var branchIdClaim = principal.FindFirst("branch_id")?.Value;
        int? branchId = int.TryParse(branchIdClaim, out var bid) ? bid : null;

        var companyBranches = selectedCompanyUser.Company!.Branches.Where(b => b.IsActive && !b.IsDeleted).ToList();
        var selectedBranch = companyBranches.FirstOrDefault(b => b.Id == branchId)
                          ?? companyBranches.FirstOrDefault(b => b.IsMainBranch);

        var roleName = selectedCompanyUser.Role?.Name ?? "User";
        List<string> permissions;
        if (roleName == "SuperAdmin")
        {
            permissions = await _context.Permissions.Select(p => p.Code).ToListAsync(cancellationToken);
        }
        else
        {
            permissions = selectedCompanyUser.Role?.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Code)
                .ToList() ?? new List<string>();
        }

        var newAccessToken = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.UserName,
            user.Email,
            selectedCompanyUser.CompanyId,
            selectedBranch?.Id,
            roleName,
            permissions
        );

        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var expirationMinutes = int.TryParse(_configuration["JwtSettings:AccessTokenExpirationMinutes"], out var exp) ? exp : 60;
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user, selectedCompanyUser, selectedBranch, roleName, permissions, activeCompanyUsers, newAccessToken, newRefreshToken, expiresAt);
    }

    public async Task<AuthResponseDto> SelectCompanyBranchAsync(string userId, SelectCompanyBranchRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.CompanyUsers)
                .ThenInclude(cu => cu.Company!)
                    .ThenInclude(c => c.Branches)
            .Include(u => u.CompanyUsers)
                .ThenInclude(cu => cu.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        var activeCompanyUsers = user.CompanyUsers
            .Where(cu => cu.IsActive && cu.Company != null && cu.Company.IsActive && !cu.Company.IsDeleted)
            .ToList();

        var selectedCompanyUser = activeCompanyUsers.FirstOrDefault(cu => cu.CompanyId == request.CompanyId)
            ?? throw new UnauthorizedAccessException("غير مصرح لك بالوصول إلى الشركة المحددة.");

        var companyBranches = selectedCompanyUser.Company!.Branches.Where(b => b.IsActive && !b.IsDeleted).ToList();
        var selectedBranch = request.BranchId.HasValue
            ? companyBranches.FirstOrDefault(b => b.Id == request.BranchId.Value)
            : companyBranches.FirstOrDefault(b => b.Id == selectedCompanyUser.DefaultBranchId)
              ?? companyBranches.FirstOrDefault(b => b.IsMainBranch)
              ?? companyBranches.FirstOrDefault();

        var roleName = selectedCompanyUser.Role?.Name ?? "User";
        List<string> permissions;
        if (roleName == "SuperAdmin")
        {
            permissions = await _context.Permissions.Select(p => p.Code).ToListAsync(cancellationToken);
        }
        else
        {
            permissions = selectedCompanyUser.Role?.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Code)
                .ToList() ?? new List<string>();
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.UserName,
            user.Email,
            selectedCompanyUser.CompanyId,
            selectedBranch?.Id,
            roleName,
            permissions
        );

        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var expirationMinutes = int.TryParse(_configuration["JwtSettings:AccessTokenExpirationMinutes"], out var exp) ? exp : 60;
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user, selectedCompanyUser, selectedBranch, roleName, permissions, activeCompanyUsers, accessToken, refreshToken, expiresAt);
    }

    public async Task<AuthResponseDto> GetCurrentUserProfileAsync(string userId, int? currentCompanyId, int? currentBranchId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.CompanyUsers)
                .ThenInclude(cu => cu.Company!)
                    .ThenInclude(c => c.Branches)
            .Include(u => u.CompanyUsers)
                .ThenInclude(cu => cu.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        var activeCompanyUsers = user.CompanyUsers
            .Where(cu => cu.IsActive && cu.Company != null && cu.Company.IsActive && !cu.Company.IsDeleted)
            .ToList();

        var selectedCompanyUser = currentCompanyId.HasValue
            ? activeCompanyUsers.FirstOrDefault(cu => cu.CompanyId == currentCompanyId.Value) ?? activeCompanyUsers[0]
            : activeCompanyUsers[0];

        var companyBranches = selectedCompanyUser.Company!.Branches.Where(b => b.IsActive && !b.IsDeleted).ToList();
        var selectedBranch = currentBranchId.HasValue
            ? companyBranches.FirstOrDefault(b => b.Id == currentBranchId.Value)
            : companyBranches.FirstOrDefault(b => b.IsMainBranch);

        var roleName = selectedCompanyUser.Role?.Name ?? "User";
        List<string> permissions;
        if (roleName == "SuperAdmin")
        {
            permissions = await _context.Permissions.Select(p => p.Code).ToListAsync(cancellationToken);
        }
        else
        {
            permissions = selectedCompanyUser.Role?.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Code)
                .ToList() ?? new List<string>();
        }

        return BuildAuthResponse(user, selectedCompanyUser, selectedBranch, roleName, permissions, activeCompanyUsers, "", "", DateTime.UtcNow);
    }

    private static AuthResponseDto BuildAuthResponse(
        ApplicationUser user,
        CompanyUser selectedCompanyUser,
        Branch? selectedBranch,
        string roleName,
        List<string> permissions,
        List<CompanyUser> activeCompanyUsers,
        string accessToken,
        string refreshToken,
        DateTime expiresAt)
    {
        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new UserSummaryDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FullName = user.FullName
            },
            ActiveCompany = new CompanySummaryDto
            {
                Id = selectedCompanyUser.CompanyId,
                Code = selectedCompanyUser.Company?.Code ?? "",
                NameAr = selectedCompanyUser.Company?.NameAr ?? "",
                NameEn = selectedCompanyUser.Company?.NameEn ?? "",
                TaxNumber = selectedCompanyUser.Company?.TaxNumber
            },
            ActiveBranch = selectedBranch != null ? new BranchSummaryDto
            {
                Id = selectedBranch.Id,
                Code = selectedBranch.Code,
                NameAr = selectedBranch.NameAr,
                NameEn = selectedBranch.NameEn,
                IsMainBranch = selectedBranch.IsMainBranch
            } : null,
            Role = roleName,
            Permissions = permissions,
            AvailableCompanies = activeCompanyUsers.Select(cu => new CompanyBranchAccessDto
            {
                CompanyId = cu.CompanyId,
                CompanyCode = cu.Company?.Code ?? "",
                CompanyNameAr = cu.Company?.NameAr ?? "",
                CompanyNameEn = cu.Company?.NameEn ?? "",
                Role = cu.Role?.Name ?? "User",
                DefaultBranchId = cu.DefaultBranchId,
                Branches = cu.Company?.Branches.Where(b => b.IsActive && !b.IsDeleted).Select(b => new BranchSummaryDto
                {
                    Id = b.Id,
                    Code = b.Code,
                    NameAr = b.NameAr,
                    NameEn = b.NameEn,
                    IsMainBranch = b.IsMainBranch
                }).ToList() ?? new List<BranchSummaryDto>()
            }).ToList()
        };
    }
}
