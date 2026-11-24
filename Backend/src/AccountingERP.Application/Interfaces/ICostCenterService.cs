using AccountingERP.Application.DTOs.Accounting;

namespace AccountingERP.Application.Interfaces;

public interface ICostCenterService
{
    Task<List<CostCenterTreeDto>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<List<CostCenterDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CostCenterDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CostCenterDto> CreateAsync(CreateCostCenterDto request, CancellationToken cancellationToken = default);
    Task<CostCenterDto> UpdateAsync(int id, UpdateCostCenterDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
