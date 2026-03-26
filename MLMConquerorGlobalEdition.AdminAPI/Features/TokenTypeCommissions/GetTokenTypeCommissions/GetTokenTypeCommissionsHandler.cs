using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.GetTokenTypeCommissions;

public class GetTokenTypeCommissionsHandler
    : IRequestHandler<GetTokenTypeCommissionsQuery, Result<PagedResult<TokenTypeCommissionDto>>>
{
    private readonly AppDbContext _db;

    public GetTokenTypeCommissionsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<TokenTypeCommissionDto>>> Handle(
        GetTokenTypeCommissionsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.TokenTypeCommissions.AsNoTracking();

        if (request.TokenTypeId.HasValue)
            query = query.Where(x => x.TokenTypeId == request.TokenTypeId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.TokenTypeId)
            .ThenBy(x => x.Id)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .Select(x => new TokenTypeCommissionDto
            {
                Id = x.Id,
                TokenTypeId = x.TokenTypeId,
                CommissionTypeId = x.CommissionTypeId,
                CommissionPerToken = x.CommissionPerToken,
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

        return Result<PagedResult<TokenTypeCommissionDto>>.Success(new PagedResult<TokenTypeCommissionDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        });
    }
}
