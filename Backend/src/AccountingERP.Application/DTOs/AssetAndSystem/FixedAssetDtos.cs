using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.DTOs.AssetAndSystem;

public class FixedAssetDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal SalvageValue { get; set; }
    public int UsefulLifeMonths { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public string DepreciationMethodName => DepreciationMethod.ToString();
    public int AssetAccountId { get; set; }
    public string? AssetAccountCode { get; set; }
    public string? AssetAccountName { get; set; }
    public int AccumulatedDepreciationAccountId { get; set; }
    public string? AccumulatedDepreciationAccountCode { get; set; }
    public string? AccumulatedDepreciationAccountName { get; set; }
    public int DepreciationExpenseAccountId { get; set; }
    public string? DepreciationExpenseAccountCode { get; set; }
    public string? DepreciationExpenseAccountName { get; set; }
    public FixedAssetStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal TotalAccumulatedDepreciation { get; set; }
    public decimal NetBookValue => Math.Max(0m, PurchaseCost - TotalAccumulatedDepreciation);
    public decimal MonthlyDepreciationAmount => UsefulLifeMonths > 0 
        ? Math.Round((PurchaseCost - SalvageValue) / UsefulLifeMonths, 4) 
        : 0m;
}

public class CreateFixedAssetDto
{
    public int? BranchId { get; set; }
    public string? AssetCode { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    public decimal PurchaseCost { get; set; }
    public decimal SalvageValue { get; set; } = 0m;
    public int UsefulLifeMonths { get; set; } = 36; // 3 years default
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public int? AssetAccountId { get; set; }
    public int? AccumulatedDepreciationAccountId { get; set; }
    public int? DepreciationExpenseAccountId { get; set; }
}

public class UpdateFixedAssetDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal SalvageValue { get; set; }
    public int UsefulLifeMonths { get; set; }
    public int? AssetAccountId { get; set; }
    public int? AccumulatedDepreciationAccountId { get; set; }
    public int? DepreciationExpenseAccountId { get; set; }
}

public class FixedAssetFilterRequest
{
    public string? SearchTerm { get; set; }
    public FixedAssetStatus? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AssetDepreciationDto
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public int FiscalPeriodId { get; set; }
    public string FiscalPeriodName { get; set; } = string.Empty;
    public DateTime DepreciationDate { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal BookValueAfterDepreciation { get; set; }
    public long? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
}

public class PostAssetDepreciationDto
{
    public int FixedAssetId { get; set; }
    public int? FiscalPeriodId { get; set; }
    public DateTime DepreciationDate { get; set; } = DateTime.UtcNow;
}

public class DisposeFixedAssetDto
{
    public int FixedAssetId { get; set; }
    public DateTime DisposalDate { get; set; } = DateTime.UtcNow;
    public decimal SaleProceeds { get; set; } // Cash received (0 if scrapped)
    public int? BankAccountId { get; set; }
    public string? Notes { get; set; }
}

public class AssetDisposalResultDto
{
    public int FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public decimal PurchaseCost { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
    public decimal SaleProceeds { get; set; }
    public decimal GainLossOnDisposal => SaleProceeds - NetBookValue;
    public bool IsGain => GainLossOnDisposal >= 0;
    public long JournalEntryId { get; set; }
    public string JournalEntryNumber { get; set; } = string.Empty;
}
