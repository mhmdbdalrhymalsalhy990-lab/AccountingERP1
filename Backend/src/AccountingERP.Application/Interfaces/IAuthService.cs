using AccountingERP.Application.DTOs.Auth;

namespace AccountingERP.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> SelectCompanyBranchAsync(string userId, SelectCompanyBranchRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> GetCurrentUserProfileAsync(string userId, int? currentCompanyId, int? currentBranchId, CancellationToken cancellationToken = default);
}
