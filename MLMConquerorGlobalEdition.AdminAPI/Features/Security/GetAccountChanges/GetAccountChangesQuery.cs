using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccountChanges;

public record GetAccountChangesQuery(PagedRequest Page) : IRequest<Result<PagedResult<AdminMemberDto>>>;
