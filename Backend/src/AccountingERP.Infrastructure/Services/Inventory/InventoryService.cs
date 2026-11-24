using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.DTOs.TreasuryAndInventory;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

// ==========================================
// 1. Product Service Implementation
// ==========================================
public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<ProductService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<PagedResult<ProductDto>> GetPagedAsync(ProductFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.Products
            .Include(p => p.InventoryAccount)
            .Include(p => p.CogsAccount)
            .Include(p => p.SalesAccount)
            .Where(p => p.CompanyId == companyId && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(p => p.Code.Contains(term) || p.NameAr.Contains(term) || p.NameEn.Contains(term) || (p.Barcode != null && p.Barcode.Contains(term)));
        }

        if (filter.ProductType.HasValue)
            query = query.Where(p => p.ProductType == filter.ProductType.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(p => p.IsActive == filter.IsActive.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderBy(p => p.Code)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();
        var stockMap = await CalculateProductStockMapAsync(companyId, productIds, cancellationToken);

        var dtos = products.Select(p => MapToDto(p, stockMap.GetValueOrDefault(p.Id, 0m))).ToList();
        return new PagedResult<ProductDto>(dtos, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var product = await _context.Products
            .Include(p => p.InventoryAccount)
            .Include(p => p.CogsAccount)
            .Include(p => p.SalesAccount)
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), id);

        var stockQty = await CalculateProductStockAsync(companyId, product.Id, cancellationToken);
        return MapToDto(product, stockQty);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        string code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            var count = await _context.Products.CountAsync(p => p.CompanyId == companyId, cancellationToken);
            code = $"PRD-{(count + 1):D4}";
        }

        var product = new Product
        {
            CompanyId = companyId,
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            ProductType = request.ProductType,
            Barcode = request.Barcode?.Trim(),
            UnitName = string.IsNullOrWhiteSpace(request.UnitName) ? "Unit" : request.UnitName.Trim(),
            CostPrice = request.InitialCostPrice,
            SellingPrice = request.SellingPrice,
            InventoryAccountId = request.InventoryAccountId,
            CogsAccountId = request.CogsAccountId,
            SalesAccountId = request.SalesAccountId,
            IsActive = request.IsActive
        };

        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Product '{ProductCode}' ({ProductName}) created successfully.", product.Code, product.NameAr);
        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), id);

        product.NameAr = request.NameAr.Trim();
        product.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim();
        product.ProductType = request.ProductType;
        product.Barcode = request.Barcode?.Trim();
        product.UnitName = string.IsNullOrWhiteSpace(request.UnitName) ? "Unit" : request.UnitName.Trim();
        product.SellingPrice = request.SellingPrice;
        product.InventoryAccountId = request.InventoryAccountId;
        product.CogsAccountId = request.CogsAccountId;
        product.SalesAccountId = request.SalesAccountId;
        product.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), id);

        var hasTransactions = await _context.StockTransactions.AnyAsync(t => t.ProductId == id, cancellationToken);
        var hasSales = await _context.SalesInvoiceItems.AnyAsync(i => i.ProductId == id, cancellationToken);
        var hasPurchases = await _context.PurchaseInvoiceItems.AnyAsync(i => i.ProductId == id, cancellationToken);

        if (hasTransactions || hasSales || hasPurchases)
            throw new BusinessRuleViolationException($"لا يمكن حذف الصنف '{product.NameAr}' لوجود حركات مخزنية أو فواتير مرتبطة به. يمكن إيقاف تنشيطه بدلاً من ذلك.");

        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Product {ProductId} soft deleted.", id);
    }

    private async Task<decimal> CalculateProductStockAsync(int companyId, int productId, CancellationToken cancellationToken)
    {
        var inQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.ProductId == productId &&
                        (t.TransactionType == StockTransactionType.PurchaseIn ||
                         t.TransactionType == StockTransactionType.TransferIn ||
                         t.TransactionType == StockTransactionType.InventoryAdjustment))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        var outQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.ProductId == productId &&
                        (t.TransactionType == StockTransactionType.SalesOut ||
                         t.TransactionType == StockTransactionType.TransferOut))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        return inQty - outQty;
    }

    private async Task<Dictionary<int, decimal>> CalculateProductStockMapAsync(int companyId, List<int> productIds, CancellationToken cancellationToken)
    {
        var transactions = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && productIds.Contains(t.ProductId))
            .GroupBy(t => new { t.ProductId, t.TransactionType })
            .Select(g => new { g.Key.ProductId, g.Key.TransactionType, Total = g.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<int, decimal>();
        foreach (var pid in productIds)
        {
            var pTrans = transactions.Where(t => t.ProductId == pid).ToList();
            var inQty = pTrans.Where(t => t.TransactionType == StockTransactionType.PurchaseIn ||
                                          t.TransactionType == StockTransactionType.TransferIn ||
                                          t.TransactionType == StockTransactionType.InventoryAdjustment).Sum(t => t.Total);
            var outQty = pTrans.Where(t => t.TransactionType == StockTransactionType.SalesOut ||
                                           t.TransactionType == StockTransactionType.TransferOut).Sum(t => t.Total);
            result[pid] = inQty - outQty;
        }

        return result;
    }

    private static ProductDto MapToDto(Product p, decimal currentStock)
    {
        return new ProductDto
        {
            Id = p.Id,
            CompanyId = p.CompanyId,
            Code = p.Code,
            NameAr = p.NameAr,
            NameEn = p.NameEn,
            ProductType = p.ProductType,
            Barcode = p.Barcode,
            UnitName = p.UnitName,
            CostPrice = p.CostPrice,
            SellingPrice = p.SellingPrice,
            InventoryAccountId = p.InventoryAccountId,
            InventoryAccountCode = p.InventoryAccount?.AccountCode,
            InventoryAccountName = p.InventoryAccount?.NameAr,
            CogsAccountId = p.CogsAccountId,
            CogsAccountCode = p.CogsAccount?.AccountCode,
            CogsAccountName = p.CogsAccount?.NameAr,
            SalesAccountId = p.SalesAccountId,
            SalesAccountCode = p.SalesAccount?.AccountCode,
            SalesAccountName = p.SalesAccount?.NameAr,
            IsActive = p.IsActive,
            CurrentStockQuantity = currentStock
        };
    }
}

