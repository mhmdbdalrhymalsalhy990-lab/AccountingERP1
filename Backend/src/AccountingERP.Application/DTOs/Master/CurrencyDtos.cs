namespace AccountingERP.Application.DTOs.Master;

public class CurrencyDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCurrencyDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; } = 2;
    public bool IsActive { get; set; } = true;
}

public class ExchangeRateDto
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int FromCurrencyId { get; set; }
    public string FromCurrencyCode { get; set; } = string.Empty;
    public int ToCurrencyId { get; set; }
    public string ToCurrencyCode { get; set; } = string.Empty;
    public DateTime RateDate { get; set; }
    public decimal Rate { get; set; }
}

public class SetExchangeRateDto
{
    public int FromCurrencyId { get; set; }
    public int ToCurrencyId { get; set; }
    public DateTime RateDate { get; set; } = DateTime.UtcNow;
    public decimal Rate { get; set; }
}
