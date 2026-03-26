using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenBalances;

public class GetTokenBalancesHandler : IRequestHandler<GetTokenBalancesQuery, Result<IEnumerable<TokenBalanceDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetTokenBalancesHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<IEnumerable<TokenBalanceDto>>> Handle(GetTokenBalancesQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var balances = await _db.TokenBalances
            .AsNoTracking()
            .Where(tb => tb.MemberId == memberId)
            .Join(
                _db.TokenTypes,
                tb => tb.TokenTypeId,
                tt => tt.Id,
                (tb, tt) => new TokenBalanceDto
                {
                    TokenTypeName = tt.Name,
                    IsGuestPass = tt.IsGuestPass,
                    Balance = tb.Balance
                })
            .ToListAsync(ct);

        return Result<IEnumerable<TokenBalanceDto>>.Success(balances);
    }
}
