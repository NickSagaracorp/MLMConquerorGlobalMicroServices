using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenTransactions;

public record GetTokenTransactionsQuery(int Page, int PageSize)
    : IRequest<Result<PagedResult<TokenTransactionDto>>>;
