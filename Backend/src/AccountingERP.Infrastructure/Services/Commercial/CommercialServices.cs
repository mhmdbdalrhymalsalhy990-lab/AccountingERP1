using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.DTOs.Commercial;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

// ==========================================
// 1. Customer Service Implementation
// ==========================================
public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<CustomerService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<List<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var customers = await _context.Customers
            .Include(c => c.Account)
            .Where(c => c.CompanyId == companyId && !c.IsDeleted)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);

        return customers.Select(MapToDto).ToList();
    }

    public async Task<CustomerDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var customer = await _context.Customers
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), id);

        return MapToDto(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        // 1. Determine AccountId
        int accountId;
        if (request.AccountId.HasValue && request.AccountId.Value > 0)
        {
            var acc = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId.Value && a.CompanyId == companyId, cancellationToken)
                ?? throw new NotFoundException(nameof(Account), request.AccountId.Value);

            if (!acc.IsPosting)
                throw new HeaderAccountPostingException(acc.AccountCode);

            accountId = acc.Id;
        }
        else
        {
            // Default Customer Control Account: 110301 (العملاء المحليون) or first account in 1103
            var defaultAcc = await _context.Accounts
                .Where(a => a.CompanyId == companyId && a.IsPosting && a.IsActive && a.AccountCode.StartsWith("1103"))
                .OrderBy(a => a.AccountCode)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleViolationException("لم يتم العثور على حساب أستاذ عام للعملاء (Accounts Receivable). يرجى تحديد حساب للعميل.");

            accountId = defaultAcc.Id;
        }

        // 2. Generate Customer Code if not supplied
        string code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            var count = await _context.Customers.CountAsync(c => c.CompanyId == companyId, cancellationToken);
            code = $"CUST-{(count + 1):D4}";
        }

        var customer = new Customer
        {
            CompanyId = companyId,
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            TaxNumber = request.TaxNumber?.Trim(),
            AccountId = accountId,
            CreditLimit = request.CreditLimit,
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = request.IsActive
        };

        await _context.Customers.AddAsync(customer, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer '{CustomerCode}' ({CustomerName}) created successfully in Company {CompanyId}.", customer.Code, customer.NameAr, companyId);

        return await GetByIdAsync(customer.Id, cancellationToken);
    }

    public async Task<CustomerDto> UpdateAsync(int id, UpdateCustomerDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), id);

        customer.NameAr = request.NameAr.Trim();
        customer.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim();
        customer.TaxNumber = request.TaxNumber?.Trim();
        customer.CreditLimit = request.CreditLimit;
        customer.Phone = request.Phone?.Trim();
        customer.Email = request.Email?.Trim();
        customer.Address = request.Address?.Trim();
        customer.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(customer.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), id);

        // Verify no invoices or payments attached
        var hasInvoices = await _context.SalesInvoices.AnyAsync(si => si.CustomerId == id, cancellationToken);
        var hasPayments = await _context.Payments.AnyAsync(p => p.CustomerId == id, cancellationToken);

        if (hasInvoices || hasPayments)
        {
            throw new BusinessRuleViolationException($"لا يمكن حذف العميل '{customer.NameAr}' لوجود فواتير أو سندات مالية مرتبطة به. يمكن إيقاف تنشيطه بدلاً من ذلك.");
        }

        customer.IsDeleted = true;
        customer.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerId} soft deleted.", id);
    }

    private static CustomerDto MapToDto(Customer c)
    {
        return new CustomerDto
        {
            Id = c.Id,
            CompanyId = c.CompanyId,
            Code = c.Code,
            NameAr = c.NameAr,
            NameEn = c.NameEn,
            TaxNumber = c.TaxNumber,
            AccountId = c.AccountId,
            AccountCode = c.Account?.AccountCode,
            AccountName = c.Account?.NameAr,
            CreditLimit = c.CreditLimit,
            Phone = c.Phone,
            Email = c.Email,
            Address = c.Address,
            IsActive = c.IsActive,
            CurrentBalance = 0m
        };
    }
}

// ==========================================
// 2. Supplier Service Implementation
// ==========================================
public class SupplierService : ISupplierService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<SupplierService> _logger;

    public SupplierService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<SupplierService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<List<SupplierDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var suppliers = await _context.Suppliers
            .Include(s => s.Account)
            .Where(s => s.CompanyId == companyId && !s.IsDeleted)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        return suppliers.Select(MapToDto).ToList();
    }

    public async Task<SupplierDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var supplier = await _context.Suppliers
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), id);

        return MapToDto(supplier);
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        // 1. Determine AccountId
        int accountId;
        if (request.AccountId.HasValue && request.AccountId.Value > 0)
        {
            var acc = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId.Value && a.CompanyId == companyId, cancellationToken)
                ?? throw new NotFoundException(nameof(Account), request.AccountId.Value);

            if (!acc.IsPosting)
                throw new HeaderAccountPostingException(acc.AccountCode);

            accountId = acc.Id;
        }
        else
        {
            // Default Supplier Control Account: 210101 (الموردون المحليون) or first account in 2101
            var defaultAcc = await _context.Accounts
                .Where(a => a.CompanyId == companyId && a.IsPosting && a.IsActive && a.AccountCode.StartsWith("2101"))
                .OrderBy(a => a.AccountCode)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleViolationException("لم يتم العثور على حساب أستاذ عام للموردين (Accounts Payable). يرجى تحديد حساب للمورد.");

            accountId = defaultAcc.Id;
        }

        // 2. Generate Supplier Code if not supplied
        string code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            var count = await _context.Suppliers.CountAsync(s => s.CompanyId == companyId, cancellationToken);
            code = $"SUPP-{(count + 1):D4}";
        }

        var supplier = new Supplier
        {
            CompanyId = companyId,
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            TaxNumber = request.TaxNumber?.Trim(),
            AccountId = accountId,
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = request.IsActive
        };

        await _context.Suppliers.AddAsync(supplier, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Supplier '{SupplierCode}' ({SupplierName}) created successfully in Company {CompanyId}.", supplier.Code, supplier.NameAr, companyId);

        return await GetByIdAsync(supplier.Id, cancellationToken);
    }

    public async Task<SupplierDto> UpdateAsync(int id, UpdateSupplierDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), id);

        supplier.NameAr = request.NameAr.Trim();
        supplier.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim();
        supplier.TaxNumber = request.TaxNumber?.Trim();
        supplier.Phone = request.Phone?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.Address = request.Address?.Trim();
        supplier.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(supplier.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), id);

        var hasInvoices = await _context.PurchaseInvoices.AnyAsync(pi => pi.SupplierId == id, cancellationToken);
        var hasPayments = await _context.Payments.AnyAsync(p => p.SupplierId == id, cancellationToken);

        if (hasInvoices || hasPayments)
        {
            throw new BusinessRuleViolationException($"لا يمكن حذف المورد '{supplier.NameAr}' لوجود فواتير أو سندات مالية مرتبطة به. يمكن إيقاف تنشيطه بدلاً من ذلك.");
        }

        supplier.IsDeleted = true;
        supplier.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Supplier {SupplierId} soft deleted.", id);
    }

    private static SupplierDto MapToDto(Supplier s)
    {
        return new SupplierDto
        {
            Id = s.Id,
            CompanyId = s.CompanyId,
            Code = s.Code,
            NameAr = s.NameAr,
            NameEn = s.NameEn,
            TaxNumber = s.TaxNumber,
            AccountId = s.AccountId,
            AccountCode = s.Account?.AccountCode,
            AccountName = s.Account?.NameAr,
            Phone = s.Phone,
            Email = s.Email,
            Address = s.Address,
            IsActive = s.IsActive,
            CurrentBalance = 0m
        };
    }
}

