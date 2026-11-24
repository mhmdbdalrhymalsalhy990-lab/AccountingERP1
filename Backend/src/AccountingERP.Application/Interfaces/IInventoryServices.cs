using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.TreasuryAndInventory;

namespace AccountingERP.Application.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetPagedAsync(ProductFilterRequest filter, CancellationToken cancellationToken = default);
    Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(CreateProductDto request, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateAsync(int id, UpdateProductDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface IWarehouseService
{
    Task<List<WarehouseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WarehouseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<WarehouseDto> CreateAsync(CreateWarehouseDto request, CancellationToken cancellationToken = default);
    Task<WarehouseDto> UpdateAsync(int id, UpdateWarehouseDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<List<WarehouseStockItemDto>> GetWarehouseStockAsync(int warehouseId, CancellationToken cancellationToken = default);
}

public interface IStockTransactionService
{
    Task<PagedResult<StockTransactionDto>> GetPagedAsync(StockTransactionFilterRequest filter, CancellationToken cancellationToken = default);
    Task<StockTransactionDto> RecordInboundAsync(CreateInboundStockDto request, CancellationToken cancellationToken = default);
    Task<StockTransactionDto> RecordOutboundAsync(CreateOutboundStockDto request, CancellationToken cancellationToken = default);
    Task<(StockTransactionDto Outbound, StockTransactionDto Inbound)> RecordTransferAsync(CreateStockTransferDto request, CancellationToken cancellationToken = default);
    Task<StockTransactionDto> RecordAdjustmentAsync(CreateStockAdjustmentDto request, CancellationToken cancellationToken = default);
}
