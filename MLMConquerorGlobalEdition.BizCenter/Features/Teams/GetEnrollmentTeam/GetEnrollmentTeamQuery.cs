using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentTeam;

public record GetEnrollmentTeamQuery() : IRequest<Result<IEnumerable<TeamMemberDto>>>;