// ==========================================
// 2. Warehouse Service Implementation
// ==========================================
public class WarehouseService : IWarehouseService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<WarehouseService> _logger;

    public WarehouseService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<WarehouseService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<List<WarehouseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var warehouses = await _context.Warehouses
            .Include(w => w.Branch)
            .Where(w => w.CompanyId == companyId && !w.IsDeleted)
            .OrderBy(w => w.Code)
            .ToListAsync(cancellationToken);

        return warehouses.Select(MapToDto).ToList();
    }

    public async Task<WarehouseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var warehouse = await _context.Warehouses
            .Include(w => w.Branch)
            .FirstOrDefaultAsync(w => w.Id == id && w.CompanyId == companyId && !w.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), id);

        return MapToDto(warehouse);
    }

    public async Task<WarehouseDto> CreateAsync(CreateWarehouseDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var branchId = request.BranchId ?? _currentTenantService.BranchId
            ?? (await _context.Branches.Where(b => b.CompanyId == companyId).Select(b => b.Id).FirstOrDefaultAsync(cancellationToken));

        if (branchId == 0)
            throw new BusinessRuleViolationException("لا يوجد فرع محدد لربط المستودع به.");

        string code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            var count = await _context.Warehouses.CountAsync(w => w.CompanyId == companyId, cancellationToken);
            code = $"WH-{(count + 1):D2}";
        }

        var warehouse = new Warehouse
        {
            CompanyId = companyId,
            BranchId = branchId,
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            Location = request.Location?.Trim(),
            IsActive = request.IsActive
        };

        await _context.Warehouses.AddAsync(warehouse, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Warehouse '{WarehouseCode}' ({WarehouseName}) created successfully.", warehouse.Code, warehouse.NameAr);
        return await GetByIdAsync(warehouse.Id, cancellationToken);
    }

    public async Task<WarehouseDto> UpdateAsync(int id, UpdateWarehouseDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == id && w.CompanyId == companyId && !w.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), id);

        warehouse.NameAr = request.NameAr.Trim();
        warehouse.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim();
        warehouse.Location = request.Location?.Trim();
        warehouse.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(warehouse.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == id && w.CompanyId == companyId && !w.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), id);

        var hasTransactions = await _context.StockTransactions.AnyAsync(t => t.WarehouseId == id, cancellationToken);
        if (hasTransactions)
            throw new BusinessRuleViolationException($"لا يمكن حذف المستودع '{warehouse.NameAr}' لوجود حركات مخزنية مسجلة به. يمكن إيقاف تنشيطه بدلاً من ذلك.");

        warehouse.IsDeleted = true;
        warehouse.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Warehouse {WarehouseId} soft deleted.", id);
    }

    public async Task<List<WarehouseStockItemDto>> GetWarehouseStockAsync(int warehouseId, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == warehouseId && w.CompanyId == companyId && !w.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), warehouseId);

        var transactions = await _context.StockTransactions
            .Include(t => t.Product)
            .Where(t => t.CompanyId == companyId && t.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);

        var grouped = transactions.GroupBy(t => t.ProductId).ToList();

        var result = new List<WarehouseStockItemDto>();
        foreach (var group in grouped)
        {
            var product = group.First().Product;
            if (product == null) continue;

            var inQty = group.Where(t => t.TransactionType == StockTransactionType.PurchaseIn ||
                                         t.TransactionType == StockTransactionType.TransferIn ||
                                         t.TransactionType == StockTransactionType.InventoryAdjustment).Sum(t => t.Quantity);
            var outQty = group.Where(t => t.TransactionType == StockTransactionType.SalesOut ||
                                          t.TransactionType == StockTransactionType.TransferOut).Sum(t => t.Quantity);
            var netQty = inQty - outQty;

            result.Add(new WarehouseStockItemDto
            {
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductName = product.NameAr,
                UnitName = product.UnitName,
                Quantity = netQty,
                AverageCost = product.CostPrice
            });
        }

        return result;
    }

    private static WarehouseDto MapToDto(Warehouse w)
    {
        return new WarehouseDto
        {
            Id = w.Id,
            CompanyId = w.CompanyId,
            BranchId = w.BranchId,
            BranchName = w.Branch?.NameAr ?? string.Empty,
            Code = w.Code,
            NameAr = w.NameAr,
            NameEn = w.NameEn,
            Location = w.Location,
            IsActive = w.IsActive,
            TotalProductsCount = 0,
            TotalStockValuation = 0m
        };
    }
}

