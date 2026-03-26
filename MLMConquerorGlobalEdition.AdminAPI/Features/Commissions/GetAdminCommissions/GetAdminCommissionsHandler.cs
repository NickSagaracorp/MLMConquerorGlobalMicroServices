using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetAdminCommissions;

public class GetAdminCommissionsHandler
    : IRequestHandler<GetAdminCommissionsQuery, Result<PagedResult<AdminCommissionDto>>>
{
    private readonly AppDbContext _db;

    public GetAdminCommissionsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<AdminCommissionDto>>> Handle(
        GetAdminCommissionsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.CommissionEarnings
            .AsNoTracking()
            .Include(c => c.CommissionType);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.EarnedDate)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .Select(c => new AdminCommissionDto
            {
                Id = c.Id,
                BeneficiaryMemberId = c.BeneficiaryMemberId,
                CommissionTypeName = c.CommissionType != null ? c.CommissionType.Name : string.Empty,
                Amount = c.Amount,
                Status = c.Status.ToString(),
                EarnedDate = c.EarnedDate,
                PaymentDate = c.PaymentDate,
                IsManualEntry = c.IsManualEntry
            })
            .ToListAsync(cancellationToken);

        var result = new PagedResult<AdminCommissionDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        };

        return Result<PagedResult<AdminCommissionDto>>.Success(result);
    }
}
