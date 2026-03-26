using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GetAdminTokenBalances;

public record GetAdminTokenBalancesQuery(PagedRequest Page, string? MemberIdFilter)
    : IRequest<Result<PagedResult<AdminTokenBalanceDto>>>;
