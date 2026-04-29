using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetVisualizerStats;

/// <summary>BizCenter "visualizer stats" — delegates to <see cref="IEnrollmentTeamService"/>.</summary>
public class GetVisualizerStatsHandler
    : IRequestHandler<GetVisualizerStatsQuery, Result<EnrollmentVisualizerStatsDto>>
{
    private readonly IEnrollmentTeamService _service;
    private readonly ICurrentUserService    _currentUser;

    public GetVisualizerStatsHandler(IEnrollmentTeamService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<EnrollmentVisualizerStatsDto>> Handle(
        GetVisualizerStatsQuery request, CancellationToken ct)
    {
        var v = await _service.GetVisualizerStatsAsync(_currentUser.MemberId, ct);
        return Result<EnrollmentVisualizerStatsDto>.Success(new EnrollmentVisualizerStatsDto
        {
            TotalMembers     = v.TotalMembers,
            TotalQualified   = v.TotalQualified,
            TotalUnqualified = v.TotalUnqualified,
            TotalCancelled   = v.TotalCancelled
        });
    }
}
