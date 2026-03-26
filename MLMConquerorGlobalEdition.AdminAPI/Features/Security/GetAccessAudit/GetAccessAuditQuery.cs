using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccessAudit;

public record GetAccessAuditQuery(PagedRequest Page) : IRequest<Result<PagedResult<MemberStatusHistory>>>;
