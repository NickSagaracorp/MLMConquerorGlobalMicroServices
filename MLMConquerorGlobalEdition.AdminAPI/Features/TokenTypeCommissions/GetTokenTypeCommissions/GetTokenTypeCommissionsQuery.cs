using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.GetTokenTypeCommissions;

public record GetTokenTypeCommissionsQuery(int? TokenTypeId, PagedRequest Page)
    : IRequest<Result<PagedResult<TokenTypeCommissionDto>>>;
