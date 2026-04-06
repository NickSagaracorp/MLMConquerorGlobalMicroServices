using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.GetAdminPendingPlacements;

public class GetAdminPendingPlacementsHandler
    : IRequestHandler<GetAdminPendingPlacementsQuery, Result<IEnumerable<AdminPendingPlacementDto>>>
{
    private const int PlacementWindowDays       = 30;
    private const int CorrectionWindowHours     = 72;
    private const int MaxPlacementOpportunities = 2;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public GetAdminPendingPlacementsHandler(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<Result<IEnumerable<AdminPendingPlacementDto>>> Handle(
        GetAdminPendingPlacementsQuery request, CancellationToken ct)
    {
        var now          = _clock.Now;
        var windowCutoff = now.AddDays(-PlacementWindowDays);

        // All enrolled members (optionally filtered by sponsor) within the placement window
        var query = _db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.EnrollDate >= windowCutoff);

        if (!string.IsNullOrWhiteSpace(request.SponsorId))
            query = query.Where(m => m.SponsorMemberId == request.SponsorId);

        var enrolledMembers = await query.ToListAsync(ct);

        if (!enrolledMembers.Any())
            return Result<IEnumerable<AdminPendingPlacementDto>>.Success(
                Enumerable.Empty<AdminPendingPlacementDto>());

        var memberIds  = enrolledMembers.Select(m => m.MemberId).ToList();
        var sponsorIds = enrolledMembers
            .Where(m => m.SponsorMemberId != null)
            .Select(m => m.SponsorMemberId!)
            .Distinct()
            .ToList();

        // Dual-team nodes
        var dualNodes = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => memberIds.Contains(d.MemberId))
            .ToDictionaryAsync(d => d.MemberId, ct);

        // Latest placement log per member
        var logByMember = (await _db.PlacementLogs
            .AsNoTracking()
            .Where(p => memberIds.Contains(p.MemberId))
            .GroupBy(p => p.MemberId)
            .Select(g => g.OrderByDescending(p => p.CreationDate).First())
            .ToListAsync(ct))
            .ToDictionary(p => p.MemberId);

        // Parent names
        var parentIds = dualNodes.Values
            .Where(n => n.ParentMemberId != null)
            .Select(n => n.ParentMemberId!)
            .Distinct()
            .ToList();

        var allLookupIds = parentIds.Union(sponsorIds).Distinct().ToList();
        var nameCache = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => allLookupIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId, m => $"{m.FirstName} {m.LastName}", ct);

        var result = enrolledMembers.Select(member =>
        {
            dualNodes.TryGetValue(member.MemberId, out var node);
            logByMember.TryGetValue(member.MemberId, out var log);
            nameCache.TryGetValue(member.SponsorMemberId ?? "", out var sponsorName);
            nameCache.TryGetValue(node?.ParentMemberId ?? "", out var parentName);

            var isPlaced           = node != null;
            var opportunitiesUsed  = log?.UnplacementCount ?? 0;
            var firstPlacedAt      = log?.FirstPlacementDate;
            var isBlocked          = opportunitiesUsed >= MaxPlacementOpportunities;
            var daysRemaining      = (int)(member.EnrollDate.AddDays(PlacementWindowDays) - now).TotalDays;
            var isWindowExpired    = daysRemaining <= 0;
            var windowPercentUsed  = Math.Min(100, Math.Max(0,
                ((now - member.EnrollDate).TotalDays / PlacementWindowDays) * 100));

            var correctionHoursLeft = 0.0;
            var canCorrect          = false;

            if (isPlaced && firstPlacedAt.HasValue)
            {
                var correctionExpiry = firstPlacedAt.Value.AddHours(CorrectionWindowHours);
                if (now < correctionExpiry && !isBlocked)
                {
                    canCorrect          = true;
                    correctionHoursLeft = (correctionExpiry - now).TotalHours;
                }
            }

            string status;
            if (isBlocked)
                status = "Blocked";
            else if (isWindowExpired && !isPlaced)
                status = "Expired";
            else if (isPlaced && log?.Action == "AutoPlaced")
                status = "AutoPlaced";
            else if (isPlaced)
                status = "Placed";
            else
                status = "Unplaced";

            return new AdminPendingPlacementDto
            {
                MemberId                   = member.MemberId,
                FullName                   = $"{member.FirstName} {member.LastName}",
                MemberCode                 = member.MemberId,
                PhotoUrl                   = member.ProfilePhotoUrl,
                EnrolledAt                 = member.EnrollDate,
                DaysRemainingInWindow      = Math.Max(0, daysRemaining),
                WindowPercentUsed          = windowPercentUsed,
                PlacementOpportunitiesUsed = opportunitiesUsed,
                IsAlreadyPlaced            = isPlaced,
                PlacedAt                   = firstPlacedAt,
                CanCorrect                 = canCorrect,
                CorrectionHoursRemaining   = Math.Round(correctionHoursLeft, 1),
                CurrentTreeSide            = node?.Side.ToString(),
                CurrentParentMemberId      = node?.ParentMemberId,
                CurrentParentFullName      = parentName,
                IsWindowExpired            = isWindowExpired,
                IsBlocked                  = isBlocked,
                PlacementStatus            = status,
                SponsorMemberId            = member.SponsorMemberId,
                SponsorFullName            = sponsorName
            };
        })
        .OrderBy(d => d.IsBlocked)
        .ThenBy(d => d.DaysRemainingInWindow)
        .ToList();

        return Result<IEnumerable<AdminPendingPlacementDto>>.Success(result);
    }
}
