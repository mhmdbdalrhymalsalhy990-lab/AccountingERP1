using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.DTOs.Accounting;

public class AccountDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ParentAccountId { get; set; }
    public string? ParentAccountName { get; set; }
    public string? ParentAccountCode { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public string AccountTypeName => AccountType.ToString();
    public AccountNature Nature { get; set; }
    public string NatureName => Nature.ToString();
    public int Level { get; set; }
    public bool IsPosting { get; set; }
    public int? CurrencyId { get; set; }
    public string? CurrencyCode { get; set; }
    public bool IsActive { get; set; }
    public bool HasSubAccounts { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AccountTreeDto
{
    public int Id { get; set; }
    public int? ParentAccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }
    public int Level { get; set; }
    public bool IsPosting { get; set; }
    public bool IsActive { get; set; }
    public int? CurrencyId { get; set; }
    public List<AccountTreeDto> Children { get; set; } = new();
}

public class AccountLookupDto
{
    public int Id { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DisplayText => $"{AccountCode} - {NameAr}";
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }
    public int? CurrencyId { get; set; }
}

public class CreateAccountDto
{
    public int? ParentAccountId { get; set; }
    public string? AccountCode { get; set; } // Auto-generated if left empty
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountType? AccountType { get; set; } // Inherited from parent if not specified
    public AccountNature? Nature { get; set; } // Inherited from parent if not specified
    public bool IsPosting { get; set; } = true;
    public int? CurrencyId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateAccountDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountNature Nature { get; set; }
    public int? CurrencyId { get; set; }
    public bool IsActive { get; set; }
}

public class AccountFilterRequest
{
    public string? SearchTerm { get; set; }
    public AccountType? AccountType { get; set; }
    public int? Level { get; set; }
    public bool? IsPosting { get; set; }
    public bool? IsActive { get; set; }
    public int? ParentAccountId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
