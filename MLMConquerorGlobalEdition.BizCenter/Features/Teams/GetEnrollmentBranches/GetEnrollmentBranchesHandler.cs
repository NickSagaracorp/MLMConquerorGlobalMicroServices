using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentBranches;

/// <summary>BizCenter "branches" endpoint — delegates to <see cref="IEnrollmentTeamService"/>.</summary>
public class GetEnrollmentBranchesHandler
    : IRequestHandler<GetEnrollmentBranchesQuery, Result<EnrollmentBranchesResultDto>>
{
    private readonly IEnrollmentTeamService _service;
    private readonly ICurrentUserService    _currentUser;

    public GetEnrollmentBranchesHandler(IEnrollmentTeamService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<EnrollmentBranchesResultDto>> Handle(
        GetEnrollmentBranchesQuery request, CancellationToken ct)
    {
        var view = await _service.GetBranchesAsync(
            _currentUser.MemberId, request.Page, request.PageSize, request.Search, ct);

        return Result<EnrollmentBranchesResultDto>.Success(new EnrollmentBranchesResultDto
        {
            TotalPoints              = view.TotalPoints,
            TotalEligibleCurrentRank = view.TotalEligibleCurrentRank,
            TotalEligibleNextRank    = view.TotalEligibleNextRank,
            Branches = new BranchPagedData
            {
                Items      = view.Branches.Items.Select(b => new BranchItemDto
                {
                    MemberId            = b.MemberId,
                    FullName            = b.FullName,
                    TotalPoints         = b.TotalPoints,
                    EligibleCurrentRank = b.EligibleCurrentRank,
                    EligibleNextRank    = b.EligibleNextRank,
                    EligibleCurrentPct  = b.EligibleCurrentPct,
                    EligibleNextPct     = b.EligibleNextPct
                }).ToList(),
                TotalCount = view.Branches.TotalCount,
                Page       = view.Branches.Page,
                PageSize   = view.Branches.PageSize
            }
        });
    }
}
