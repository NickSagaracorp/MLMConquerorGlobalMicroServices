using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentMyTeam;

public record GetEnrollmentMyTeamQuery(
    int     Page       = 1,
    int     PageSize   = 20,
    string? Search     = null,
    DateTime? From     = null,
    DateTime? To       = null
) : IRequest<Result<PagedResult<EnrollmentMyTeamMemberDto>>>;
