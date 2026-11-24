using AccountingERP.Domain.Common;
using AccountingERP.Domain.Enums;

namespace AccountingERP.Domain.Entities;

public class Country : BaseEntity<int>
{
    public string Code { get; set; } = string.Empty; // ISO 2-letter: SA, EG, AE...
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? PhoneCode { get; set; }
}

public class Currency : BaseEntity<int>
{
    public string Code { get; set; } = string.Empty; // ISO 3-letter: SAR, USD, EUR...
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; } = 2;
    public bool IsActive { get; set; } = true;
}

public class ExchangeRate : AuditableEntity<long>, ITenantEntity
{
    public int CompanyId { get; set; }
    public int FromCurrencyId { get; set; }
    public int ToCurrencyId { get; set; }
    public DateTime RateDate { get; set; }
    public decimal Rate { get; set; } // Direct Quotation: 1 Foreign = Rate * BaseCurrency

    // Navigation
    public Company? Company { get; set; }
    public Currency? FromCurrency { get; set; }
    public Currency? ToCurrency { get; set; }
}

public class TaxType : AuditableEntity<int>, ITenantEntity
{
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. VAT
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public TaxCategory Category { get; set; } = TaxCategory.Sales;
    public int AccountId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Company? Company { get; set; }
    public Account? Account { get; set; }
    public ICollection<TaxRate> TaxRates { get; set; } = new List<TaxRate>();
}

public class TaxRate : AuditableEntity<int>, ITenantEntity
{
    public int CompanyId { get; set; }
    public int TaxTypeId { get; set; }
    public decimal Rate { get; set; } // e.g. 15.00
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public TaxType? TaxType { get; set; }
}
