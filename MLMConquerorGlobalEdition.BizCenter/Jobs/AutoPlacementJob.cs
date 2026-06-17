using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// HangFire recurring job — runs every 6 hours.
/// Auto-places ambassadors who have not received a manual placement
/// and whose 30-day placement window has elapsed.
///
/// Logic (placement-rules.md §5):
///   5.2.a  Sponsor has no children          → place on LEFT
///   5.2.b  Sponsor has only left child      → place on RIGHT
///   5.2.c  Sponsor has both children        → place on deepest available node
///          on the same side as the sponsor's position in their own upline.
///
/// Ghost points are NOT transferred on placement.
/// </summary>
[Queue("bizcenter")]
public class AutoPlacementJob
{
    private const int PlacementWindowDays = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoPlacementJob> _logger;

    public AutoPlacementJob(
        IServiceScopeFactory      scopeFactory,
        ILogger<AutoPlacementJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public Task ExecuteAsync() => ExecuteAsync(ignoreWindow: false);

    /// <summary>
    /// <paramref name="ignoreWindow"/> = true bypasses the 30-day placement window —
    /// used by the admin "force run" endpoint to backfill placements for newly
    /// signed-up members who don't yet exist in the dual tree.
    /// </summary>
    public async Task<int> ExecuteAsync(bool ignoreWindow)
    {
        using var scope = _scopeFactory.CreateScope();
        var db        = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock     = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var placement = scope.ServiceProvider.GetRequiredService<IPlacementService>();
        var now       = clock.Now;

        // Members whose placement window has expired and are still unplaced
        var windowCutoff = now.AddDays(-PlacementWindowDays);

        var unplacedQuery = db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.SponsorMemberId != null);

        if (!ignoreWindow)
            unplacedQuery = unplacedQuery.Where(m => m.EnrollDate <= windowCutoff);

        // Exclude members already in the dual tree.
        var unplaced = await unplacedQuery
            .Where(m => !db.DualTeamTree.Any(d => d.MemberId == m.MemberId))
            .Select(m => new { m.MemberId, m.SponsorMemberId })
            .ToListAsync();

        if (unplaced.Count == 0)
        {
            _logger.LogInformation("AutoPlacementJob: no unplaced members found at {Now}", now);
            return 0;
        }

        // Single placement authority: deepest-chain spillover, idempotent + concurrency-safe,
        // O(1) slot finding via the frontier cache, and one deferred incremental leg-point pass.
        var pairs = unplaced.Select(m => (m.MemberId, m.SponsorMemberId!)).ToList();
        var result = await placement.PlaceBulkAsync(pairs);

        if (!result.IsSuccess)
        {
            _logger.LogError("AutoPlacementJob: bulk placement failed: {Error}", result.Error);
            return 0;
        }

        _logger.LogInformation(
            "AutoPlacementJob completed at {Now}. Placed: {Placed}, Skipped: {Skipped}, Failed: {Failed}",
            now, result.Value!.Placed, result.Value.Skipped, result.Value.Failed);
        return result.Value.Placed;
    }
}
