using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetTeamMembers;

public record GetTeamMembersQuery(int Page, int PageSize) : IRequest<Result<PagedResult<TeamMemberDto>>>;
