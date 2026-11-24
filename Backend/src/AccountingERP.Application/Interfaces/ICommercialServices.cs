using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.Commercial;

namespace AccountingERP.Application.Interfaces;

public interface ICustomerService
{
    Task<List<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CustomerDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateAsync(CreateCustomerDto request, CancellationToken cancellationToken = default);
    Task<CustomerDto> UpdateAsync(int id, UpdateCustomerDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface ISupplierService
{
    Task<List<SupplierDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SupplierDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SupplierDto> CreateAsync(CreateSupplierDto request, CancellationToken cancellationToken = default);
    Task<SupplierDto> UpdateAsync(int id, UpdateSupplierDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface ISalesInvoiceService
{
    Task<PagedResult<SalesInvoiceDto>> GetPagedAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<SalesInvoiceDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<SalesInvoiceDto> CreateDraftAsync(CreateSalesInvoiceDto request, CancellationToken cancellationToken = default);
    Task<SalesInvoiceDto> PostToGLAsync(long id, CancellationToken cancellationToken = default);
}

public interface IPurchaseInvoiceService
{
    Task<PagedResult<PurchaseInvoiceDto>> GetPagedAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto> CreateDraftAsync(CreatePurchaseInvoiceDto request, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto> PostToGLAsync(long id, CancellationToken cancellationToken = default);
}

public interface IPaymentService
{
    Task<PagedResult<PaymentDto>> GetPagedAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<PaymentDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PaymentDto> CreateAndPostPaymentAsync(CreatePaymentDto request, CancellationToken cancellationToken = default);
}
