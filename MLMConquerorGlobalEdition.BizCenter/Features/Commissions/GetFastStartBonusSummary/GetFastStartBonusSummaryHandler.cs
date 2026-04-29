using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusSummary;

public class GetFastStartBonusSummaryHandler : IRequestHandler<GetFastStartBonusSummaryQuery, Result<CommissionBonusSummaryDto>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetFastStartBonusSummaryHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<CommissionBonusSummaryDto>> Handle(GetFastStartBonusSummaryQuery request, CancellationToken ct)
    {
        var v = await _service.GetFastStartBonusSummaryAsync(_currentUser.MemberId, ct);

        var dto = new CommissionBonusSummaryDto
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
        };

        return Result<CommissionBonusSummaryDto>.Success(dto);
    }
}