// ==========================================
// 3. Sales Invoice Service Implementation
// ==========================================
public class SalesInvoiceService : ISalesInvoiceService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IJournalEntryService _journalEntryService;
    private readonly ILogger<SalesInvoiceService> _logger;

    public SalesInvoiceService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IJournalEntryService journalEntryService,
        ILogger<SalesInvoiceService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _journalEntryService = journalEntryService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<PagedResult<SalesInvoiceDto>> GetPagedAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.SalesInvoices
            .Include(si => si.Branch)
            .Include(si => si.Customer)
            .Include(si => si.Currency)
            .Include(si => si.JournalEntry)
            .Include(si => si.Items)
                .ThenInclude(i => i.Product)
            .Include(si => si.Items)
                .ThenInclude(i => i.Warehouse)
            .Where(si => si.CompanyId == companyId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(si => si.InvoiceDate)
            .ThenByDescending(si => si.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<SalesInvoiceDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<SalesInvoiceDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var invoice = await _context.SalesInvoices
            .Include(si => si.Branch)
            .Include(si => si.Customer)
            .Include(si => si.Currency)
            .Include(si => si.JournalEntry)
            .Include(si => si.Items)
                .ThenInclude(i => i.Product)
            .Include(si => si.Items)
                .ThenInclude(i => i.Warehouse)
            .FirstOrDefaultAsync(si => si.Id == id && si.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesInvoice), id);

        return MapToDto(invoice);
    }

    public async Task<SalesInvoiceDto> CreateDraftAsync(CreateSalesInvoiceDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        // 1. Branch
        var branchId = request.BranchId ?? _currentTenantService.BranchId
            ?? (await _context.Branches.Where(b => b.CompanyId == companyId).Select(b => b.Id).FirstOrDefaultAsync(cancellationToken));

        if (branchId == 0)
            throw new BusinessRuleViolationException("لا يوجد فرع محدد للفاتورة.");

        // 2. Customer
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.CompanyId == companyId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        if (!customer.IsActive)
            throw new BusinessRuleViolationException($"العميل '{customer.NameAr}' غير نشط ولا يمكن إصدار فواتير له.");

        // 3. Currency & Exchange Rate
        int currencyId = request.CurrencyId ?? 0;
        decimal exchangeRate = request.ExchangeRate ?? 1.000000m;

        if (currencyId == 0)
        {
            var company = await _context.Companies.FirstAsync(c => c.Id == companyId, cancellationToken);
            currencyId = company.BaseCurrencyId;
        }

        if (exchangeRate <= 0) exchangeRate = 1.000000m;

        if (request.Items == null || !request.Items.Any())
            throw new BusinessRuleViolationException("لا يمكن حفظ فاتورة مبيعات دون بنود (Invoice must have at least one item).");

        // 4. Default VAT TaxType lookup
        var defaultTaxType = await _context.TaxTypes
            .Include(t => t.TaxRates)
            .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.IsActive && t.Category == TaxCategory.Sales, cancellationToken);

        var invoice = new SalesInvoice
        {
            CompanyId = companyId,
            BranchId = branchId,
            InvoiceNumber = $"DRAFT-INV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate ?? request.InvoiceDate.AddDays(30),
            CustomerId = customer.Id,
            CurrencyId = currencyId,
            ExchangeRate = exchangeRate,
            Status = InvoiceStatus.Draft
        };

        decimal subTotal = 0m;
        decimal taxTotal = 0m;
        decimal linesDiscount = 0m;

        foreach (var itemDto in request.Items)
        {
            if (itemDto.Quantity <= 0)
                throw new BusinessRuleViolationException("يجب أن تكون كمية الصنف أكبر من صفر.");

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == itemDto.ProductId && p.CompanyId == companyId, cancellationToken)
                ?? throw new NotFoundException(nameof(Product), itemDto.ProductId);

            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == itemDto.WarehouseId && w.CompanyId == companyId, cancellationToken)
                ?? throw new NotFoundException(nameof(Warehouse), itemDto.WarehouseId);

            decimal lineRaw = Math.Round(itemDto.Quantity * itemDto.UnitPrice, 4);
            decimal discountAmt = Math.Round(lineRaw * (itemDto.DiscountRate / 100m), 4);
            decimal lineAfterDiscount = lineRaw - discountAmt;

            // Determine tax
            decimal taxRate = 0m;
            int? taxTypeId = itemDto.TaxTypeId;

            if (taxTypeId.HasValue && taxTypeId.Value > 0)
            {
                var tax = await _context.TaxTypes.Include(t => t.TaxRates)
                    .FirstOrDefaultAsync(t => t.Id == taxTypeId.Value && t.CompanyId == companyId, cancellationToken);
                taxRate = tax?.TaxRates.OrderByDescending(tr => tr.CreatedAt).Select(tr => tr.Rate).FirstOrDefault() ?? 0m;
            }
            else if (defaultTaxType != null)
            {
                taxTypeId = defaultTaxType.Id;
                taxRate = defaultTaxType.TaxRates.OrderByDescending(tr => tr.CreatedAt).Select(tr => tr.Rate).FirstOrDefault();
            }

            decimal lineTax = Math.Round(lineAfterDiscount * (taxRate / 100m), 4);
            decimal lineTotal = lineAfterDiscount + lineTax;

            subTotal += lineRaw;
            linesDiscount += discountAmt;
            taxTotal += lineTax;

            invoice.Items.Add(new SalesInvoiceItem
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                DiscountRate = itemDto.DiscountRate,
                TaxTypeId = taxTypeId,
                TaxRate = taxRate,
                TaxAmount = lineTax,
                LineTotal = lineTotal
            });
        }

        invoice.SubTotal = subTotal;
        invoice.DiscountTotal = linesDiscount + request.DiscountTotal;
        invoice.TaxTotal = taxTotal;
        invoice.GrandTotal = (subTotal - invoice.DiscountTotal) + taxTotal;
        invoice.RemainingAmount = invoice.GrandTotal;
        invoice.PaidAmount = 0m;

        await _context.SalesInvoices.AddAsync(invoice, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Draft Sales Invoice {InvoiceId} created successfully.", invoice.Id);
        return await GetByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<SalesInvoiceDto> PostToGLAsync(long id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var invoice = await _context.SalesInvoices
            .Include(si => si.Customer)
            .Include(si => si.Items)
            .FirstOrDefaultAsync(si => si.Id == id && si.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesInvoice), id);

        if (invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.Approved)
            throw new BusinessRuleViolationException($"لا يمكن ترحيل الفاتورة لأن حالتها الحالية هي '{invoice.Status}'.");

        var customer = invoice.Customer
            ?? await _context.Customers.FirstAsync(c => c.Id == invoice.CustomerId, cancellationToken);

        // 1. Get Fiscal Period
        var fiscalPeriod = await _context.FiscalPeriods
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.FiscalYear != null && p.FiscalYear.CompanyId == companyId &&
                                      p.StartDate <= invoice.InvoiceDate &&
                                      p.EndDate >= invoice.InvoiceDate &&
                                      p.Status == FiscalPeriodStatus.Open, cancellationToken)
            ?? throw new ClosedFiscalPeriodException($"لا توجد فترة مالية مفتوحة لتاريخ الفاتورة {invoice.InvoiceDate:yyyy-MM-dd}.");

        // 2. Generate Gapless Sequence Number (INV-YYYY-XXXXXX)
        var sequence = await _context.DocumentSequences
            .FirstOrDefaultAsync(ds => ds.CompanyId == companyId && ds.FiscalYearId == fiscalPeriod.FiscalYearId && ds.DocumentType == 2, cancellationToken);

        if (sequence == null)
        {
            sequence = new DocumentSequence
            {
                CompanyId = companyId,
                FiscalYearId = fiscalPeriod.FiscalYearId,
                DocumentType = 2, // SalesInvoice
                Prefix = "INV",
                LastNumber = 0
            };
            await _context.DocumentSequences.AddAsync(sequence, cancellationToken);
        }

        sequence.LastNumber += 1;
        var formattedNumber = $"{sequence.Prefix}-{fiscalPeriod.FiscalYear!.StartDate.Year}-{sequence.LastNumber:D6}";
        invoice.InvoiceNumber = formattedNumber;

        // 3. Financial Accounts from CompanySettings
        var settings = await _context.CompanySettings
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken)
            ?? throw new BusinessRuleViolationException("إعدادات ربط الحسابات للشركة غير مهيأة (Company Settings missing).");

        int salesRevenueAccountId = settings.SalesRevenueAccountId
            ?? (await _context.Accounts.Where(a => a.CompanyId == companyId && a.AccountCode == "410101").Select(a => a.Id).FirstOrDefaultAsync(cancellationToken));

        int vatAccountId = settings.DefaultVatAccountId
            ?? (await _context.Accounts.Where(a => a.CompanyId == companyId && a.AccountCode == "210201").Select(a => a.Id).FirstOrDefaultAsync(cancellationToken));

        if (salesRevenueAccountId == 0)
            throw new BusinessRuleViolationException("لم يتم تحديد حساب إيرادات المبيعات في إعدادات الشركة (Sales Revenue Account missing).");

        // 4. Create and Post Journal Entry
        // Double-entry accounting:
        // Debit: Customer Account (GrandTotal)
        // Credit: Sales Revenue (Net Total = SubTotal - Discount)
        // Credit: VAT Output (TaxTotal)
        var lines = new List<CreateJournalEntryLineDto>
        {
            new()
            {
                AccountId = customer.AccountId,
                Description = $"استحقاق فاتورة مبيعات {invoice.InvoiceNumber} - العميل: {customer.NameAr}",
                Debit = invoice.GrandTotal,
                Credit = 0m
            },
            new()
            {
                AccountId = salesRevenueAccountId,
                Description = $"إيراد مبيعات فاتورة {invoice.InvoiceNumber}",
                Debit = 0m,
                Credit = invoice.GrandTotal - invoice.TaxTotal
            }
        };

        if (invoice.TaxTotal > 0)
        {
            lines.Add(new CreateJournalEntryLineDto
            {
                AccountId = vatAccountId,
                Description = $"ضريبة القيمة المضافة المحصلة - فاتورة {invoice.InvoiceNumber}",
                Debit = 0m,
                Credit = invoice.TaxTotal
            });
        }

        var entryDto = new CreateJournalEntryDto
        {
            EntryDate = invoice.InvoiceDate,
            ReferenceNumber = invoice.InvoiceNumber,
            Description = $"قيد إثبات فاتورة مبيعات رقم {invoice.InvoiceNumber} - العميل: {customer.NameAr}",
            BranchId = invoice.BranchId,
            EntryType = JournalEntryType.Manual,
            SourceDocumentType = SourceDocumentType.SalesInvoice,
            SourceDocumentId = invoice.InvoiceNumber,
            Lines = lines
        };

        var postedEntry = await _journalEntryService.CreateAndPostAsync(entryDto, cancellationToken);

        invoice.JournalEntryId = postedEntry.Id;
        invoice.Status = InvoiceStatus.PostedToGL;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sales Invoice {InvoiceNumber} posted to GL successfully with Journal Entry {EntryNumber}.", invoice.InvoiceNumber, postedEntry.EntryNumber);

        return await GetByIdAsync(invoice.Id, cancellationToken);
    }

    private static SalesInvoiceDto MapToDto(SalesInvoice si)
    {
        return new SalesInvoiceDto
        {
            Id = si.Id,
            CompanyId = si.CompanyId,
            BranchId = si.BranchId,
            BranchName = si.Branch?.NameAr ?? string.Empty,
            InvoiceNumber = si.InvoiceNumber,
            InvoiceDate = si.InvoiceDate,
            DueDate = si.DueDate,
            CustomerId = si.CustomerId,
            CustomerNameAr = si.Customer?.NameAr ?? string.Empty,
            CustomerNameEn = si.Customer?.NameEn ?? string.Empty,
            CurrencyId = si.CurrencyId,
            CurrencyCode = si.Currency?.Code ?? string.Empty,
            ExchangeRate = si.ExchangeRate,
            SubTotal = si.SubTotal,
            TaxTotal = si.TaxTotal,
            DiscountTotal = si.DiscountTotal,
            GrandTotal = si.GrandTotal,
            PaidAmount = si.PaidAmount,
            RemainingAmount = si.RemainingAmount,
            Status = si.Status,
            JournalEntryId = si.JournalEntryId,
            JournalEntryNumber = si.JournalEntry?.EntryNumber,
            Items = si.Items.Select(i => new SalesInvoiceItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product?.NameAr ?? string.Empty,
                WarehouseId = i.WarehouseId,
                WarehouseName = i.Warehouse?.NameAr ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountRate = i.DiscountRate,
                TaxTypeId = i.TaxTypeId,
                TaxRate = i.TaxRate,
                TaxAmount = i.TaxAmount,
                LineTotal = i.LineTotal
            }).ToList()
        };
    }
}

