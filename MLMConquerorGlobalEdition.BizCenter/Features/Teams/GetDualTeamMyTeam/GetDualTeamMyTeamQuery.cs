using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTeamMyTeam;

public record GetDualTeamMyTeamQuery(
    int       Page         = 1,
    int       PageSize     = 20,
    string?   Search       = null,
    DateTime? From         = null,
    DateTime? To           = null,
    bool      BypassCache  = false
) : IRequest<Result<PagedResult<DualTeamMyTeamMemberDto>>>;
