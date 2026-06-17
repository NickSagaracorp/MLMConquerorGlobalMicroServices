using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public class FraudFingerprintService : IFraudFingerprintService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<FraudFingerprintService> _logger;
    private readonly int _windowHours;
    private readonly int _threshold;

    public FraudFingerprintService(
        AppDbContext db,
        IDateTimeProvider dateTime,
        IConfiguration config,
        ILogger<FraudFingerprintService> logger)
    {
        _db = db;
        _dateTime = dateTime;
        _logger = logger;
        // Defaults: 3 attempts in 24 hours from the same fingerprint trigger a flag.
        // Explicit parsing avoids surprises from IConfiguration binding nullables.
        _windowHours = int.TryParse(config["FraudGuard:WindowHours"], out var win) && win > 0 ? win : 24;
        _threshold   = int.TryParse(config["FraudGuard:DuplicateThreshold"], out var thr) && thr > 0 ? thr : 3;
    }

    public async Task<FingerprintCaptureResult> RecordAsync(
        string? visitorId,
        SignupRiskFlow flow,
        string? sponsorReplicateSite,
        string? ipAddress,
        string? userAgent,
        string? orderId,
        string? memberId,
        CancellationToken ct)
    {
        var now      = _dateTime.Now;
        var windowAt = now.AddHours(-_windowHours);

        // VisitorId may be null/empty if the OSS lib failed to load. Treat as "unknown" but
        // still record the event so we have a row keyed by IP + UA for correlation later.
        var safeVisitor = string.IsNullOrWhiteSpace(visitorId)
            ? $"unknown-{Guid.NewGuid():N}"
            : visitorId.Trim();

        var entity = new SignupRiskFingerprint
        {
            VisitorId            = Truncate(safeVisitor, 100),
            RequestId            = Guid.NewGuid().ToString("N"),  // Pro replaces this with its own
            Flow                 = flow,
            SponsorReplicateSite = Truncate(sponsorReplicateSite, 100),
            IpAddress            = Truncate(ipAddress,   45),
            UserAgent            = Truncate(userAgent,  500),
            OrderId              = Truncate(orderId,    36),
            MemberId             = Truncate(memberId,   36),
            CreatedBy            = memberId ?? "anonymous-signup",
            CreationDate         = now
        };

        // Threshold check: count prior events (any flow) for this visitorId within window.
        // Cleared rows are excluded — admin has manually verified the visitor is legitimate.
        var dupCount = string.IsNullOrEmpty(visitorId)
            ? 0
            : await _db.SignupRiskFingerprints
                .AsNoTracking()
                .CountAsync(f => f.VisitorId == safeVisitor && f.CreationDate >= windowAt && !f.Cleared, ct);

        if (dupCount + 1 >= _threshold)
        {
            entity.IsFlagged  = true;
            entity.FlagReason = $"DUP_VISITOR_{_threshold}_IN_{_windowHours}H";
            _logger.LogWarning(
                "Fingerprint flagged: visitor={Visitor} ip={Ip} count={Count} threshold={Threshold}",
                safeVisitor, ipAddress, dupCount + 1, _threshold);
        }

        await _db.SignupRiskFingerprints.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return new FingerprintCaptureResult(entity.IsFlagged, entity.FlagReason, entity.Id);
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
