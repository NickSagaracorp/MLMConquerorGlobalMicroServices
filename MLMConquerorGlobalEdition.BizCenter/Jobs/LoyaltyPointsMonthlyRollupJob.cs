using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// HangFire recurring job — Monthly 1st 2:30 AM UTC.
/// Unlocks LoyaltyPoints records where:
///   - IsLocked = true
///   - MissedPayment = false
///   - NumberOfSuccessPayments >= ProductLoyaltyPointsSetting.RequiredSuccessfulPayments
/// Idempotent: already-unlocked records are skipped.
/// </summary>
public class LoyaltyPointsMonthlyRollupJob
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<LoyaltyPointsMonthlyRollupJob> _logger;

    public LoyaltyPointsMonthlyRollupJob(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<LoyaltyPointsMonthlyRollupJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = _dateTime.Now;
        _logger.LogInformation("LoyaltyPointsMonthlyRollupJob: starting at {Now}.", now);

        // Load threshold per product
        var settings = await _db.ProductLoyaltySettings
            .AsNoTracking()
            .Where(s => s.IsActive)
            .ToDictionaryAsync(s => s.ProductId, s => s.RequiredSuccessfulPayments, ct);

        var locked = await _db.LoyaltyPoints
            .Where(lp => lp.IsLocked && !lp.MissedPayment)
            .ToListAsync(ct);

        var unlocked = 0;

        foreach (var lp in locked)
        {
            if (!settings.TryGetValue(lp.ProductId, out var required)) continue;
            if (lp.NumberOfSuccessPayments < required) continue;

            lp.IsLocked   = false;
            lp.UnlockedAt = now;
            unlocked++;
        }

        if (unlocked > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "LoyaltyPointsMonthlyRollupJob: unlocked {Count} loyalty point records at {Now}.",
            unlocked, now);
    }
}
