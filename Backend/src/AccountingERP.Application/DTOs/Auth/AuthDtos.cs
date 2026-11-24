namespace AccountingERP.Application.DTOs.Auth;

public class LoginRequestDto
{
    public string UserName { get; set; } = string.Empty;
    public string? UserNameOrEmail { get; set; }
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public int? BranchId { get; set; }
}

public class RefreshTokenRequestDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class SelectCompanyBranchRequestDto
{
    public int CompanyId { get; set; }
    public int? BranchId { get; set; }
}

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string Token => AccessToken;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserSummaryDto User { get; set; } = new();
    public CompanySummaryDto ActiveCompany { get; set; } = new();
    public BranchSummaryDto? ActiveBranch { get; set; }
    public string Role { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public List<CompanyBranchAccessDto> AvailableCompanies { get; set; } = new();
}

public class UserSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class CompanySummaryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
}

public class BranchSummaryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsMainBranch { get; set; }
}

public class CompanyBranchAccessDto
{
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? DefaultBranchId { get; set; }
    public List<BranchSummaryDto> Branches { get; set; } = new();
}
