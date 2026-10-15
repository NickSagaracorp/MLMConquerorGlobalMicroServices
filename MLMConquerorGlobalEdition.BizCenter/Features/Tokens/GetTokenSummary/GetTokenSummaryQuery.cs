using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenSummary;

public record GetTokenSummaryQuery() : IRequest<Result<TokenSummaryDto>>;
