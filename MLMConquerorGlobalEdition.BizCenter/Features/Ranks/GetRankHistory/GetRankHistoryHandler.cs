using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankHistory;

public class GetRankHistoryHandler : IRequestHandler<GetRankHistoryQuery, Result<PagedResult<RankHistoryDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetRankHistoryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<RankHistoryDto>>> Handle(
        GetRankHistoryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Collect all previous-rank IDs so we can resolve names in one query
        var rawItems = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId && !r.IsDeleted)
            .OrderByDescending(r => r.AchievedAt)
            .ToListAsync(ct);

        var totalCount = rawItems.Count;

        var pagedItems = rawItems
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Resolve previous rank names in a single query
        var previousRankIds = pagedItems
            .Where(r => r.PreviousRankId.HasValue)
            .Select(r => r.PreviousRankId!.Value)
            .Distinct()
            .ToList();

        var previousRankNames = previousRankIds.Count > 0
            ? await _db.RankDefinitions
                .AsNoTracking()
                .Where(rd => previousRankIds.Contains(rd.Id))
                .ToDictionaryAsync(rd => rd.Id, rd => rd.Name, ct)
            : new Dictionary<int, string>();

        var items = pagedItems.Select(r => new RankHistoryDto
        {
            Id               = r.Id,
            RankDefinitionId = r.RankDefinitionId,
            RankName         = r.RankDefinition?.Name ?? string.Empty,
            RankSortOrder    = r.RankDefinition?.SortOrder ?? 0,
            AchievedAt       = r.AchievedAt,
            PreviousRankId   = r.PreviousRankId,
            PreviousRankName = r.PreviousRankId.HasValue &&
                               previousRankNames.TryGetValue(r.PreviousRankId.Value, out var prevName)
                               ? prevName : null,
            HasCertificate   = r.GeneratedCertificateUrl is not null
        }).ToList();

        return Result<PagedResult<RankHistoryDto>>.Success(new PagedResult<RankHistoryDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = request.Page,
            PageSize   = request.PageSize
        });
    }
}
