using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoMembers;

public record GetPromoMembersQuery(string PromoId, PagedRequest Page)
    : IRequest<Result<PagedResult<PromoMemberDto>>>;
