using AccountingERP.Application.DTOs.Commercial;
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
public class CommercialIntegrationTests
{
    private const string ConnectionString = "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private (ApplicationDbContext Context,
            CustomerService CustomerService,
            SupplierService SupplierService,
            SalesInvoiceService SalesInvoiceService,
            PurchaseInvoiceService PurchaseInvoiceService,
            PaymentService PaymentService,
            JournalEntryService JournalService,
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
        var customerService = new CustomerService(context, currentTenantService, NullLogger<CustomerService>.Instance);
        var supplierService = new SupplierService(context, currentTenantService, NullLogger<SupplierService>.Instance);
        var salesInvoiceService = new SalesInvoiceService(context, currentTenantService, journalService, NullLogger<SalesInvoiceService>.Instance);
        var purchaseInvoiceService = new PurchaseInvoiceService(context, currentTenantService, journalService, NullLogger<PurchaseInvoiceService>.Instance);
        var paymentService = new PaymentService(context, currentTenantService, journalService, NullLogger<PaymentService>.Instance);

        return (context, customerService, supplierService, salesInvoiceService, purchaseInvoiceService, paymentService, journalService, currentTenantService);
    }

    private async Task<(Company Company, Branch Branch, Warehouse Warehouse, Product Product, BankAccount BankAccount)> EnsureTestDataAsync(ApplicationDbContext context, int companyId)
    {
        var company = await context.Companies.FirstAsync(c => c.Id == companyId);
        var branch = await context.Branches.FirstAsync(b => b.CompanyId == companyId);
        var warehouse = await context.Warehouses.FirstAsync(w => w.CompanyId == companyId);
        var bankAccount = await context.BankAccounts.FirstAsync(ba => ba.CompanyId == companyId && !ba.IsCash);

        // Ensure product exists
        var product = await context.Products.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Code == "TEST-PRD01");
        if (product == null)
        {
            product = new Product
            {
                CompanyId = companyId,
                Code = "TEST-PRD01",
                NameAr = "كمبيوتر مكتبي ديل",
                NameEn = "Dell Desktop PC",
                ProductType = ProductType.Storable,
                UnitName = "Unit",
                CostPrice = 2500.00m,
                SellingPrice = 3500.00m,
                IsActive = true
            };
            await context.Products.AddAsync(product);
            await context.SaveChangesAsync();
        }

        return (company, branch, warehouse, product, bankAccount);
    }

    [TestMethod]
    public async Task Test_01_CreateAndRetrieveCustomer()
    {
        var (context, customerService, _, _, _, _, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var customer = await customerService.CreateAsync(new CreateCustomerDto
        {
            NameAr = "شركة الأمل للتوزيع والتقنية",
            NameEn = "Al-Amal Distribution & Tech",
            TaxNumber = "310999999900003",
            CreditLimit = 50000.00m,
            Phone = "+966551234567",
            Email = "info@alamal.com",
            Address = "الرياض - شارع العليا"
        });

        Assert.IsNotNull(customer);
        Assert.IsTrue(customer.Id > 0);
        Assert.IsTrue(customer.Code.StartsWith("CUST-"));
        Assert.AreEqual("شركة الأمل للتوزيع والتقنية", customer.NameAr);
        Assert.AreEqual("110301", customer.AccountCode); // Linked to Receivables control account

        var fetched = await customerService.GetByIdAsync(customer.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual(customer.NameAr, fetched.NameAr);
    }

    [TestMethod]
    public async Task Test_02_CreateAndRetrieveSupplier()
    {
        var (context, _, supplierService, _, _, _, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var supplier = await supplierService.CreateAsync(new CreateSupplierDto
        {
            NameAr = "مؤسسة التوريدات الإلكترونية الحديثة",
            NameEn = "Modern Electronic Supplies Est",
            TaxNumber = "310888888800003",
            Phone = "+966559876543",
            Email = "sales@modernsupplies.sa",
            Address = "جدة - حي البلد"
        });

        Assert.IsNotNull(supplier);
        Assert.IsTrue(supplier.Id > 0);
        Assert.IsTrue(supplier.Code.StartsWith("SUPP-"));
        Assert.AreEqual("مؤسسة التوريدات الإلكترونية الحديثة", supplier.NameAr);
        Assert.AreEqual("210101", supplier.AccountCode); // Linked to Payables control account

        var fetched = await supplierService.GetByIdAsync(supplier.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual(supplier.NameAr, fetched.NameAr);
    }

    [TestMethod]
    public async Task Test_03_CreateDraftSalesInvoice_And_PostToGL()
    {
        var (context, customerService, _, salesInvoiceService, _, _, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var (_, branch, warehouse, product, _) = await EnsureTestDataAsync(context, company.Id);

        // 1. Create Customer
        var customer = await customerService.CreateAsync(new CreateCustomerDto
        {
            NameAr = "عميل تجربة المبيعات المباشرة",
            NameEn = "Direct Sales Test Customer"
        });

        // 2. Create Draft Sales Invoice
        // 2 units * 3,000 = 6,000 SAR + 15% VAT (900 SAR) = 6,900 SAR
        var draftInvoice = await salesInvoiceService.CreateDraftAsync(new CreateSalesInvoiceDto
        {
            BranchId = branch.Id,
            CustomerId = customer.Id,
            InvoiceDate = new DateTime(2026, 2, 15),
            Items = new List<CreateInvoiceItemDto>
            {
                new()
                {
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    Quantity = 2,
                    UnitPrice = 3000.00m,
                    DiscountRate = 0m
                }
            }
        });

        Assert.IsNotNull(draftInvoice);
        Assert.AreEqual(InvoiceStatus.Draft, draftInvoice.Status);
        Assert.AreEqual(6000.00m, draftInvoice.SubTotal);
        Assert.AreEqual(900.00m, draftInvoice.TaxTotal);
        Assert.AreEqual(6900.00m, draftInvoice.GrandTotal);
        Assert.AreEqual(6900.00m, draftInvoice.RemainingAmount);
        Assert.AreEqual(0m, draftInvoice.PaidAmount);

        // 3. Post Invoice to GL
        var postedInvoice = await salesInvoiceService.PostToGLAsync(draftInvoice.Id);

        Assert.IsNotNull(postedInvoice);
        Assert.AreEqual(InvoiceStatus.PostedToGL, postedInvoice.Status);
        Assert.IsTrue(postedInvoice.InvoiceNumber.StartsWith("INV-2026-"));
        Assert.IsNotNull(postedInvoice.JournalEntryId);

        // 4. Verify balanced GL journal entry
        var entry = await context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == postedInvoice.JournalEntryId);

        Assert.IsNotNull(entry);
        Assert.AreEqual(JournalEntryStatus.Posted, entry.Status);
        Assert.AreEqual(6900.00m, entry.TotalDebit);
        Assert.AreEqual(6900.00m, entry.TotalCredit);
        Assert.AreEqual(3, entry.Lines.Count); // Customer (Debit 6900), Revenue (Credit 6000), VAT Output (Credit 900)
    }

    [TestMethod]
    public async Task Test_04_CreateDraftPurchaseInvoice_And_PostToGL()
    {
        var (context, _, supplierService, _, purchaseInvoiceService, _, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var (_, branch, warehouse, product, _) = await EnsureTestDataAsync(context, company.Id);

        // 1. Create Supplier
        var supplier = await supplierService.CreateAsync(new CreateSupplierDto
        {
            NameAr = "مورد تجربة المشتريات العامة",
            NameEn = "General Purchases Test Supplier"
        });

        // 2. Create Draft Purchase Invoice
        // 5 units * 2,000 = 10,000 SAR + 15% VAT (1,500 SAR) = 11,500 SAR
        var draftInvoice = await purchaseInvoiceService.CreateDraftAsync(new CreatePurchaseInvoiceDto
        {
            BranchId = branch.Id,
            SupplierId = supplier.Id,
            VendorBillNumber = "BILL-2026-999",
            InvoiceDate = new DateTime(2026, 2, 18),
            Items = new List<CreateInvoiceItemDto>
            {
                new()
                {
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    Quantity = 5,
                    UnitPrice = 2000.00m
                }
            }
        });

        Assert.IsNotNull(draftInvoice);
        Assert.AreEqual(InvoiceStatus.Draft, draftInvoice.Status);
        Assert.AreEqual(10000.00m, draftInvoice.SubTotal);
        Assert.AreEqual(1500.00m, draftInvoice.TaxTotal);
        Assert.AreEqual(11500.00m, draftInvoice.GrandTotal);

        // 3. Post to GL
        var postedInvoice = await purchaseInvoiceService.PostToGLAsync(draftInvoice.Id);

        Assert.IsNotNull(postedInvoice);
        Assert.AreEqual(InvoiceStatus.PostedToGL, postedInvoice.Status);
        Assert.IsTrue(postedInvoice.InvoiceNumber.StartsWith("PINV-2026-"));
        Assert.IsNotNull(postedInvoice.JournalEntryId);

        // 4. Verify balanced GL journal entry
        var entry = await context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == postedInvoice.JournalEntryId);

        Assert.IsNotNull(entry);
        Assert.AreEqual(JournalEntryStatus.Posted, entry.Status);
        Assert.AreEqual(11500.00m, entry.TotalDebit);
        Assert.AreEqual(11500.00m, entry.TotalCredit);
        Assert.AreEqual(3, entry.Lines.Count); // Expense/Inventory (Debit 10000), VAT Input (Debit 1500), Supplier (Credit 11500)
    }

    [TestMethod]
    public async Task Test_05_CreatePaymentReceipt_With_SalesInvoiceAllocation()
    {
        var (context, customerService, _, salesInvoiceService, _, paymentService, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var (_, branch, warehouse, product, bankAccount) = await EnsureTestDataAsync(context, company.Id);

        // 1. Create Customer & Sales Invoice
        var customer = await customerService.CreateAsync(new CreateCustomerDto
        {
            NameAr = "عميل التحصيل والسداد",
            NameEn = "Receipt Collection Customer"
        });

        var invoice = await salesInvoiceService.CreateDraftAsync(new CreateSalesInvoiceDto
        {
            BranchId = branch.Id,
            CustomerId = customer.Id,
            InvoiceDate = new DateTime(2026, 3, 1),
            Items = new List<CreateInvoiceItemDto>
            {
                new()
                {
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    Quantity = 1,
                    UnitPrice = 4000.00m
                }
            }
        });
        // Total = 4000 + 15% (600) = 4,600 SAR
        var postedInvoice = await salesInvoiceService.PostToGLAsync(invoice.Id);

        // 2. Create Payment Receipt with Full Allocation (4,600 SAR)
        var payment = await paymentService.CreateAndPostPaymentAsync(new CreatePaymentDto
        {
            BranchId = branch.Id,
            PaymentType = PaymentType.Receipt,
            PaymentMethod = PaymentMethod.BankTransfer,
            PaymentDate = new DateTime(2026, 3, 5),
            BankAccountId = bankAccount.Id,
            CustomerId = customer.Id,
            Amount = 4600.00m,
            Allocations = new List<CreatePaymentAllocationDto>
            {
                new()
                {
                    SalesInvoiceId = postedInvoice.Id,
                    AllocatedAmount = 4600.00m
                }
            }
        });

        Assert.IsNotNull(payment);
        Assert.IsTrue(payment.PaymentNumber.StartsWith("PAY-2026-"));
        Assert.AreEqual(4600.00m, payment.Amount);
        Assert.IsNotNull(payment.JournalEntryId);

        // 3. Verify Invoice status changed to Paid
        var updatedInvoice = await salesInvoiceService.GetByIdAsync(postedInvoice.Id);
        Assert.AreEqual(InvoiceStatus.Paid, updatedInvoice.Status);
        Assert.AreEqual(4600.00m, updatedInvoice.PaidAmount);
        Assert.AreEqual(0.00m, updatedInvoice.RemainingAmount);

        // 4. Verify balanced GL journal entry: Debit Bank, Credit Customer
        var entry = await context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == payment.JournalEntryId);

        Assert.IsNotNull(entry);
        Assert.AreEqual(4600.00m, entry.TotalDebit);
        Assert.AreEqual(4600.00m, entry.TotalCredit);
    }

    [TestMethod]
    public async Task Test_06_CreatePaymentToSupplier_With_PurchaseInvoiceAllocation()
    {
        var (context, _, supplierService, _, purchaseInvoiceService, paymentService, _, tenantService) = CreateServices();
        var company = await context.Companies.FirstAsync(c => c.Code == "COMP01");
        tenantService.SetTenant(company.Id, null);

        var (_, branch, warehouse, product, bankAccount) = await EnsureTestDataAsync(context, company.Id);

        // 1. Create Supplier & Purchase Invoice
        var supplier = await supplierService.CreateAsync(new CreateSupplierDto
        {
            NameAr = "مورد سداد الفواتير الجزئية",
            NameEn = "Partial Payment Supplier"
        });

        var invoice = await purchaseInvoiceService.CreateDraftAsync(new CreatePurchaseInvoiceDto
        {
            BranchId = branch.Id,
            SupplierId = supplier.Id,
            InvoiceDate = new DateTime(2026, 3, 2),
            Items = new List<CreateInvoiceItemDto>
            {
                new()
                {
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    Quantity = 2,
                    UnitPrice = 1000.00m
                }
            }
        });
        // Total = 2000 + 15% (300) = 2,300 SAR
        var postedInvoice = await purchaseInvoiceService.PostToGLAsync(invoice.Id);

        // 2. Partial Payment to Supplier (1,000 SAR out of 2,300 SAR)
        var payment = await paymentService.CreateAndPostPaymentAsync(new CreatePaymentDto
        {
            BranchId = branch.Id,
            PaymentType = PaymentType.Payment,
            PaymentMethod = PaymentMethod.BankTransfer,
            PaymentDate = new DateTime(2026, 3, 10),
            BankAccountId = bankAccount.Id,
            SupplierId = supplier.Id,
            Amount = 1000.00m,
            Allocations = new List<CreatePaymentAllocationDto>
            {
                new()
                {
                    PurchaseInvoiceId = postedInvoice.Id,
                    AllocatedAmount = 1000.00m
                }
            }
        });

        Assert.IsNotNull(payment);
        Assert.AreEqual(1000.00m, payment.Amount);

        // 3. Verify Invoice status changed to PartiallyPaid
        var updatedInvoice = await purchaseInvoiceService.GetByIdAsync(postedInvoice.Id);
        Assert.AreEqual(InvoiceStatus.PartiallyPaid, updatedInvoice.Status);
        Assert.AreEqual(1000.00m, updatedInvoice.PaidAmount);
        Assert.AreEqual(1300.00m, updatedInvoice.RemainingAmount);

        // 4. Verify balanced GL journal entry: Debit Supplier, Credit Bank
        var entry = await context.JournalEntries
            .Include(je => je.Lines)
            .FirstOrDefaultAsync(je => je.Id == payment.JournalEntryId);

        Assert.IsNotNull(entry);
        Assert.AreEqual(1000.00m, entry.TotalDebit);
        Assert.AreEqual(1000.00m, entry.TotalCredit);
    }
}
