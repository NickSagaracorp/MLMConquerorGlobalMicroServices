using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GetAdminTokenBalances;

public class GetAdminTokenBalancesHandler
    : IRequestHandler<GetAdminTokenBalancesQuery, Result<PagedResult<AdminTokenBalanceDto>>>
{
    private readonly AppDbContext _db;
    public GetAdminTokenBalancesHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<AdminTokenBalanceDto>>> Handle(
        GetAdminTokenBalancesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.TokenBalances.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.MemberIdFilter))
            query = query.Where(t => t.MemberId == request.MemberIdFilter);

        var totalCount = await query.CountAsync(cancellationToken);

        var balances = await query
            .OrderByDescending(t => t.CreationDate)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);

        var memberIds = balances.Select(b => b.MemberId).Distinct().ToList();
        var members = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => memberIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId, m => $"{m.FirstName} {m.LastName}".Trim(), cancellationToken);

        var tokenTypeIds = balances.Select(b => b.TokenTypeId).Distinct().ToList();
        var tokenTypes = await _db.TokenTypes
            .AsNoTracking()
            .Where(tt => tokenTypeIds.Contains(tt.Id))
            .ToDictionaryAsync(tt => tt.Id, cancellationToken);

        var items = balances.Select(b =>
        {
            tokenTypes.TryGetValue(b.TokenTypeId, out var tokenType);
            members.TryGetValue(b.MemberId, out var memberName);
            return new AdminTokenBalanceDto
            {
                TokenBalanceId = b.Id,
                MemberId = b.MemberId,
                MemberFullName = memberName ?? b.MemberId,
                TokenTypeName = tokenType?.Name ?? string.Empty,
                IsGuestPass = tokenType?.IsGuestPass ?? false,
                Balance = b.Balance
            };
        }).ToList();

        return Result<PagedResult<AdminTokenBalanceDto>>.Success(new PagedResult<AdminTokenBalanceDto>
        {
            Items = items, TotalCount = totalCount,
            Page = request.Page.Page, PageSize = request.Page.PageSize
        });
    }
}
