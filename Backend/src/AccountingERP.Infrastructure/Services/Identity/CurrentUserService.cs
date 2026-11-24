using AccountingERP.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AccountingERP.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

    public string? UserName =>
        _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
