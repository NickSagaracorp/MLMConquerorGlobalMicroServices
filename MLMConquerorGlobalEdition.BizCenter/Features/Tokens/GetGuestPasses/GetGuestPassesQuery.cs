using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetGuestPasses;

public record GetGuestPassesQuery() : IRequest<Result<IEnumerable<TokenBalanceDto>>>;
