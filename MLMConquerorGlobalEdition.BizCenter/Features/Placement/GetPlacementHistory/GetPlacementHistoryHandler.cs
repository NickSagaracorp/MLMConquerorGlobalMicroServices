using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetPlacementHistory;

public class GetPlacementHistoryHandler
    : IRequestHandler<GetPlacementHistoryQuery, Result<PagedResult<PlacementHistoryDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetPlacementHistoryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<PlacementHistoryDto>>> Handle(
        GetPlacementHistoryQuery request, CancellationToken ct)
    {
        var sponsorMemberId = _currentUser.MemberId;

        // Members enrolled by this ambassador
        var enrolledIds = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == sponsorMemberId && !m.IsDeleted)
            .Select(m => m.MemberId)
            .ToListAsync(ct);

        var query = _db.PlacementLogs
            .AsNoTracking()
            .Where(p => enrolledIds.Contains(p.MemberId))
            .OrderByDescending(p => p.CreationDate);

        var total = await query.CountAsync(ct);

        var logs = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var memberIds = logs.Select(l => l.MemberId)
            .Concat(logs.Select(l => l.PlacedUnderMemberId))
            .Distinct()
            .ToList();

        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => memberIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId, ct);

        var items = logs.Select(l =>
        {
            profiles.TryGetValue(l.MemberId, out var mp);
            profiles.TryGetValue(l.PlacedUnderMemberId, out var pp);

            var executedBy = l.CreatedBy == "system" ? "system"
                : l.Reason?.Contains("Admin") == true   ? "admin"
                : "ambassador";

            return new PlacementHistoryDto
            {
                Id                   = l.Id,
                MemberId             = l.MemberId,
                MemberFullName       = mp != null ? $"{mp.FirstName} {mp.LastName}" : l.MemberId,
                MemberCode           = l.MemberId,
                Action               = l.Action,
                TargetParentMemberId = l.PlacedUnderMemberId,
                TargetParentFullName = pp != null ? $"{pp.FirstName} {pp.LastName}" : null,
                Side                 = l.Side.ToString(),
                ExecutedAt           = l.CreationDate,
                ExecutedByType       = executedBy
            };
        }).ToList();

        return Result<PagedResult<PlacementHistoryDto>>.Success(new PagedResult<PlacementHistoryDto>
        {
            Items      = items,
            TotalCount = total,
            Page       = request.Page,
            PageSize   = request.PageSize
        });
    }
}
