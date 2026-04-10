using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
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
        var memberId = _currentUser.MemberId;

        var query =
            from t  in _db.TokenTransactions.AsNoTracking()
            where t.MemberId == memberId
            join tt in _db.TokenTypes
                on t.TokenTypeId equals tt.Id
            join mp in _db.MemberProfiles
                on t.UsedByMemberId equals mp.MemberId into usedByGroup
            from usedBy in usedByGroup.DefaultIfEmpty()
            select new TokenTransactionDto
            {
                TokenTypeName    = tt.Name,
                TransactionType  = t.TransactionType.ToString(),
                TokenCode        = t.ReferenceId,
                IsUsed           = t.UsedByMemberId != null,
                UsedByMemberId   = t.UsedByMemberId,
                UsedByMemberName = usedBy != null
                    ? usedBy.FirstName + " " + usedBy.LastName
                    : null,
                Amount    = t.Quantity,
                OccurredAt = t.CreationDate,
                Notes     = t.Notes
            };

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.OccurredAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

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