// ==========================================
// 4. Purchase Invoice Service Implementation
// ==========================================
public class PurchaseInvoiceService : IPurchaseInvoiceService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IJournalEntryService _journalEntryService;
    private readonly ILogger<PurchaseInvoiceService> _logger;

    public PurchaseInvoiceService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IJournalEntryService journalEntryService,
        ILogger<PurchaseInvoiceService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _journalEntryService = journalEntryService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<PagedResult<PurchaseInvoiceDto>> GetPagedAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.PurchaseInvoices
            .Include(pi => pi.Branch)
            .Include(pi => pi.Supplier)
            .Include(pi => pi.Currency)
            .Include(pi => pi.JournalEntry)
            .Include(pi => pi.Items)
                .ThenInclude(i => i.Product)
            .Include(pi => pi.Items)
                .ThenInclude(i => i.Warehouse)
            .Where(pi => pi.CompanyId == companyId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(pi => pi.InvoiceDate)
            .ThenByDescending(pi => pi.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<PurchaseInvoiceDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<PurchaseInvoiceDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var invoice = await _context.PurchaseInvoices
            .Include(pi => pi.Branch)
            .Include(pi => pi.Supplier)
            .Include(pi => pi.Currency)
            .Include(pi => pi.JournalEntry)
            .Include(pi => pi.Items)
                .ThenInclude(i => i.Product)
            .Include(pi => pi.Items)
                .ThenInclude(i => i.Warehouse)
            .FirstOrDefaultAsync(pi => pi.Id == id && pi.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), id);

        return MapToDto(invoice);
    }

    public async Task<PurchaseInvoiceDto> CreateDraftAsync(CreatePurchaseInvoiceDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var branchId = request.BranchId ?? _currentTenantService.BranchId
            ?? (await _context.Branches.Where(b => b.CompanyId == companyId).Select(b => b.Id).FirstOrDefaultAsync(cancellationToken));

        if (branchId == 0)
            throw new BusinessRuleViolationException("لا يوجد فرع محدد لفاتورة الشراء.");

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId && s.CompanyId == companyId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.SupplierId);

        if (!supplier.IsActive)
            throw new BusinessRuleViolationException($"المورد '{supplier.NameAr}' غير نشط ولا يمكن تسجيل فواتير له.");

        int currencyId = request.CurrencyId ?? 0;
        decimal exchangeRate = request.ExchangeRate ?? 1.000000m;

        if (currencyId == 0)
        {
            var company = await _context.Companies.FirstAsync(c => c.Id == companyId, cancellationToken);
            currencyId = company.BaseCurrencyId;
        }

        if (exchangeRate <= 0) exchangeRate = 1.000000m;

        if (request.Items == null || !request.Items.Any())
            throw new BusinessRuleViolationException("لا يمكن حفظ فاتورة شراء دون بنود (Purchase invoice must have at least one item).");

        var defaultTaxType = await _context.TaxTypes
            .Include(t => t.TaxRates)
            .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.IsActive, cancellationToken);

        var invoice = new PurchaseInvoice
        {
            CompanyId = companyId,
            BranchId = branchId,
            InvoiceNumber = $"DRAFT-PINV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            VendorBillNumber = request.VendorBillNumber?.Trim(),
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate ?? request.InvoiceDate.AddDays(30),
            SupplierId = supplier.Id,
            CurrencyId = currencyId,
            ExchangeRate = exchangeRate,
            Status = InvoiceStatus.Draft
        };

        decimal subTotal = 0m;
        decimal taxTotal = 0m;

        foreach (var itemDto in request.Items)
        {
            if (itemDto.Quantity <= 0)
                throw new BusinessRuleViolationException("يجب أن تكون كمية الصنف أكبر من صفر.");

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == itemDto.ProductId && p.CompanyId == companyId, cancellationToken)
                ?? throw new NotFoundException(nameof(Product), itemDto.ProductId);

            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == itemDto.WarehouseId && w.CompanyId == companyId, cancellationToken)
                ?? throw new NotFoundException(nameof(Warehouse), itemDto.WarehouseId);

            decimal lineRaw = Math.Round(itemDto.Quantity * itemDto.UnitPrice, 4);

            decimal taxRate = 0m;
            int? taxTypeId = itemDto.TaxTypeId;

            if (taxTypeId.HasValue && taxTypeId.Value > 0)
            {
                var tax = await _context.TaxTypes.Include(t => t.TaxRates)
                    .FirstOrDefaultAsync(t => t.Id == taxTypeId.Value && t.CompanyId == companyId, cancellationToken);
                taxRate = tax?.TaxRates.OrderByDescending(tr => tr.CreatedAt).Select(tr => tr.Rate).FirstOrDefault() ?? 0m;
            }
            else if (defaultTaxType != null)
            {
                taxTypeId = defaultTaxType.Id;
                taxRate = defaultTaxType.TaxRates.OrderByDescending(tr => tr.CreatedAt).Select(tr => tr.Rate).FirstOrDefault();
            }

            decimal lineTax = Math.Round(lineRaw * (taxRate / 100m), 4);
            decimal lineTotal = lineRaw + lineTax;

            subTotal += lineRaw;
            taxTotal += lineTax;

            invoice.Items.Add(new PurchaseInvoiceItem
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                TaxTypeId = taxTypeId,
                TaxRate = taxRate,
                TaxAmount = lineTax,
                LineTotal = lineTotal
            });
        }

        invoice.SubTotal = subTotal;
        invoice.TaxTotal = taxTotal;
        invoice.GrandTotal = subTotal + taxTotal;
        invoice.RemainingAmount = invoice.GrandTotal;
        invoice.PaidAmount = 0m;

        await _context.PurchaseInvoices.AddAsync(invoice, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Draft Purchase Invoice {InvoiceId} created successfully.", invoice.Id);
        return await GetByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<PurchaseInvoiceDto> PostToGLAsync(long id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var invoice = await _context.PurchaseInvoices
            .Include(pi => pi.Supplier)
            .Include(pi => pi.Items)
            .FirstOrDefaultAsync(pi => pi.Id == id && pi.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), id);

        if (invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.Approved)
            throw new BusinessRuleViolationException($"لا يمكن ترحيل الفاتورة لأن حالتها الحالية هي '{invoice.Status}'.");

        var supplier = invoice.Supplier
            ?? await _context.Suppliers.FirstAsync(s => s.Id == invoice.SupplierId, cancellationToken);

        // 1. Get Fiscal Period
        var fiscalPeriod = await _context.FiscalPeriods
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.FiscalYear != null && p.FiscalYear.CompanyId == companyId &&
                                      p.StartDate <= invoice.InvoiceDate &&
                                      p.EndDate >= invoice.InvoiceDate &&
                                      p.Status == FiscalPeriodStatus.Open, cancellationToken)
            ?? throw new ClosedFiscalPeriodException($"لا توجد فترة مالية مفتوحة لتاريخ الفاتورة {invoice.InvoiceDate:yyyy-MM-dd}.");

        // 2. Generate Gapless Sequence Number (PINV-YYYY-XXXXXX)
        var sequence = await _context.DocumentSequences
            .FirstOrDefaultAsync(ds => ds.CompanyId == companyId && ds.FiscalYearId == fiscalPeriod.FiscalYearId && ds.DocumentType == 3, cancellationToken);

        if (sequence == null)
        {
            sequence = new DocumentSequence
            {
                CompanyId = companyId,
                FiscalYearId = fiscalPeriod.FiscalYearId,
                DocumentType = 3, // PurchaseInvoice
                Prefix = "PINV",
                LastNumber = 0
            };
            await _context.DocumentSequences.AddAsync(sequence, cancellationToken);
        }

        sequence.LastNumber += 1;
        var formattedNumber = $"{sequence.Prefix}-{fiscalPeriod.FiscalYear!.StartDate.Year}-{sequence.LastNumber:D6}";
        invoice.InvoiceNumber = formattedNumber;

        // 3. Financial Accounts from CompanySettings
        var settings = await _context.CompanySettings
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken)
            ?? throw new BusinessRuleViolationException("إعدادات ربط الحسابات للشركة غير مهيأة.");

        int purchaseExpenseAccountId = settings.PurchaseExpenseAccountId
            ?? (await _context.Accounts.Where(a => a.CompanyId == companyId && a.AccountCode == "510101").Select(a => a.Id).FirstOrDefaultAsync(cancellationToken));

        // Input VAT account (210202) or default VAT
        int vatAccountId = (await _context.Accounts.Where(a => a.CompanyId == companyId && a.AccountCode == "210202").Select(a => a.Id).FirstOrDefaultAsync(cancellationToken));
        if (vatAccountId == 0)
        {
            vatAccountId = settings.DefaultVatAccountId ?? (await _context.Accounts.Where(a => a.CompanyId == companyId && a.AccountCode == "210201").Select(a => a.Id).FirstOrDefaultAsync(cancellationToken));
        }

        // 4. Create and Post Journal Entry
        // Double-entry accounting:
        // Debit: Purchase Expense / Inventory (SubTotal)
        // Debit: Input VAT (TaxTotal)
        // Credit: Supplier Account (GrandTotal)
        var lines = new List<CreateJournalEntryLineDto>
        {
            new()
            {
                AccountId = purchaseExpenseAccountId,
                Description = $"مشتريات فاتورة رقم {invoice.InvoiceNumber} - المورد: {supplier.NameAr}",
                Debit = invoice.SubTotal,
                Credit = 0m
            }
        };

        if (invoice.TaxTotal > 0)
        {
            lines.Add(new CreateJournalEntryLineDto
            {
                AccountId = vatAccountId,
                Description = $"ضريبة القيمة المضافة المدفوعة (مدخلات) - فاتورة {invoice.InvoiceNumber}",
                Debit = invoice.TaxTotal,
                Credit = 0m
            });
        }

        lines.Add(new CreateJournalEntryLineDto
        {
            AccountId = supplier.AccountId,
            Description = $"استحقاق فاتورة مشتريات رقم {invoice.InvoiceNumber} - المورد: {supplier.NameAr}",
            Debit = 0m,
            Credit = invoice.GrandTotal
        });

        var entryDto = new CreateJournalEntryDto
        {
            EntryDate = invoice.InvoiceDate,
            ReferenceNumber = invoice.InvoiceNumber,
            Description = $"قيد إثبات فاتورة مشتريات رقم {invoice.InvoiceNumber} - المورد: {supplier.NameAr}",
            BranchId = invoice.BranchId,
            EntryType = JournalEntryType.Manual,
            SourceDocumentType = SourceDocumentType.PurchaseInvoice,
            SourceDocumentId = invoice.InvoiceNumber,
            Lines = lines
        };

        var postedEntry = await _journalEntryService.CreateAndPostAsync(entryDto, cancellationToken);

        invoice.JournalEntryId = postedEntry.Id;
        invoice.Status = InvoiceStatus.PostedToGL;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Purchase Invoice {InvoiceNumber} posted to GL successfully with Journal Entry {EntryNumber}.", invoice.InvoiceNumber, postedEntry.EntryNumber);

        return await GetByIdAsync(invoice.Id, cancellationToken);
    }

    private static PurchaseInvoiceDto MapToDto(PurchaseInvoice pi)
    {
        return new PurchaseInvoiceDto
        {
            Id = pi.Id,
            CompanyId = pi.CompanyId,
            BranchId = pi.BranchId,
            BranchName = pi.Branch?.NameAr ?? string.Empty,
            InvoiceNumber = pi.InvoiceNumber,
            VendorBillNumber = pi.VendorBillNumber,
            InvoiceDate = pi.InvoiceDate,
            DueDate = pi.DueDate,
            SupplierId = pi.SupplierId,
            SupplierNameAr = pi.Supplier?.NameAr ?? string.Empty,
            SupplierNameEn = pi.Supplier?.NameEn ?? string.Empty,
            CurrencyId = pi.CurrencyId,
            CurrencyCode = pi.Currency?.Code ?? string.Empty,
            ExchangeRate = pi.ExchangeRate,
            SubTotal = pi.SubTotal,
            TaxTotal = pi.TaxTotal,
            GrandTotal = pi.GrandTotal,
            PaidAmount = pi.PaidAmount,
            RemainingAmount = pi.RemainingAmount,
            Status = pi.Status,
            JournalEntryId = pi.JournalEntryId,
            JournalEntryNumber = pi.JournalEntry?.EntryNumber,
            Items = pi.Items.Select(i => new PurchaseInvoiceItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product?.NameAr ?? string.Empty,
                WarehouseId = i.WarehouseId,
                WarehouseName = i.Warehouse?.NameAr ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TaxTypeId = i.TaxTypeId,
                TaxRate = i.TaxRate,
                TaxAmount = i.TaxAmount,
                LineTotal = i.LineTotal
            }).ToList()
        };
    }
}

