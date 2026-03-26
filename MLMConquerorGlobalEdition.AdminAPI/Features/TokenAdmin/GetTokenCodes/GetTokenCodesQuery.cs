using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GetTokenCodes;

public record GetTokenCodesQuery(
    string MemberId,
    int? TokenTypeId,
    bool? IsUsed,
    int Page,
    int PageSize
) : IRequest<Result<PagedResult<TokenCodeDto>>>;
