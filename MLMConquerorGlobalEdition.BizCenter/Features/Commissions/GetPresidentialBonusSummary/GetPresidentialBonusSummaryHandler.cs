using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusSummary;

public class GetPresidentialBonusSummaryHandler : IRequestHandler<GetPresidentialBonusSummaryQuery, Result<CommissionBonusSummaryDto>>
{
    private const int PresidentialBonusCategoryId = 4;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetPresidentialBonusSummaryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<CommissionBonusSummaryDto>> Handle(GetPresidentialBonusSummaryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var summary = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == PresidentialBonusCategoryId
                                            && t.Name.Contains("Presidential")),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, _) => c.Amount)
            .GroupBy(_ => 1)
            .Select(g => new CommissionBonusSummaryDto
            {
                Count       = g.Count(),
                TotalAmount = g.Sum()
            })
            .FirstOrDefaultAsync(ct);

        return Result<CommissionBonusSummaryDto>.Success(summary ?? new CommissionBonusSummaryDto());
    }
}
