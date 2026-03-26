using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetAllTeamMembers;

public record GetAllTeamMembersQuery(int Page, int PageSize) : IRequest<Result<PagedResult<TeamMemberDto>>>;
