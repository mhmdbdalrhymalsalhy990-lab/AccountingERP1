namespace AccountingERP.Application.DTOs.Accounting;

public class CostCenterDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ParentCostCenterId { get; set; }
    public string? ParentCostCenterName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsActive { get; set; }
    public decimal TotalDebitMovement { get; set; }
    public decimal TotalCreditMovement { get; set; }
    public decimal NetMovement => TotalDebitMovement - TotalCreditMovement;
}

public class CostCenterTreeDto
{
    public int Id { get; set; }
    public int? ParentCostCenterId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsActive { get; set; }
    public List<CostCenterTreeDto> Children { get; set; } = new();
}

public class CreateCostCenterDto
{
    public int? ParentCostCenterId { get; set; }
    public string? Code { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class UpdateCostCenterDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
