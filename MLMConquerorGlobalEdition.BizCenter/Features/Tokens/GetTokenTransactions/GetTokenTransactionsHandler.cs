using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenTransactions;

public class GetTokenTransactionsHandler
    : IRequestHandler<GetTokenTransactionsQuery, Result<PagedResult<TokenTransactionDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetTokenTransactionsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<TokenTransactionDto>>> Handle(
        GetTokenTransactionsQuery request, CancellationToken ct)
    {
        // La misma consulta que enseña AdminAPI en la ficha del miembro (MemberTokenQueries).
        var (rows, totalCount) = await MemberTokenQueries.HistoryPageAsync(
            _db, _currentUser.MemberId, request.Page, request.PageSize, ct);

        var items = rows.Select(r => new TokenTransactionDto
        {
            TokenTypeName    = r.TokenTypeName,
            TransactionType  = r.TransactionType,
            TokenCode        = r.TokenCode,
            IsUsed           = r.IsUsed,
            UsedByMemberId   = r.UsedByMemberId,
            UsedByMemberName = r.UsedByMemberName,
            Amount           = r.Amount,
            OccurredAt       = r.OccurredAt,
            Notes            = r.Notes
        }).ToList();

        var result = new PagedResult<TokenTransactionDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = request.Page,
            PageSize   = request.PageSize
        };

        return Result<PagedResult<TokenTransactionDto>>.Success(result);
    }
}
