using AccountingERP.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AccountingERP.Infrastructure.Services;

public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private int? _explicitCompanyId;
    private int? _explicitBranchId;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? CompanyId
    {
        get
        {
            // 1. Explicitly configured tenant (e.g. In-process jobs, Integration test harness)
            if (_explicitCompanyId.HasValue) return _explicitCompanyId;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // 2. Fail-Closed: Only authenticated users with verified company_id claim can access tenant data
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var claim = httpContext.User.FindFirst("company_id")?.Value 
                         ?? httpContext.User.FindFirst("CompanyId")?.Value;
                if (int.TryParse(claim, out var cid)) return cid;
            }

            // Headers alone are NOT trusted without authentication and verification
            return null;
        }
    }

    public int? BranchId
    {
        get
        {
            if (_explicitBranchId.HasValue) return _explicitBranchId;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var claim = httpContext.User.FindFirst("branch_id")?.Value
                         ?? httpContext.User.FindFirst("BranchId")?.Value;
                if (int.TryParse(claim, out var bid)) return bid;
            }

            return null;
        }
    }

    public void SetTenant(int companyId, int? branchId = null)
    {
        _explicitCompanyId = companyId;
        _explicitBranchId = branchId;
    }
}
