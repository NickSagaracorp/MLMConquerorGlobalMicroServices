using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GetTokenCodes;

public class GetTokenCodesHandler : IRequestHandler<GetTokenCodesQuery, Result<PagedResult<TokenCodeDto>>>
{
    private readonly AppDbContext _db;
    public GetTokenCodesHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<TokenCodeDto>>> Handle(
        GetTokenCodesQuery request, CancellationToken ct)
    {
        // Only transactions that have a TokenCode (ReferenceId) assigned
        var query = _db.TokenTransactions
            .AsNoTracking()
            .Where(t => t.MemberId == request.MemberId && t.ReferenceId != null);

        if (request.TokenTypeId.HasValue)
            query = query.Where(t => t.TokenTypeId == request.TokenTypeId.Value);

        if (request.IsUsed.HasValue)
            query = query.Where(t => request.IsUsed.Value ? t.UsedAt != null : t.UsedAt == null);

        var totalCount = await query.CountAsync(ct);

        var transactions = await query
            .OrderByDescending(t => t.CreationDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var tokenTypeIds = transactions.Select(t => t.TokenTypeId).Distinct().ToList();
        var tokenTypes = await _db.TokenTypes
            .AsNoTracking()
            .Where(tt => tokenTypeIds.Contains(tt.Id))
            .ToDictionaryAsync(tt => tt.Id, ct);

        var items = transactions.Select(t =>
        {
            tokenTypes.TryGetValue(t.TokenTypeId, out var tokenType);
            return new TokenCodeDto(
                Id: t.Id,
                TokenCode: t.ReferenceId!,
                TokenTypeName: tokenType?.Name ?? string.Empty,
                IsGuestPass: tokenType?.IsGuestPass ?? false,
                TransactionType: t.TransactionType.ToString(),
                IsUsed: t.UsedAt.HasValue,
                UsedByMemberId: t.UsedByMemberId,
                UsedAt: t.UsedAt,
                CreatedDate: t.CreationDate
            );
        }).ToList();

        return Result<PagedResult<TokenCodeDto>>.Success(new PagedResult<TokenCodeDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
