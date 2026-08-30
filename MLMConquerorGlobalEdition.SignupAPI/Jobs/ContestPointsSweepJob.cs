using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.SignupAPI.Jobs;

/// <summary>
/// HangFire recurring job — every 10 minutes.
/// Awards contest points to the sponsor + every enrollment-tree upline of
/// each new VIP / Elite / Turbo signup whose order completed inside an
/// active contest window. Mirrors the lookback / idempotency model of
/// <c>BuilderBonusSweepJob</c>:
///   • Lookback: 7 days. Late-completed orders (e.g. payment retry) still
///     get their points awarded as long as the contest is still in window.
///   • Idempotency: <c>(ContestId, SourceOrderId, BeneficiaryMemberId)</c>
///     unique index prevents double credits on re-run.
///
/// Points per tier mirror the comp plan (VIP=1, Elite=6, Turbo=6). When the
/// product mix on a single order spans tiers we use the highest level seen,
/// matching how <c>BuilderBonusSweepJob</c> resolves its level.
/// </summary>
[Queue("signups")]
public class ContestPointsSweepJob
{
    private const int LookbackDays = 7;

    /// <summary>
    /// VIP=1 / Elite=6 / Turbo=6. Lifestyle (1) + any unlisted level
    /// contribute zero points. Mirrors the comp-plan constants in
    /// <c>CommissionsService.LevelPoints</c>.
    /// </summary>
    private static readonly Dictionary<int, int> ContestPointsByLevel = new()
    {
        [2] = 1,   // VIP
        [3] = 6,   // Elite
        [4] = 6    // Turbo
    };

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<ContestPointsSweepJob> _logger;

    public ContestPointsSweepJob(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<ContestPointsSweepJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now   = _dateTime.Now;
        var since = now.AddDays(-LookbackDays);

        // Active contests whose window overlaps the sweep lookback window.
        // A finished contest still picks up late-completed orders that fall
        // inside its own [StartDate, EndDate] — leaderboard accuracy beats
        // a strict "current-time only" cutoff for analytics.
        var contests = await _db.CorporateContests.AsNoTracking()
            .Where(c => c.IsActive && !c.IsDeleted
                     && c.StartDate <= now
                     && c.EndDate   >= since)
            .Select(c => new { c.Id, c.StartDate, c.EndDate })
            .ToListAsync(ct);

        if (contests.Count == 0) return;

        // Per-order membership level — same shared query BuilderBonusSweep uses.
        var orderLevels = await _db.GetHighestMembershipLevelIdsByOrderAsync(
            ContestPointsByLevel.Keys.ToArray(), ct);

        if (orderLevels.Count == 0) return;

        var earliestStart = contests.Min(c => c.StartDate);
        var since2        = since < earliestStart ? earliestStart : since;

        var candidates = await (
            from o  in _db.Orders.AsNoTracking()
            join mp in _db.MemberProfiles.AsNoTracking() on o.MemberId equals mp.MemberId
            where o.Status == OrderStatus.Completed
               && o.CreationDate >= since2
               && mp.SponsorMemberId != null
               && orderLevels.Keys.Contains(o.Id)
            select new
            {
                OrderId           = o.Id,
                NewMemberId       = o.MemberId,
                SponsorMemberId   = mp.SponsorMemberId!,
                OrderCreationDate = o.CreationDate
            }
        ).ToListAsync(ct);

        if (candidates.Count == 0) return;

        // Genealogy chain (closest-first) for every distinct sponsor in the
        // candidate set. Same path-parsing trick BuilderBonusSweep uses.
        var sponsorIds = candidates.Select(c => c.SponsorMemberId).Distinct().ToList();
        var paths = await _db.GenealogyTree.AsNoTracking()
            .Where(g => sponsorIds.Contains(g.MemberId))
            .Select(g => new { g.MemberId, g.HierarchyPath })
            .ToDictionaryAsync(g => g.MemberId, g => g.HierarchyPath, ct);

        var chains = new Dictionary<string, List<string>>(sponsorIds.Count);
        foreach (var sid in sponsorIds)
        {
            var chain = new List<string> { sid }; // index 0 = direct sponsor
            if (paths.TryGetValue(sid, out var path) && !string.IsNullOrEmpty(path))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                // Drop self (last) then reverse so closest-first.
                chain.AddRange(segments.SkipLast(1).Reverse());
            }
            chains[sid] = chain;
        }

        // Existing earnings for the candidate set — same shape as the unique
        // index. We hash them up-front to skip already-awarded slots cheaply
        // without one round-trip per row.
        var orderIds   = candidates.Select(c => c.OrderId).ToList();
        var contestIds = contests.Select(c => c.Id).ToList();
        var existing   = await _db.CorporateContestEarnings.AsNoTracking()
            .Where(e => contestIds.Contains(e.ContestId) && orderIds.Contains(e.SourceOrderId))
            .Select(e => new { e.ContestId, e.SourceOrderId, e.BeneficiaryMemberId })
            .ToListAsync(ct);
        var existingSet = existing
            .Select(e => Key(e.ContestId, e.SourceOrderId, e.BeneficiaryMemberId))
            .ToHashSet();

        int inserted = 0;

        foreach (var contest in contests)
        {
            foreach (var c in candidates)
            {
                // Award only when the order's creation date sits inside the
                // contest window. A 7-day lookback can still surface orders
                // older than a contest that ended yesterday — those get
                // skipped here.
                if (c.OrderCreationDate < contest.StartDate
                 || c.OrderCreationDate > contest.EndDate) continue;

                if (!orderLevels.TryGetValue(c.OrderId, out var levelId)) continue;
                if (!ContestPointsByLevel.TryGetValue(levelId, out var points)) continue;
                if (points <= 0) continue;
                if (!chains.TryGetValue(c.SponsorMemberId, out var chain)) continue;

                for (var i = 0; i < chain.Count; i++)
                {
                    var memberId = chain[i];
                    var k = Key(contest.Id, c.OrderId, memberId);
                    if (existingSet.Contains(k)) continue;

                    _db.CorporateContestEarnings.Add(new CorporateContestEarning
                    {
                        ContestId           = contest.Id,
                        BeneficiaryMemberId = memberId,
                        SourceMemberId      = c.NewMemberId,
                        SourceOrderId       = c.OrderId,
                        Level               = i,
                        Points              = points,
                        MembershipLevelId   = levelId,
                        EarnedDate          = c.OrderCreationDate,
                        CreationDate        = now,
                        CreatedBy           = $"contest-points-sweep · {now:yyyy-MM-dd HH:mm}"
                    });
                    existingSet.Add(k);
                    inserted++;
                }
            }
        }

        if (inserted > 0)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "ContestPointsSweep: inserted {Count} earnings across {Contests} active contests.",
                    inserted, contests.Count);
            }
            catch (DbUpdateException ex)
            {
                // The unique index is the last line of defence — if a parallel
                // sweep runs the indexed insert wins and we log the conflict
                // rather than crashing the whole job.
                _logger.LogWarning(ex,
                    "ContestPointsSweep: unique-index conflict on save, partial insert skipped.");
                _db.ChangeTracker.Clear();
            }
        }
    }

    private static string Key(string contestId, string orderId, string memberId)
        => $"{contestId}|{orderId}|{memberId}";
}
