using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.DTOs.Master;

public class TaxTypeDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public TaxCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public int AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public bool IsActive { get; set; }
    public decimal CurrentRate { get; set; }
    public List<TaxRateDto> TaxRates { get; set; } = new();
}

public class TaxRateDto
{
    public int Id { get; set; }
    public int TaxTypeId { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class CreateTaxTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public TaxCategory Category { get; set; } = TaxCategory.Sales;
    public int AccountId { get; set; }
    public decimal InitialRate { get; set; } = 15.00m;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public class UpdateTaxTypeDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateTaxRateDto
{
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
}
