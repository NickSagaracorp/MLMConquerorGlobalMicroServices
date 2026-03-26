using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// HangFire recurring job — Daily 5:00 AM UTC.
/// Marks TokenTransaction records as Expired when the global validity window
/// has passed and the token has never been used, distributed, or already expired.
///
/// Config key: Tokens:ValidityDays (default: 365).
/// Idempotent: tokens already in Expired/Used/Distributed state are skipped.
/// </summary>
public class ExpiredTokenCleanupJob
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly IConfiguration _config;
    private readonly ILogger<ExpiredTokenCleanupJob> _logger;

    public ExpiredTokenCleanupJob(
        AppDbContext db,
        IDateTimeProvider dateTime,
        IConfiguration config,
        ILogger<ExpiredTokenCleanupJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _config   = config;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = _dateTime.Now;
        _logger.LogInformation("ExpiredTokenCleanupJob: starting at {Now}.", now);

        var validityDays = _config.GetValue("Tokens:ValidityDays", 365);
        var expiryThreshold = now.AddDays(-validityDays);

        var expiredTokens = await _db.TokenTransactions
            .Where(t => t.TransactionType != TokenTransactionType.Used
                     && t.TransactionType != TokenTransactionType.Distributed
                     && t.TransactionType != TokenTransactionType.Expired
                     && t.CreationDate <= expiryThreshold)
            .ToListAsync(ct);

        if (expiredTokens.Count == 0)
        {
            _logger.LogInformation("ExpiredTokenCleanupJob: no tokens to expire.");
            return;
        }

        foreach (var token in expiredTokens)
            token.TransactionType = TokenTransactionType.Expired;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ExpiredTokenCleanupJob: expired {Count} tokens at {Now}.", expiredTokens.Count, now);
    }
}
