using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.DTOs.TreasuryAndInventory;

// ==========================================
// 1. Product DTOs
// ==========================================
public class ProductDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public ProductType ProductType { get; set; }
    public string ProductTypeName => ProductType.ToString();
    public string? Barcode { get; set; }
    public string UnitName { get; set; } = "Unit";
    public decimal CostPrice { get; set; } // Moving Weighted Average Cost (WAC)
    public decimal SellingPrice { get; set; }
    public int? InventoryAccountId { get; set; }
    public string? InventoryAccountCode { get; set; }
    public string? InventoryAccountName { get; set; }
    public int? CogsAccountId { get; set; }
    public string? CogsAccountCode { get; set; }
    public string? CogsAccountName { get; set; }
    public int? SalesAccountId { get; set; }
    public string? SalesAccountCode { get; set; }
    public string? SalesAccountName { get; set; }
    public bool IsActive { get; set; }
    public decimal CurrentStockQuantity { get; set; }
    public decimal TotalStockValuation => Math.Round(CurrentStockQuantity * CostPrice, 4);
}

public class CreateProductDto
{
    public string? Code { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public ProductType ProductType { get; set; } = ProductType.Storable;
    public string? Barcode { get; set; }
    public string UnitName { get; set; } = "Unit";
    public decimal InitialCostPrice { get; set; } = 0m;
    public decimal SellingPrice { get; set; } = 0m;
    public int? InventoryAccountId { get; set; }
    public int? CogsAccountId { get; set; }
    public int? SalesAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateProductDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public ProductType ProductType { get; set; }
    public string? Barcode { get; set; }
    public string UnitName { get; set; } = "Unit";
    public decimal SellingPrice { get; set; }
    public int? InventoryAccountId { get; set; }
    public int? CogsAccountId { get; set; }
    public int? SalesAccountId { get; set; }
    public bool IsActive { get; set; }
}

public class ProductFilterRequest
{
    public string? SearchTerm { get; set; }
    public ProductType? ProductType { get; set; }
    public bool? IsActive { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// ==========================================
// 2. Warehouse DTOs
// ==========================================
public class WarehouseDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public int TotalProductsCount { get; set; }
    public decimal TotalStockValuation { get; set; }
}

public class CreateWarehouseDto
{
    public int? BranchId { get; set; }
    public string? Code { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateWarehouseDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
}

public class WarehouseStockItemDto
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UnitName { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal TotalValuation => Math.Round(Quantity * AverageCost, 4);
}

// ==========================================
// 3. Stock Transaction DTOs
// ==========================================
public class StockTransactionDto
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public StockTransactionType TransactionType { get; set; }
    public string TransactionTypeName => TransactionType.ToString();
    public DateTime TransactionDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public SourceDocumentType? ReferenceDocumentType { get; set; }
    public string? ReferenceDocumentId { get; set; }
    public long? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
}

public class CreateInboundStockDto
{
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? ReferenceDocumentId { get; set; }
}

public class CreateOutboundStockDto
{
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public decimal Quantity { get; set; }
    public string? ReferenceDocumentId { get; set; }
}

public class CreateStockTransferDto
{
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public int ProductId { get; set; }
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public class CreateStockAdjustmentDto
{
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;
    public decimal PhysicalCount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class StockTransactionFilterRequest
{
    public int? WarehouseId { get; set; }
    public int? ProductId { get; set; }
    public StockTransactionType? TransactionType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
