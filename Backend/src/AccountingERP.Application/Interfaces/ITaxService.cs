using AccountingERP.Application.DTOs.Master;
using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.Interfaces;

public interface ITaxService
{
    Task<List<TaxTypeDto>> GetAllTaxTypesAsync(TaxCategory? category = null, CancellationToken cancellationToken = default);
    Task<TaxTypeDto> GetTaxTypeByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TaxTypeDto> CreateTaxTypeAsync(CreateTaxTypeDto request, CancellationToken cancellationToken = default);
    Task<TaxTypeDto> UpdateTaxTypeAsync(int id, UpdateTaxTypeDto request, CancellationToken cancellationToken = default);
    Task<TaxRateDto> AddTaxRateAsync(int taxTypeId, CreateTaxRateDto request, CancellationToken cancellationToken = default);
    Task<decimal> GetEffectiveRateForDateAsync(int taxTypeId, DateTime date, CancellationToken cancellationToken = default);
}
