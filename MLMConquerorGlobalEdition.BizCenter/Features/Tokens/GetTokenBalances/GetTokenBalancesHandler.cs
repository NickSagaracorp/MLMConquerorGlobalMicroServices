using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenBalances;

public class GetTokenBalancesHandler : IRequestHandler<GetTokenBalancesQuery, Result<IEnumerable<TokenBalanceDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public GetTokenBalancesHandler(AppDbContext db, ICurrentUserService currentUser, ICacheService cache)
    {
        _db          = db;
        _currentUser = currentUser;
        _cache       = cache;
    }

    public async Task<Result<IEnumerable<TokenBalanceDto>>> Handle(
        GetTokenBalancesQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var cacheKey = CacheKeys.MemberTokenBalances(memberId);

        var cached = await _cache.GetAsync<List<TokenBalanceDto>>(cacheKey, ct);
        if (cached is not null)
            return Result<IEnumerable<TokenBalanceDto>>.Success(cached);

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
                    IsGuestPass   = tt.IsGuestPass,
                    Balance       = tb.Balance
                })
            .ToListAsync(ct);

        await _cache.SetAsync(cacheKey, balances, CacheKeys.MemberTokenBalancesTtl, ct);

        return Result<IEnumerable<TokenBalanceDto>>.Success(balances);
    }
}
