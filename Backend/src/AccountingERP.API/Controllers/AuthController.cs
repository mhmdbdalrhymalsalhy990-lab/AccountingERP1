using System.Security.Claims;
using AccountingERP.Application.DTOs.Auth;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ICurrentTenantService currentTenantService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate user credentials and return JWT Access Token and Refresh Token with tenant context
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.LoginAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Refresh an expired JWT access token using a valid Refresh Token
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Switch active company or branch context for the currently authenticated user
    /// </summary>
    [HttpPost("select-company")]
    [Authorize]
    public async Task<ActionResult<AuthResponseDto>> SelectCompany([FromBody] SelectCompanyBranchRequestDto request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "المستخدم غير مصرح له." });
        }

        try
        {
            var result = await _authService.SelectCompanyBranchAsync(userId, request, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get profile and accessible companies/branches for the currently authenticated user
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthResponseDto>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "المستخدم غير مصرح له." });
        }

        var currentCompanyId = _currentTenantService.CompanyId;
        var currentBranchId = _currentTenantService.BranchId;

        var result = await _authService.GetCurrentUserProfileAsync(userId, currentCompanyId, currentBranchId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Verification endpoint to test permission enforcement
    /// </summary>
    [HttpGet("test-permission")]
    [HasPermission("COA.Manage")]
    public IActionResult TestPermission()
    {
        return Ok(new
        {
            message = "Permission check passed! You have 'COA.Manage' authority.",
            companyId = _currentTenantService.CompanyId,
            branchId = _currentTenantService.BranchId,
            user = User.Identity?.Name
        });
    }
}
