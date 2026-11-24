using AccountingERP.Application.DTOs.TreasuryAndInventory;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Persistence.Context;
using AccountingERP.Infrastructure.Persistence.Interceptors;
using AccountingERP.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.IntegrationTests;

[TestClass]
public class InventoryAndTreasuryIntegrationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private (ApplicationDbContext Context,
            ProductService ProductService,
            WarehouseService WarehouseService,
            StockTransactionService StockService,
            BankAccountService BankAccountService,
            BankReconciliationService ReconciliationService,
            CurrentTenantService TenantService) CreateServices()
    {
        var httpContextAccessor = new HttpContextAccessor();
        var currentTenantService = new CurrentTenantService(httpContextAccessor);
        var currentUserService = new CurrentUserService(httpContextAccessor);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        optionsBuilder.AddInterceptors(new AuditingAndTenantInterceptor(currentTenantService, currentUserService));

        var context = new ApplicationDbContext(optionsBuilder.Options, currentTenantService);

        var journalService = new JournalEntryService(context, currentTenantService, currentUserService, NullLogger<JournalEntryService>.Instance);
        var productService = new ProductService(context, currentTenantService, NullLogger<ProductService>.Instance);
        var warehouseService = new WarehouseService(context, currentTenantService, NullLogger<WarehouseService>.Instance);
        var stockService = new StockTransactionService(context, currentTenantService, journalService, NullLogger<StockTransactionService>.Instance);
        var bankAccountService = new BankAccountService(context, currentTenantService, NullLogger<BankAccountService>.Instance);
        var reconciliationService = new BankReconciliationService(context, currentTenantService, NullLogger<BankReconciliationService>.Instance);

        return (context, productService, warehouseService, stockService, bankAccountService, reconciliationService, currentTenantService);
    }

    [TestMethod]
    public async Task Test_01_Product_Create_And_WAC_Calculation()
    {
        var (context, productService, _, stockService, _, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var warehouse = await context.Warehouses.FirstAsync(w => w.CompanyId == company.Id);

        // 1. Create a Storable Product
        var product = await productService.CreateAsync(new CreateProductDto
        {
            Code = $"PRD-WAC-{DateTime.UtcNow.Ticks}",
            NameAr = "شاشة سامسونج احترافية 32 بوصة",
            NameEn = "Samsung Professional 32 Monitor",
            ProductType = ProductType.Storable,
            UnitName = "Piece",
            InitialCostPrice = 1000.00m,
            SellingPrice = 1600.00m
        });

        Assert.IsNotNull(product);
        Assert.AreEqual(1000.00m, product.CostPrice);
        Assert.AreEqual(0m, product.CurrentStockQuantity);

        // 2. Inbound 1: 10 units @ 1,000 SAR
        var in1 = await stockService.RecordInboundAsync(new CreateInboundStockDto
        {
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            Quantity = 10m,
            UnitCost = 1000.00m,
            ReferenceDocumentId = "BATCH-01"
        });

        Assert.AreEqual(10m, in1.Quantity);
        var pAfterIn1 = await productService.GetByIdAsync(product.Id);
        Assert.AreEqual(10m, pAfterIn1.CurrentStockQuantity);
        Assert.AreEqual(1000.00m, pAfterIn1.CostPrice);
        Assert.AreEqual(10000.00m, pAfterIn1.TotalStockValuation);

        // 3. Inbound 2: 10 units @ 1,500 SAR
        // New WAC = ((10 * 1000) + (10 * 1500)) / (10 + 10) = (10,000 + 15,000) / 20 = 25,000 / 20 = 1,250 SAR
        var in2 = await stockService.RecordInboundAsync(new CreateInboundStockDto
        {
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            Quantity = 10m,
            UnitCost = 1500.00m,
            ReferenceDocumentId = "BATCH-02"
        });

        var pAfterIn2 = await productService.GetByIdAsync(product.Id);
        Assert.AreEqual(20m, pAfterIn2.CurrentStockQuantity);
        Assert.AreEqual(1250.00m, pAfterIn2.CostPrice, "Moving WAC must be strictly 1,250 SAR");
        Assert.AreEqual(25000.00m, pAfterIn2.TotalStockValuation);

        // 4. Outbound: 5 units
        // Remaining = 15 units, WAC remains 1,250 SAR
        var out1 = await stockService.RecordOutboundAsync(new CreateOutboundStockDto
        {
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            Quantity = 5m,
            ReferenceDocumentId = "DELIVERY-01"
        });

        Assert.AreEqual(5m, out1.Quantity);
        Assert.AreEqual(1250.00m, out1.UnitCost);
        Assert.AreEqual(6250.00m, out1.TotalCost);

        var pAfterOut1 = await productService.GetByIdAsync(product.Id);
        Assert.AreEqual(15m, pAfterOut1.CurrentStockQuantity);
        Assert.AreEqual(1250.00m, pAfterOut1.CostPrice);
        Assert.AreEqual(18750.00m, pAfterOut1.TotalStockValuation);
    }

    [TestMethod]
    public async Task Test_02_Warehouse_Creation_And_Stock_Transfer()
    {
        var (context, productService, warehouseService, stockService, _, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        // 1. Create two warehouses
        var whSource = await warehouseService.CreateAsync(new CreateWarehouseDto
        {
            NameAr = "مستودع التوزيع الشمالي",
            NameEn = "North Distribution Warehouse",
            Location = "شمال الرياض"
        });

        var whDest = await warehouseService.CreateAsync(new CreateWarehouseDto
        {
            NameAr = "مستودع فرع الدمام",
            NameEn = "Dammam Branch Warehouse",
            Location = "المنطقة الشرقية"
        });

        // 2. Create Product & Inbound 30 units to Source
        var product = await productService.CreateAsync(new CreateProductDto
        {
            Code = $"PRD-TRF-{DateTime.UtcNow.Ticks}",
            NameAr = "طابعة ليزر متعددة الوظائف",
            NameEn = "Multifunction Laser Printer",
            UnitName = "Piece",
            InitialCostPrice = 800.00m
        });

        await stockService.RecordInboundAsync(new CreateInboundStockDto
        {
            WarehouseId = whSource.Id,
            ProductId = product.Id,
            Quantity = 30m,
            UnitCost = 800.00m
        });

        // 3. Transfer 12 units from Source to Dest
        var (outbound, inbound) = await stockService.RecordTransferAsync(new CreateStockTransferDto
        {
            FromWarehouseId = whSource.Id,
            ToWarehouseId = whDest.Id,
            ProductId = product.Id,
            Quantity = 12m,
            Notes = "نقل بضاعة لتغذية فرع الدمام"
        });

        Assert.IsNotNull(outbound);
        Assert.IsNotNull(inbound);
        Assert.AreEqual(StockTransactionType.TransferOut, outbound.TransactionType);
        Assert.AreEqual(StockTransactionType.TransferIn, inbound.TransactionType);
        Assert.AreEqual(12m, outbound.Quantity);
        Assert.AreEqual(12m, inbound.Quantity);

        // 4. Verify Stock per Warehouse
        var sourceStock = await warehouseService.GetWarehouseStockAsync(whSource.Id);
        var destStock = await warehouseService.GetWarehouseStockAsync(whDest.Id);

        var sourceItem = sourceStock.FirstOrDefault(s => s.ProductId == product.Id);
        var destItem = destStock.FirstOrDefault(s => s.ProductId == product.Id);

        Assert.IsNotNull(sourceItem);
        Assert.AreEqual(18m, sourceItem.Quantity, "Source warehouse remaining quantity must be 18 (30 - 12)");

        Assert.IsNotNull(destItem);
        Assert.AreEqual(12m, destItem.Quantity, "Destination warehouse quantity must be 12");
    }

    [TestMethod]
    public async Task Test_03_StockAdjustment_With_AutoGLPosting()
    {
        var (context, productService, _, stockService, _, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var warehouse = await context.Warehouses.FirstAsync(w => w.CompanyId == company.Id);

        // 1. Create Product & Inbound 25 units @ 200 SAR
        var product = await productService.CreateAsync(new CreateProductDto
        {
            Code = $"PRD-ADJ-{DateTime.UtcNow.Ticks}",
            NameAr = "ماوس لاسلكي مريح",
            NameEn = "Wireless Ergonomic Mouse",
            UnitName = "Piece",
            InitialCostPrice = 200.00m
        });

        await stockService.RecordInboundAsync(new CreateInboundStockDto
        {
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            Quantity = 25m,
            UnitCost = 200.00m
        });

        // 2. Perform Stock Adjustment: Physical Count = 22 units (Deficit of 3 units = 600 SAR)
        var adj = await stockService.RecordAdjustmentAsync(new CreateStockAdjustmentDto
        {
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            PhysicalCount = 22m,
            AdjustmentDate = new DateTime(2026, 3, 20),
            Reason = "تسوية جرد دوري لوجود تلفيات في 3 حبات"
        });

        Assert.IsNotNull(adj);
        Assert.AreEqual(3m, adj.Quantity);
        Assert.AreEqual(600.00m, adj.TotalCost);
        Assert.IsNotNull(adj.JournalEntryId);

        // 3. Verify Generated Balanced Journal Entry
        var entry = await context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == adj.JournalEntryId);

        Assert.IsNotNull(entry);
        Assert.AreEqual(JournalEntryStatus.Posted, entry.Status);
        Assert.AreEqual(600.00m, entry.TotalDebit);
        Assert.AreEqual(600.00m, entry.TotalCredit);

        // 4. Verify Net Stock matches Physical Count (22 units)
        var updatedProduct = await productService.GetByIdAsync(product.Id);
        Assert.AreEqual(22m, updatedProduct.CurrentStockQuantity);
    }

    [TestMethod]
    public async Task Test_04_Treasury_BankAccounts_And_BookBalance()
    {
        var (context, _, _, _, bankAccountService, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        // Create Cash Box linked to Main Cash GL Account (110101)
        var cashAcc = await context.Accounts.FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "110101");

        var cashBox = await bankAccountService.CreateAsync(new CreateBankAccountDto
        {
            NameAr = "صندوق المبيعات النقدية - صالة العرض",
            NameEn = "Showroom Cash Box",
            AccountId = cashAcc.Id,
            IsCash = true
        });

        Assert.IsNotNull(cashBox);
        Assert.IsTrue(cashBox.Id > 0);
        Assert.IsTrue(cashBox.IsCash);
        Assert.AreEqual("110101", cashBox.AccountCode);

        // Fetch balance
        var balance = await bankAccountService.GetBookBalanceAsync(cashBox.Id);
        Assert.IsTrue(balance >= 0m);
    }

    [TestMethod]
    public async Task Test_05_BankReconciliation_Matching_And_Difference()
    {
        var (context, _, _, _, bankAccountService, reconciliationService, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var bankAccount = await context.BankAccounts.FirstAsync(b => b.CompanyId == company.Id && !b.IsCash);

        // 1. Get current book balance from GL
        var currentBookBalance = await bankAccountService.GetBookBalanceAsync(bankAccount.Id);

        // 2. Perform Matching Reconciliation (Statement Balance == Book Balance)
        var reconMatched = await reconciliationService.PerformReconciliationAsync(new CreateBankReconciliationDto
        {
            BankAccountId = bankAccount.Id,
            ReconciliationDate = new DateTime(2026, 3, 31),
            StatementEndingBalance = currentBookBalance
        });

        Assert.IsNotNull(reconMatched);
        Assert.AreEqual(0m, reconMatched.Difference);
        Assert.IsTrue(reconMatched.IsReconciled);
        Assert.IsNotNull(reconMatched.ReconciledAt);

        // 3. Perform Unmatched Reconciliation with Difference
        var reconUnmatched = await reconciliationService.PerformReconciliationAsync(new CreateBankReconciliationDto
        {
            BankAccountId = bankAccount.Id,
            ReconciliationDate = new DateTime(2026, 3, 31),
            StatementEndingBalance = currentBookBalance + 500.00m // 500 SAR difference
        });

        Assert.IsNotNull(reconUnmatched);
        Assert.AreEqual(500.00m, reconUnmatched.Difference);
        Assert.IsFalse(reconUnmatched.IsReconciled);
        Assert.IsNull(reconUnmatched.ReconciledAt);
    }
}