// ==========================================
// 3. Stock Transaction Service & WAC Engine
// ==========================================
public class StockTransactionService : IStockTransactionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IJournalEntryService _journalEntryService;
    private readonly ILogger<StockTransactionService> _logger;

    public StockTransactionService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IJournalEntryService journalEntryService,
        ILogger<StockTransactionService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _journalEntryService = journalEntryService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<PagedResult<StockTransactionDto>> GetPagedAsync(StockTransactionFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.StockTransactions
            .Include(t => t.Warehouse)
            .Include(t => t.Product)
            .Include(t => t.JournalEntry)
            .Where(t => t.CompanyId == companyId);

        if (filter.WarehouseId.HasValue)
            query = query.Where(t => t.WarehouseId == filter.WarehouseId.Value);

        if (filter.ProductId.HasValue)
            query = query.Where(t => t.ProductId == filter.ProductId.Value);

        if (filter.TransactionType.HasValue)
            query = query.Where(t => t.TransactionType == filter.TransactionType.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(t => t.TransactionDate <= filter.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<StockTransactionDto>(dtos, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<StockTransactionDto> RecordInboundAsync(CreateInboundStockDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        if (request.Quantity <= 0)
            throw new BusinessRuleViolationException("كمية الإدخال المخزني يجب أن تكون أكبر من صفر.");

        if (request.UnitCost < 0)
            throw new BusinessRuleViolationException("تكلفة الوحدة لا يمكن أن تكون سالبة.");

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.CompanyId == companyId && !w.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.WarehouseId);

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.CompanyId == companyId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        // 1. Moving Weighted Average Cost (WAC) Engine
        // Current Stock Quantity
        var inQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.ProductId == product.Id &&
                        (t.TransactionType == StockTransactionType.PurchaseIn ||
                         t.TransactionType == StockTransactionType.TransferIn ||
                         t.TransactionType == StockTransactionType.InventoryAdjustment))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        var outQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.ProductId == product.Id &&
                        (t.TransactionType == StockTransactionType.SalesOut ||
                         t.TransactionType == StockTransactionType.TransferOut))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        var currentQty = inQty - outQty;

        decimal newWac;
        if (currentQty <= 0)
        {
            newWac = request.UnitCost;
        }
        else
        {
            decimal totalExistingValuation = currentQty * product.CostPrice;
            decimal incomingValuation = request.Quantity * request.UnitCost;
            newWac = Math.Round((totalExistingValuation + incomingValuation) / (currentQty + request.Quantity), 4);
        }

        product.CostPrice = newWac;

        var transaction = new StockTransaction
        {
            CompanyId = companyId,
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            TransactionType = StockTransactionType.PurchaseIn,
            TransactionDate = request.TransactionDate,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            TotalCost = Math.Round(request.Quantity * request.UnitCost, 4),
            ReferenceDocumentType = SourceDocumentType.StockTransaction,
            ReferenceDocumentId = request.ReferenceDocumentId
        };

        await _context.StockTransactions.AddAsync(transaction, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recorded Inbound Stock of {Quantity} for Product {ProductCode} at UnitCost {UnitCost}. Updated WAC: {WAC}",
            transaction.Quantity, product.Code, transaction.UnitCost, product.CostPrice);

        return MapToDto(transaction);
    }

    public async Task<StockTransactionDto> RecordOutboundAsync(CreateOutboundStockDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        if (request.Quantity <= 0)
            throw new BusinessRuleViolationException("كمية الإخراج المخزني يجب أن تكون أكبر من صفر.");

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.CompanyId == companyId && !w.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.WarehouseId);

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.CompanyId == companyId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        // Check stock availability in this specific warehouse
        var inQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.WarehouseId == warehouse.Id && t.ProductId == product.Id &&
                        (t.TransactionType == StockTransactionType.PurchaseIn ||
                         t.TransactionType == StockTransactionType.TransferIn ||
                         t.TransactionType == StockTransactionType.InventoryAdjustment))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        var outQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.WarehouseId == warehouse.Id && t.ProductId == product.Id &&
                        (t.TransactionType == StockTransactionType.SalesOut ||
                         t.TransactionType == StockTransactionType.TransferOut))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        var availableQty = inQty - outQty;

        if (availableQty < request.Quantity)
            throw new BusinessRuleViolationException($"الرصيد المتاح من الصنف '{product.NameAr}' في المستودع '{warehouse.NameAr}' هو {availableQty:F2} وهو غير كافٍ لصرف {request.Quantity:F2}.");

        var transaction = new StockTransaction
        {
            CompanyId = companyId,
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            TransactionType = StockTransactionType.SalesOut,
            TransactionDate = request.TransactionDate,
            Quantity = request.Quantity,
            UnitCost = product.CostPrice, // Evaluated at current WAC
            TotalCost = Math.Round(request.Quantity * product.CostPrice, 4),
            ReferenceDocumentType = SourceDocumentType.StockTransaction,
            ReferenceDocumentId = request.ReferenceDocumentId
        };

        await _context.StockTransactions.AddAsync(transaction, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recorded Outbound Stock of {Quantity} for Product {ProductCode} from Warehouse {WarehouseCode} at WAC {WAC}",
            transaction.Quantity, product.Code, warehouse.Code, transaction.UnitCost);

        return MapToDto(transaction);
    }

    public async Task<(StockTransactionDto Outbound, StockTransactionDto Inbound)> RecordTransferAsync(CreateStockTransferDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        if (request.FromWarehouseId == request.ToWarehouseId)
            throw new BusinessRuleViolationException("لا يمكن التحويل لنفس المستودع.");

        if (request.Quantity <= 0)
            throw new BusinessRuleViolationException("كمية التحويل يجب أن تكون أكبر من صفر.");

        var fromWarehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.FromWarehouseId && w.CompanyId == companyId && !w.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.FromWarehouseId);

        var toWarehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.ToWarehouseId && w.CompanyId == companyId && !w.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.ToWarehouseId);

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.CompanyId == companyId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        // Check availability in source warehouse
        var inQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.WarehouseId == fromWarehouse.Id && t.ProductId == product.Id &&
                        (t.TransactionType == StockTransactionType.PurchaseIn ||
                         t.TransactionType == StockTransactionType.TransferIn ||
                         t.TransactionType == StockTransactionType.InventoryAdjustment))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        var outQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.WarehouseId == fromWarehouse.Id && t.ProductId == product.Id &&
                        (t.TransactionType == StockTransactionType.SalesOut ||
                         t.TransactionType == StockTransactionType.TransferOut))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        var availableQty = inQty - outQty;

        if (availableQty < request.Quantity)
            throw new BusinessRuleViolationException($"الرصيد المتاح من الصنف '{product.NameAr}' في المستودع المصدر '{fromWarehouse.NameAr}' هو {availableQty:F2} وهو غير كافٍ لتحويل {request.Quantity:F2}.");

        var transferBatchId = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

        // 1. Outbound transaction from source warehouse
        var outTrans = new StockTransaction
        {
            CompanyId = companyId,
            WarehouseId = fromWarehouse.Id,
            ProductId = product.Id,
            TransactionType = StockTransactionType.TransferOut,
            TransactionDate = request.TransferDate,
            Quantity = request.Quantity,
            UnitCost = product.CostPrice,
            TotalCost = Math.Round(request.Quantity * product.CostPrice, 4),
            ReferenceDocumentType = SourceDocumentType.StockTransaction,
            ReferenceDocumentId = transferBatchId
        };

        // 2. Inbound transaction to destination warehouse
        var inTrans = new StockTransaction
        {
            CompanyId = companyId,
            WarehouseId = toWarehouse.Id,
            ProductId = product.Id,
            TransactionType = StockTransactionType.TransferIn,
            TransactionDate = request.TransferDate,
            Quantity = request.Quantity,
            UnitCost = product.CostPrice,
            TotalCost = Math.Round(request.Quantity * product.CostPrice, 4),
            ReferenceDocumentType = SourceDocumentType.StockTransaction,
            ReferenceDocumentId = transferBatchId
        };

        await _context.StockTransactions.AddRangeAsync(outTrans, inTrans);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stock Transfer {TransferBatchId} completed for Product {ProductCode} (Qty: {Quantity}) from {FromWH} to {ToWH}.",
            transferBatchId, product.Code, request.Quantity, fromWarehouse.Code, toWarehouse.Code);

        return (MapToDto(outTrans), MapToDto(inTrans));
    }

    public async Task<StockTransactionDto> RecordAdjustmentAsync(CreateStockAdjustmentDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        if (request.PhysicalCount < 0)
            throw new BusinessRuleViolationException("الكمية الفعلية للجرد لا يمكن أن تكون سالبة.");

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.CompanyId == companyId && !w.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.WarehouseId);

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.CompanyId == companyId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        // Calculate current book quantity in warehouse
        var inQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.WarehouseId == warehouse.Id && t.ProductId == product.Id &&
                        (t.TransactionType == StockTransactionType.PurchaseIn ||
                         t.TransactionType == StockTransactionType.TransferIn ||
                         t.TransactionType == StockTransactionType.InventoryAdjustment))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        var outQty = await _context.StockTransactions
            .Where(t => t.CompanyId == companyId && t.WarehouseId == warehouse.Id && t.ProductId == product.Id &&
                        (t.TransactionType == StockTransactionType.SalesOut ||
                         t.TransactionType == StockTransactionType.TransferOut))
            .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

        var bookQty = inQty - outQty;
        var diff = request.PhysicalCount - bookQty;

        if (diff == 0)
            throw new BusinessRuleViolationException("الكمية الفعلية تطابق رصيد الدفاتر تماماً. لا توجد فروقات جردية للتسوية.");

        var totalCost = Math.Round(Math.Abs(diff) * product.CostPrice, 4);

        // Financial accounts
        var settings = await _context.CompanySettings
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);

        int inventoryAccountId = product.InventoryAccountId
            ?? settings?.InventoryAssetAccountId
            ?? (await _context.Accounts.Where(a => a.CompanyId == companyId && a.AccountCode == "110301").Select(a => a.Id).FirstOrDefaultAsync(cancellationToken));

        int cogsAccountId = product.CogsAccountId
            ?? settings?.CogsAccountId
            ?? (await _context.Accounts.Where(a => a.CompanyId == companyId && a.AccountCode == "510101").Select(a => a.Id).FirstOrDefaultAsync(cancellationToken));

        // Create Balanced Journal Entry for Inventory Adjustment
        CreateJournalEntryDto journalEntryDto;

        if (diff > 0)
        {
            // Surplus (زيادة مخزنية): Debit Inventory Asset, Credit Other Income / Surplus
            journalEntryDto = new CreateJournalEntryDto
            {
                EntryDate = request.AdjustmentDate,
                ReferenceNumber = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Description = $"تسوية جردية - زيادة مخزنية للصنف {product.NameAr} بمستودع {warehouse.NameAr}",
                BranchId = warehouse.BranchId,
                EntryType = JournalEntryType.Manual,
                SourceDocumentType = SourceDocumentType.StockTransaction,
                Lines = new List<CreateJournalEntryLineDto>
                {
                    new()
                    {
                        AccountId = inventoryAccountId,
                        Description = $"إثبات زيادة المخزون للصنف {product.NameAr}",
                        Debit = totalCost,
                        Credit = 0m
                    },
                    new()
                    {
                        AccountId = cogsAccountId, // Or gain/loss account
                        Description = $"أرباح فروقات جرد مخزني",
                        Debit = 0m,
                        Credit = totalCost
                    }
                }
            };
        }
        else
        {
            // Deficit (عجز مخزني): Debit Inventory Loss / COGS, Credit Inventory Asset
            journalEntryDto = new CreateJournalEntryDto
            {
                EntryDate = request.AdjustmentDate,
                ReferenceNumber = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Description = $"تسوية جردية - عجز مخزني للصنف {product.NameAr} بمستودع {warehouse.NameAr}",
                BranchId = warehouse.BranchId,
                EntryType = JournalEntryType.Manual,
                SourceDocumentType = SourceDocumentType.StockTransaction,
                Lines = new List<CreateJournalEntryLineDto>
                {
                    new()
                    {
                        AccountId = cogsAccountId,
                        Description = $"إثبات عجز/خسائر الجرد المخزني",
                        Debit = totalCost,
                        Credit = 0m
                    },
                    new()
                    {
                        AccountId = inventoryAccountId,
                        Description = $"تخفيض قيمة المخزون بالعجز للصنف {product.NameAr}",
                        Debit = 0m,
                        Credit = totalCost
                    }
                }
            };
        }

        var postedEntry = await _journalEntryService.CreateAndPostAsync(journalEntryDto, cancellationToken);

        var transaction = new StockTransaction
        {
            CompanyId = companyId,
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            TransactionType = diff > 0 ? StockTransactionType.InventoryAdjustment : StockTransactionType.SalesOut,
            TransactionDate = request.AdjustmentDate,
            Quantity = Math.Abs(diff),
            UnitCost = product.CostPrice,
            TotalCost = totalCost,
            ReferenceDocumentType = SourceDocumentType.StockTransaction,
            ReferenceDocumentId = journalEntryDto.ReferenceNumber,
            JournalEntryId = postedEntry.Id
        };

        await _context.StockTransactions.AddAsync(transaction, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stock Adjustment recorded for Product {ProductCode} with Diff {Diff}. Posted Journal Entry {EntryNumber}.",
            product.Code, diff, postedEntry.EntryNumber);

        return MapToDto(transaction);
    }

    private static StockTransactionDto MapToDto(StockTransaction t)
    {
        return new StockTransactionDto
        {
            Id = t.Id,
            CompanyId = t.CompanyId,
            WarehouseId = t.WarehouseId,
            WarehouseName = t.Warehouse?.NameAr ?? string.Empty,
            ProductId = t.ProductId,
            ProductCode = t.Product?.Code ?? string.Empty,
            ProductName = t.Product?.NameAr ?? string.Empty,
            TransactionType = t.TransactionType,
            TransactionDate = t.TransactionDate,
            Quantity = t.Quantity,
            UnitCost = t.UnitCost,
            TotalCost = t.TotalCost,
            ReferenceDocumentType = t.ReferenceDocumentType,
            ReferenceDocumentId = t.ReferenceDocumentId,
            JournalEntryId = t.JournalEntryId,
            JournalEntryNumber = t.JournalEntry?.EntryNumber
        };
    }
}