// ==========================================
// 5. Payment & Allocation Service Implementation
// ==========================================
public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IJournalEntryService _journalEntryService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IJournalEntryService journalEntryService,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _journalEntryService = journalEntryService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<PagedResult<PaymentDto>> GetPagedAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var query = _context.Payments
            .Include(p => p.Branch)
            .Include(p => p.BankAccount)
            .Include(p => p.Customer)
            .Include(p => p.Supplier)
            .Include(p => p.Currency)
            .Include(p => p.JournalEntry)
            .Include(p => p.Allocations)
                .ThenInclude(a => a.SalesInvoice)
            .Include(p => p.Allocations)
                .ThenInclude(a => a.PurchaseInvoice)
            .Where(p => p.CompanyId == companyId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<PaymentDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<PaymentDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var payment = await _context.Payments
            .Include(p => p.Branch)
            .Include(p => p.BankAccount)
            .Include(p => p.Customer)
            .Include(p => p.Supplier)
            .Include(p => p.Currency)
            .Include(p => p.JournalEntry)
            .Include(p => p.Allocations)
                .ThenInclude(a => a.SalesInvoice)
            .Include(p => p.Allocations)
                .ThenInclude(a => a.PurchaseInvoice)
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Payment), id);

        return MapToDto(payment);
    }

    public async Task<PaymentDto> CreateAndPostPaymentAsync(CreatePaymentDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        if (request.Amount <= 0)
            throw new BusinessRuleViolationException("مبلغ السند يجب أن يكون أكبر من صفر.");

        var branchId = request.BranchId ?? _currentTenantService.BranchId
            ?? (await _context.Branches.Where(b => b.CompanyId == companyId).Select(b => b.Id).FirstOrDefaultAsync(cancellationToken));

        if (branchId == 0)
            throw new BusinessRuleViolationException("لا يوجد فرع محدد للسند.");

        // 1. Bank/Cash Account
        var bankAccount = await _context.BankAccounts
            .Include(b => b.Account)
            .FirstOrDefaultAsync(b => b.Id == request.BankAccountId && b.CompanyId == companyId && b.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(BankAccount), request.BankAccountId);

        // 2. Currency
        int currencyId = request.CurrencyId ?? bankAccount.CurrencyId;
        decimal exchangeRate = request.ExchangeRate ?? 1.000000m;
        if (exchangeRate <= 0) exchangeRate = 1.000000m;
        decimal baseAmount = Math.Round(request.Amount * exchangeRate, 4);

        // 3. Fiscal Period
        var fiscalPeriod = await _context.FiscalPeriods
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.FiscalYear != null && p.FiscalYear.CompanyId == companyId &&
                                      p.StartDate <= request.PaymentDate &&
                                      p.EndDate >= request.PaymentDate &&
                                      p.Status == FiscalPeriodStatus.Open, cancellationToken)
            ?? throw new ClosedFiscalPeriodException($"لا توجد فترة مالية مفتوحة لتاريخ السند {request.PaymentDate:yyyy-MM-dd}.");

        // 4. Sequential Numbering (PAY-YYYY-XXXXXX)
        var sequence = await _context.DocumentSequences
            .FirstOrDefaultAsync(ds => ds.CompanyId == companyId && ds.FiscalYearId == fiscalPeriod.FiscalYearId && ds.DocumentType == 4, cancellationToken);

        if (sequence == null)
        {
            sequence = new DocumentSequence
            {
                CompanyId = companyId,
                FiscalYearId = fiscalPeriod.FiscalYearId,
                DocumentType = 4, // Payment
                Prefix = "PAY",
                LastNumber = 0
            };
            await _context.DocumentSequences.AddAsync(sequence, cancellationToken);
        }

        sequence.LastNumber += 1;
        var paymentNumber = $"{sequence.Prefix}-{fiscalPeriod.FiscalYear!.StartDate.Year}-{sequence.LastNumber:D6}";

        var payment = new Payment
        {
            CompanyId = companyId,
            BranchId = branchId,
            PaymentNumber = paymentNumber,
            PaymentType = request.PaymentType,
            PaymentMethod = request.PaymentMethod,
            PaymentDate = request.PaymentDate,
            BankAccountId = bankAccount.Id,
            Amount = request.Amount,
            CurrencyId = currencyId,
            ExchangeRate = exchangeRate,
            BaseAmount = baseAmount,
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            ChequeNumber = request.ChequeNumber?.Trim(),
            ChequeDueDate = request.ChequeDueDate,
            Status = InvoiceStatus.PostedToGL
        };

        // 5. Accounting and Allocation Engine
        CreateJournalEntryDto journalEntryDto;

        if (request.PaymentType == PaymentType.Receipt)
        {
            // Receipt from Customer (سند قبض عميل)
            if (!request.CustomerId.HasValue || request.CustomerId.Value == 0)
                throw new BusinessRuleViolationException("يجب تحديد العميل لسند القبض.");

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value && c.CompanyId == companyId && !c.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(Customer), request.CustomerId.Value);

            payment.CustomerId = customer.Id;

            // Balanced GL Entry:
            // Debit: Bank / Cash Account (bankAccount.AccountId)
            // Credit: Customer Account (customer.AccountId)
            journalEntryDto = new CreateJournalEntryDto
            {
                EntryDate = request.PaymentDate,
                ReferenceNumber = paymentNumber,
                Description = $"سند قبض رقم {paymentNumber} من العميل: {customer.NameAr}",
                BranchId = branchId,
                EntryType = JournalEntryType.Manual,
                SourceDocumentType = SourceDocumentType.CustomerReceipt,
                SourceDocumentId = paymentNumber,
                Lines = new List<CreateJournalEntryLineDto>
                {
                    new()
                    {
                        AccountId = bankAccount.AccountId,
                        Description = $"قبض نقدية/بنك بموجب سند رقم {paymentNumber}",
                        Debit = baseAmount,
                        Credit = 0m
                    },
                    new()
                    {
                        AccountId = customer.AccountId,
                        Description = $"سداد عميل بموجب سند رقم {paymentNumber}",
                        Debit = 0m,
                        Credit = baseAmount
                    }
                }
            };

            // Process Invoices Allocation
            if (request.Allocations != null && request.Allocations.Any())
            {
                decimal totalAllocated = request.Allocations.Sum(a => a.AllocatedAmount);
                if (totalAllocated > request.Amount)
                    throw new BusinessRuleViolationException($"إجمالي المبالغ الموزعة على الفواتير ({totalAllocated:F2}) يتجاوز مبلغ السند ({request.Amount:F2}).");

                foreach (var alloc in request.Allocations)
                {
                    if (alloc.SalesInvoiceId.HasValue && alloc.AllocatedAmount > 0)
                    {
                        var invoice = await _context.SalesInvoices
                            .FirstOrDefaultAsync(si => si.Id == alloc.SalesInvoiceId.Value && si.CompanyId == companyId, cancellationToken)
                            ?? throw new NotFoundException(nameof(SalesInvoice), alloc.SalesInvoiceId.Value);

                        if (invoice.CustomerId != customer.Id)
                            throw new BusinessRuleViolationException($"الفاتورة رقم '{invoice.InvoiceNumber}' لا تخص العميل '{customer.NameAr}'.");

                        invoice.PaidAmount += alloc.AllocatedAmount;
                        invoice.RemainingAmount = Math.Max(0m, invoice.GrandTotal - invoice.PaidAmount);

                        if (invoice.RemainingAmount <= 0.001m)
                            invoice.Status = InvoiceStatus.Paid;
                        else
                            invoice.Status = InvoiceStatus.PartiallyPaid;

                        payment.Allocations.Add(new PaymentAllocation
                        {
                            SalesInvoiceId = invoice.Id,
                            AllocatedAmount = alloc.AllocatedAmount
                        });
                    }
                }
            }
        }
        else
        {
            // Payment to Supplier (سند صرف مورد)
            if (!request.SupplierId.HasValue || request.SupplierId.Value == 0)
                throw new BusinessRuleViolationException("يجب تحديد المورد لسند الصرف.");

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == request.SupplierId.Value && s.CompanyId == companyId && !s.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(Supplier), request.SupplierId.Value);

            payment.SupplierId = supplier.Id;

            // Balanced GL Entry:
            // Debit: Supplier Account (supplier.AccountId)
            // Credit: Bank / Cash Account (bankAccount.AccountId)
            journalEntryDto = new CreateJournalEntryDto
            {
                EntryDate = request.PaymentDate,
                ReferenceNumber = paymentNumber,
                Description = $"سند صرف رقم {paymentNumber} للمورد: {supplier.NameAr}",
                BranchId = branchId,
                EntryType = JournalEntryType.Manual,
                SourceDocumentType = SourceDocumentType.SupplierPayment,
                SourceDocumentId = paymentNumber,
                Lines = new List<CreateJournalEntryLineDto>
                {
                    new()
                    {
                        AccountId = supplier.AccountId,
                        Description = $"سداد للمورد بموجب سند صرف رقم {paymentNumber}",
                        Debit = baseAmount,
                        Credit = 0m
                    },
                    new()
                    {
                        AccountId = bankAccount.AccountId,
                        Description = $"صرف نقدية/بنك بموجب سند رقم {paymentNumber}",
                        Debit = 0m,
                        Credit = baseAmount
                    }
                }
            };

            // Process Invoices Allocation
            if (request.Allocations != null && request.Allocations.Any())
            {
                decimal totalAllocated = request.Allocations.Sum(a => a.AllocatedAmount);
                if (totalAllocated > request.Amount)
                    throw new BusinessRuleViolationException($"إجمالي المبالغ الموزعة على الفواتير ({totalAllocated:F2}) يتجاوز مبلغ السند ({request.Amount:F2}).");

                foreach (var alloc in request.Allocations)
                {
                    if (alloc.PurchaseInvoiceId.HasValue && alloc.AllocatedAmount > 0)
                    {
                        var invoice = await _context.PurchaseInvoices
                            .FirstOrDefaultAsync(pi => pi.Id == alloc.PurchaseInvoiceId.Value && pi.CompanyId == companyId, cancellationToken)
                            ?? throw new NotFoundException(nameof(PurchaseInvoice), alloc.PurchaseInvoiceId.Value);

                        if (invoice.SupplierId != supplier.Id)
                            throw new BusinessRuleViolationException($"الفاتورة رقم '{invoice.InvoiceNumber}' لا تخص المورد '{supplier.NameAr}'.");

                        invoice.PaidAmount += alloc.AllocatedAmount;
                        invoice.RemainingAmount = Math.Max(0m, invoice.GrandTotal - invoice.PaidAmount);

                        if (invoice.RemainingAmount <= 0.001m)
                            invoice.Status = InvoiceStatus.Paid;
                        else
                            invoice.Status = InvoiceStatus.PartiallyPaid;

                        payment.Allocations.Add(new PaymentAllocation
                        {
                            PurchaseInvoiceId = invoice.Id,
                            AllocatedAmount = alloc.AllocatedAmount
                        });
                    }
                }
            }
        }

        // Post Journal Entry
        var postedEntry = await _journalEntryService.CreateAndPostAsync(journalEntryDto, cancellationToken);
        payment.JournalEntryId = postedEntry.Id;

        await _context.Payments.AddAsync(payment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment {PaymentNumber} posted successfully with Journal Entry {EntryNumber}.", paymentNumber, postedEntry.EntryNumber);

        return await GetByIdAsync(payment.Id, cancellationToken);
    }

    private static PaymentDto MapToDto(Payment p)
    {
        return new PaymentDto
        {
            Id = p.Id,
            CompanyId = p.CompanyId,
            BranchId = p.BranchId,
            BranchName = p.Branch?.NameAr ?? string.Empty,
            PaymentNumber = p.PaymentNumber,
            PaymentType = p.PaymentType,
            PaymentMethod = p.PaymentMethod,
            PaymentDate = p.PaymentDate,
            BankAccountId = p.BankAccountId,
            BankAccountName = p.BankAccount?.NameAr ?? string.Empty,
            CustomerId = p.CustomerId,
            CustomerName = p.Customer?.NameAr,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier?.NameAr,
            Amount = p.Amount,
            CurrencyId = p.CurrencyId,
            CurrencyCode = p.Currency?.Code ?? string.Empty,
            ExchangeRate = p.ExchangeRate,
            BaseAmount = p.BaseAmount,
            ReferenceNumber = p.ReferenceNumber,
            ChequeNumber = p.ChequeNumber,
            ChequeDueDate = p.ChequeDueDate,
            Status = p.Status,
            JournalEntryId = p.JournalEntryId,
            JournalEntryNumber = p.JournalEntry?.EntryNumber,
            Allocations = p.Allocations.Select(a => new PaymentAllocationDto
            {
                Id = a.Id,
                PaymentId = a.PaymentId,
                SalesInvoiceId = a.SalesInvoiceId,
                SalesInvoiceNumber = a.SalesInvoice?.InvoiceNumber,
                PurchaseInvoiceId = a.PurchaseInvoiceId,
                PurchaseInvoiceNumber = a.PurchaseInvoice?.InvoiceNumber,
                AllocatedAmount = a.AllocatedAmount
            }).ToList()
        };
    }
}
