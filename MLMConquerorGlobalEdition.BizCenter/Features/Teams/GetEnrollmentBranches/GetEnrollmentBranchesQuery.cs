using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentBranches;

/// <summary>
/// Returns each direct-sponsored ambassador as a branch row with total enrollment points
/// and per-branch eligible points for the current and next rank caps.
/// </summary>
public record GetEnrollmentBranchesQuery(string? Search, int Page, int PageSize)
    : IRequest<Result<EnrollmentBranchesResultDto>>;
