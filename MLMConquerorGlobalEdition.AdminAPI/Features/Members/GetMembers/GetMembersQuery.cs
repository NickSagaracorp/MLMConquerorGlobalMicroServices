using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;

public record GetMembersQuery(PagedRequest Page, string? StatusFilter, string? SponsorId = null, string? SearchTerm = null)
    : IRequest<Result<PagedResult<AdminMemberDto>>>;
