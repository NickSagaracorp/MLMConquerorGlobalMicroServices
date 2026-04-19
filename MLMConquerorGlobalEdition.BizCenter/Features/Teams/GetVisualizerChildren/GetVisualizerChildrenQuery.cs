using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetVisualizerChildren;

/// <summary>
/// Returns the direct children of <paramref name="ParentMemberId"/> in the enrollment tree,
/// including member name, status code, enrollment points, and whether each child has children of its own.
/// </summary>
public record GetVisualizerChildrenQuery(string ParentMemberId)
    : IRequest<Result<IEnumerable<EnrollmentVisualizerNodeDto>>>;
