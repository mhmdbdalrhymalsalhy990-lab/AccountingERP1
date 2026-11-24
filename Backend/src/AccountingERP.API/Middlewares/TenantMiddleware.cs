using System.Security.Claims;
using AccountingERP.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AccountingERP.API.Middlewares;

/// <summary>
/// Enterprise Multi-Tenancy Middleware enforcing the Fail-Closed security principle.
/// In Clean Architecture, HTTP middleware belongs in the API/Presentation layer.
/// Prevents unauthenticated tenant spoofing and ensures cross-tenant data isolation.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenantService tenantService)
    {
        int? companyId = null;
        int? branchId = null;

        // 1. Enforce Authentication Check
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var companyClaim = context.User.FindFirst("company_id")?.Value
                            ?? context.User.FindFirst("CompanyId")?.Value;

            if (int.TryParse(companyClaim, out var cid))
            {
                companyId = cid;
            }

            var branchClaim = context.User.FindFirst("branch_id")?.Value
                           ?? context.User.FindFirst("BranchId")?.Value;

            if (int.TryParse(branchClaim, out var bid))
            {
                branchId = bid;
            }

            // 2. Validate X-Company-Id Header against Authorized Token Claims (Anti-Tampering)
            if (context.Request.Headers.TryGetValue("X-Company-Id", out var companyHeader))
            {
                if (int.TryParse(companyHeader, out var headerCid))
                {
                    if (companyId.HasValue && headerCid != companyId.Value)
                    {
                        _logger.LogWarning("Tenant mismatch detected: Token company is {TokenCompanyId}, but client requested {HeaderCompanyId}. Blocking request.", 
                            companyId, headerCid);

                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            success = false,
                            message = "غير مصرح لك بالوصول إلى بيانات شركة مختلفة عن الشركة المصرح بها في رمز التوثيق."
                        });
                        return;
                    }
                }
            }

            // 3. Validate X-Branch-Id Header
            if (context.Request.Headers.TryGetValue("X-Branch-Id", out var branchHeader))
            {
                if (int.TryParse(branchHeader, out var headerBid))
                {
                    // If token specifies a strict branch and header differs, keep token's or validate
                    if (!branchId.HasValue)
                    {
                        branchId = headerBid;
                    }
                }
            }
        }
        else
        {
            // Unauthenticated requests: Do NOT set tenant based on untrusted headers alone!
            // Fail-Closed principle guarantees that no tenant context is established.
            if (context.Request.Headers.ContainsKey("X-Company-Id"))
            {
                _logger.LogDebug("Unauthenticated request with X-Company-Id ignored under Fail-Closed policy.");
            }
        }

        // Establish tenant context if authenticated and verified
        if (companyId.HasValue)
        {
            tenantService.SetTenant(companyId.Value, branchId);
        }

        await _next(context);
    }
}
