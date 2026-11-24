using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.Interfaces;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

public class CostCenterService : ICostCenterService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CostCenterService> _logger;

    public CostCenterService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        ILogger<CostCenterService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    private int EnsureCompanyId() => _currentTenantService.CompanyId
        ?? throw new InvalidOperationException("لم يتم تحديد سياق الشركة الحالية في الطلب.");

    public async Task<List<CostCenterTreeDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var costCenters = await _context.CostCenters
            .Where(cc => cc.CompanyId == companyId && !cc.IsDeleted)
            .OrderBy(cc => cc.Code)
            .ToListAsync(cancellationToken);

        var roots = costCenters.Where(cc => !cc.ParentCostCenterId.HasValue).ToList();
        return roots.Select(r => BuildTree(r, costCenters)).ToList();
    }

    public async Task<List<CostCenterDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var costCenters = await _context.CostCenters
            .Include(cc => cc.ParentCostCenter)
            .Where(cc => cc.CompanyId == companyId && !cc.IsDeleted)
            .OrderBy(cc => cc.Code)
            .ToListAsync(cancellationToken);

        var ccIds = costCenters.Select(cc => cc.Id).ToList();

        var movements = await _context.JournalEntryLines
            .Where(l => l.CostCenterId.HasValue && ccIds.Contains(l.CostCenterId.Value) &&
                        l.JournalEntry != null && l.JournalEntry.CompanyId == companyId &&
                        l.JournalEntry.Status == JournalEntryStatus.Posted)
            .GroupBy(l => l.CostCenterId!.Value)
            .Select(g => new { CostCenterId = g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .ToDictionaryAsync(x => x.CostCenterId, cancellationToken);

        return costCenters.Select(cc =>
        {
            movements.TryGetValue(cc.Id, out var m);
            return new CostCenterDto
            {
                Id = cc.Id,
                CompanyId = cc.CompanyId,
                ParentCostCenterId = cc.ParentCostCenterId,
                ParentCostCenterName = cc.ParentCostCenter?.NameAr,
                Code = cc.Code,
                NameAr = cc.NameAr,
                NameEn = cc.NameEn,
                Level = cc.Level,
                IsActive = cc.IsActive,
                TotalDebitMovement = m?.Debit ?? 0m,
                TotalCreditMovement = m?.Credit ?? 0m
            };
        }).ToList();
    }

    public async Task<CostCenterDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var cc = await _context.CostCenters
            .Include(c => c.ParentCostCenter)
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(CostCenter), id);

        var mov = await _context.JournalEntryLines
            .Where(l => l.CostCenterId == id &&
                        l.JournalEntry != null && l.JournalEntry.CompanyId == companyId &&
                        l.JournalEntry.Status == JournalEntryStatus.Posted)
            .GroupBy(l => l.CostCenterId)
            .Select(g => new { Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .FirstOrDefaultAsync(cancellationToken);

        return new CostCenterDto
        {
            Id = cc.Id,
            CompanyId = cc.CompanyId,
            ParentCostCenterId = cc.ParentCostCenterId,
            ParentCostCenterName = cc.ParentCostCenter?.NameAr,
            Code = cc.Code,
            NameAr = cc.NameAr,
            NameEn = cc.NameEn,
            Level = cc.Level,
            IsActive = cc.IsActive,
            TotalDebitMovement = mov?.Debit ?? 0m,
            TotalCreditMovement = mov?.Credit ?? 0m
        };
    }

    public async Task<CostCenterDto> CreateAsync(CreateCostCenterDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        int level = 1;
        if (request.ParentCostCenterId.HasValue && request.ParentCostCenterId.Value > 0)
        {
            var parent = await _context.CostCenters
                .FirstOrDefaultAsync(c => c.Id == request.ParentCostCenterId.Value && c.CompanyId == companyId && !c.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(CostCenter), request.ParentCostCenterId.Value);

            level = parent.Level + 1;
        }

        string code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            var count = await _context.CostCenters.CountAsync(c => c.CompanyId == companyId, cancellationToken);
            code = $"CC-{(count + 1):D2}";
        }

        var cc = new CostCenter
        {
            CompanyId = companyId,
            ParentCostCenterId = request.ParentCostCenterId,
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim(),
            Level = level,
            IsActive = request.IsActive
        };

        await _context.CostCenters.AddAsync(cc, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cost Center '{Code}' ({NameAr}) created successfully.", cc.Code, cc.NameAr);
        return await GetByIdAsync(cc.Id, cancellationToken);
    }

    public async Task<CostCenterDto> UpdateAsync(int id, UpdateCostCenterDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var cc = await _context.CostCenters
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(CostCenter), id);

        cc.NameAr = request.NameAr.Trim();
        cc.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr.Trim() : request.NameEn.Trim();
        cc.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(cc.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var cc = await _context.CostCenters
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(CostCenter), id);

        var hasChildren = await _context.CostCenters.AnyAsync(c => c.ParentCostCenterId == id && !c.IsDeleted, cancellationToken);
        if (hasChildren)
            throw new BusinessRuleViolationException($"لا يمكن حذف مركز التكلفة '{cc.NameAr}' لوجود مراكز تكلفة فرعية تابعة له.");

        var hasLines = await _context.JournalEntryLines.AnyAsync(l => l.CostCenterId == id, cancellationToken);
        if (hasLines)
            throw new BusinessRuleViolationException($"لا يمكن حذف مركز التكلفة '{cc.NameAr}' لوجود قيود محاسبية مسجلة عليه.");

        cc.IsDeleted = true;
        cc.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cost Center {Id} soft deleted.", id);
    }

    private static CostCenterTreeDto BuildTree(CostCenter node, List<CostCenter> all)
    {
        return new CostCenterTreeDto
        {
            Id = node.Id,
            ParentCostCenterId = node.ParentCostCenterId,
            Code = node.Code,
            NameAr = node.NameAr,
            NameEn = node.NameEn,
            Level = node.Level,
            IsActive = node.IsActive,
            Children = all.Where(c => c.ParentCostCenterId == node.Id)
                          .OrderBy(c => c.Code)
                          .Select(child => BuildTree(child, all))
                          .ToList()
        };
    }
}
