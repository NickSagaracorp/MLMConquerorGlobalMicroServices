using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusSummary;

public class GetPresidentialBonusSummaryHandler : IRequestHandler<GetPresidentialBonusSummaryQuery, Result<CommissionBonusSummaryDto>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetPresidentialBonusSummaryHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<CommissionBonusSummaryDto>> Handle(GetPresidentialBonusSummaryQuery request, CancellationToken ct)
    {
        var v = await _service.GetPresidentialBonusSummaryAsync(_currentUser.MemberId, ct);

        return Result<CommissionBonusSummaryDto>.Success(new CommissionBonusSummaryDto
        {
            Count              = v.Count,
            TotalAmount        = v.TotalAmount,
            IsExtendedMode     = v.IsExtendedMode,
            IsDisqualifiedW2W3 = v.IsDisqualifiedW2W3,
            Windows            = v.Windows?.Select(w => new FsbWindowDto
            {
                WindowNumber   = w.WindowNumber,
                IsPromo        = w.IsPromo,
                Amount         = w.Amount,
                IsCompleted    = w.IsCompleted,
                IsActive       = w.IsActive,
                StartDate      = w.StartDate,
                EndDate        = w.EndDate,
                SponsoredCount = w.SponsoredCount,
                IsHidden       = w.IsHidden
            }).ToList()
        });
    }
}
