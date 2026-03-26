using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductCommissions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.GetProductCommissions;

public class GetProductCommissionsHandler
    : IRequestHandler<GetProductCommissionsQuery, Result<PagedResult<ProductCommissionDto>>>
{
    private readonly AppDbContext _db;

    public GetProductCommissionsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<ProductCommissionDto>>> Handle(
        GetProductCommissionsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ProductCommissions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.ProductId))
            query = query.Where(x => x.ProductId == request.ProductId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.Id)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .Select(x => new ProductCommissionDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                TriggerSponsorBonus = x.TriggerSponsorBonus,
                TriggerBuilderBonus = x.TriggerBuilderBonus,
                TriggerSponsorBonusTurbo = x.TriggerSponsorBonusTurbo,
                TriggerBuilderBonusTurbo = x.TriggerBuilderBonusTurbo,
                TriggerFastStartBonus = x.TriggerFastStartBonus,
                TriggerBoostBonus = x.TriggerBoostBonus,
                CarBonusEligible = x.CarBonusEligible,
                PresidentialBonusEligible = x.PresidentialBonusEligible,
                EligibleMembershipResidual = x.EligibleMembershipResidual,
                EligibleDailyResidual = x.EligibleDailyResidual
            })
            .ToListAsync(cancellationToken);

        return Result<PagedResult<ProductCommissionDto>>.Success(new PagedResult<ProductCommissionDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        });
    }
}
