using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenBalances;

public record GetTokenBalancesQuery() : IRequest<Result<IEnumerable<TokenBalanceDto>>>;
