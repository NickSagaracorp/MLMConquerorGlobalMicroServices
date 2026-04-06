using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetPendingPlacements;

public class GetPendingPlacementsHandler
    : IRequestHandler<GetPendingPlacementsQuery, Result<IEnumerable<PendingPlacementDto>>>
{
    private const int PlacementWindowDays       = 30;
    private const int CorrectionWindowHours     = 72;
    private const int MaxPlacementOpportunities = 2;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public GetPendingPlacementsHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public async Task<Result<IEnumerable<PendingPlacementDto>>> Handle(
        GetPendingPlacementsQuery request, CancellationToken ct)
    {
        var sponsorMemberId = _currentUser.MemberId;
        var now             = _clock.UtcNow;
        var windowCutoff    = now.AddDays(-PlacementWindowDays);

        // All members directly sponsored (enrolled) by this ambassador
        // enrolled within the last 30 days (or already placed but within correction window)
        var enrolledMembers = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == sponsorMemberId
                     && !m.IsDeleted
                     && m.EnrollDate >= windowCutoff)
            .ToListAsync(ct);

        if (!enrolledMembers.Any())
            return Result<IEnumerable<PendingPlacementDto>>.Success(Enumerable.Empty<PendingPlacementDto>());

        var memberIds = enrolledMembers.Select(m => m.MemberId).ToList();

        // Their current dual-team nodes (if placed)
        var dualNodes = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => memberIds.Contains(d.MemberId))
            .ToDictionaryAsync(d => d.MemberId, ct);

        // Latest placement log per member (for opportunity count and correction window)
        var placementLogs = await _db.PlacementLogs
            .AsNoTracking()
            .Where(p => memberIds.Contains(p.MemberId))
            .GroupBy(p => p.MemberId)
            .Select(g => g.OrderByDescending(p => p.CreationDate).First())
            .ToListAsync(ct);

        var logByMember = placementLogs.ToDictionary(p => p.MemberId);

        // Parent member names for display
        var parentIds = dualNodes.Values
            .Where(n => n.ParentMemberId != null)
            .Select(n => n.ParentMemberId!)
            .Distinct()
            .ToList();

        var parentNames = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => parentIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId, m => $"{m.FirstName} {m.LastName}", ct);

        var result = enrolledMembers.Select(member =>
        {
            dualNodes.TryGetValue(member.MemberId, out var node);
            logByMember.TryGetValue(member.MemberId, out var log);

            var isPlaced            = node != null;
            var opportunitiesUsed   = log?.UnplacementCount ?? 0;
            var firstPlacedAt       = log?.FirstPlacementDate;
            var isBlocked           = opportunitiesUsed >= MaxPlacementOpportunities;
            var daysRemaining       = (int)(member.EnrollDate.AddDays(PlacementWindowDays) - now).TotalDays;
            var isWindowExpired     = daysRemaining <= 0;
            var windowPercentUsed   = Math.Min(100, Math.Max(0,
                ((now - member.EnrollDate).TotalDays / PlacementWindowDays) * 100));

            // Correction window: 72h after the most recent placement
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

            parentNames.TryGetValue(node?.ParentMemberId ?? "", out var parentName);

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

            return new PendingPlacementDto
            {
                MemberId                    = member.MemberId,
                FullName                    = $"{member.FirstName} {member.LastName}",
                MemberCode                  = member.MemberId,
                PhotoUrl                    = member.ProfilePhotoUrl,
                EnrolledAt                  = member.EnrollDate,
                DaysRemainingInWindow       = Math.Max(0, daysRemaining),
                WindowPercentUsed           = windowPercentUsed,
                PlacementOpportunitiesUsed  = opportunitiesUsed,
                IsAlreadyPlaced             = isPlaced,
                PlacedAt                    = firstPlacedAt,
                CanCorrect                  = canCorrect,
                CorrectionHoursRemaining    = Math.Round(correctionHoursLeft, 1),
                CurrentTreeSide             = node?.Side.ToString(),
                CurrentParentMemberId       = node?.ParentMemberId,
                CurrentParentFullName       = parentName,
                IsWindowExpired             = isWindowExpired,
                IsBlocked                   = isBlocked,
                PlacementStatus             = status
            };
        })
        .OrderBy(d => d.IsBlocked)
        .ThenBy(d => d.DaysRemainingInWindow)
        .ToList();

        return Result<IEnumerable<PendingPlacementDto>>.Success(result);
    }
}
