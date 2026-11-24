namespace AccountingERP.Application.Interfaces;

public interface ICurrentTenantService
{
    int? CompanyId { get; }
    int? BranchId { get; }
    void SetTenant(int companyId, int? branchId = null);
}

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
}
