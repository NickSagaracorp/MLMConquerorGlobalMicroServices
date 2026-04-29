using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetVisualizerChildren;

/// <summary>
/// BizCenter "visualizer children" — performs the BizCenter-only ownership check
/// (parent must be in the current member's subtree) then delegates the data
/// query to <see cref="IEnrollmentTeamService"/>.
/// </summary>
public class GetVisualizerChildrenHandler
    : IRequestHandler<GetVisualizerChildrenQuery, Result<IEnumerable<EnrollmentVisualizerNodeDto>>>
{
    private readonly AppDbContext           _db;
    private readonly IEnrollmentTeamService _service;
    private readonly ICurrentUserService    _currentUser;

    public GetVisualizerChildrenHandler(
        AppDbContext db,
        IEnrollmentTeamService service,
        ICurrentUserService currentUser)
    {
        _db          = db;
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<IEnumerable<EnrollmentVisualizerNodeDto>>> Handle(
        GetVisualizerChildrenQuery request, CancellationToken ct)
    {
        var currentMemberId = _currentUser.MemberId;

        // Ownership check — parent must be the current user or somewhere in their subtree.
        if (request.ParentMemberId != currentMemberId)
        {
            var pattern = "/" + currentMemberId + "/";
            var isInSubtree = await _db.GenealogyTree.AsNoTracking()
                .AnyAsync(g => g.MemberId == request.ParentMemberId
                            && g.HierarchyPath.Contains(pattern), ct);
            if (!isInSubtree)
                return Result<IEnumerable<EnrollmentVisualizerNodeDto>>
                    .Failure("VISUALIZER_ACCESS_DENIED",
                             "The requested member is not within your enrollment subtree.");
        }

        var children = await _service.GetVisualizerChildrenAsync(request.ParentMemberId, ct);
        var dtos = children.Select(c => new EnrollmentVisualizerNodeDto
        {
            MemberId    = c.MemberId,
            FullName    = c.FullName,
            StatusCode  = c.StatusCode,
            Points      = c.Points,
            HasChildren = c.HasChildren
        });
        return Result<IEnumerable<EnrollmentVisualizerNodeDto>>.Success(dtos);
    }
}
