using AccountingERP.Application.Common.Models;
using AccountingERP.Application.DTOs.AssetAndSystem;

namespace AccountingERP.Application.Interfaces;

public interface IFixedAssetService
{
    Task<PagedResult<FixedAssetDto>> GetPagedAsync(FixedAssetFilterRequest filter, CancellationToken cancellationToken = default);
    Task<FixedAssetDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FixedAssetDto> CreateAsync(CreateFixedAssetDto request, CancellationToken cancellationToken = default);
    Task<FixedAssetDto> UpdateAsync(int id, UpdateFixedAssetDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<AssetDepreciationDto> CalculateAndPostDepreciationAsync(PostAssetDepreciationDto request, CancellationToken cancellationToken = default);
    Task<List<AssetDepreciationDto>> GetAssetDepreciationsAsync(int fixedAssetId, CancellationToken cancellationToken = default);
    Task<AssetDisposalResultDto> DisposeAssetAsync(DisposeFixedAssetDto request, CancellationToken cancellationToken = default);
}
