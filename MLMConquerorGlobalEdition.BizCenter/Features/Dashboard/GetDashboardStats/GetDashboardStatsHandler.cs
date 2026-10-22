using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Dashboard;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Services.Dashboard;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Dashboard.GetDashboardStats;

/// <summary>El miembro sale del token; el cálculo es el del servicio compartido con el perfil de miembro del Admin.</summary>
public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStatsDto>>
{
    private readonly ICurrentUserService    _currentUser;
    private readonly IMemberDashboardService _dashboard;

    public GetDashboardStatsHandler(ICurrentUserService currentUser, IMemberDashboardService dashboard)
    {
        _currentUser = currentUser;
        _dashboard   = dashboard;
    }

    public async Task<Result<DashboardStatsDto>> Handle(GetDashboardStatsQuery request, CancellationToken ct)
    {
        var v = await _dashboard.GetStatsAsync(_currentUser.MemberId, ct);

        return Result<DashboardStatsDto>.Success(new DashboardStatsDto
        {
            TotalEarnings = v.TotalEarnings,
            TeamSize      = v.TeamSize,
            TokenBalance  = v.TokenBalance,
            CurrentRank   = v.CurrentRank,
            FsbWindows    = v.FsbWindows.Select(w => new FsbWindowDto
            {
                WindowNumber   = w.WindowNumber,
                StartDate      = w.StartDate,
                EndDate        = w.EndDate,
                Earned         = w.Earned,
                Status         = w.Status,
                IsActive       = w.IsActive,
                IsCompleted    = w.IsCompleted,
                SponsoredCount = w.SponsoredCount
            }).ToList()
        });
    }
}
