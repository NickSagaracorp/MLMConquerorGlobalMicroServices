using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IEmailService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public class PayoutReceiptService : IPayoutReceiptService
{
    public const string AutoSendKey = "PayoutReceiptEmailAutoSend";

    private readonly AppDbContext _db;
    private readonly IReceiptPdfRenderer _renderer;
    private readonly IReceiptStorage _storage;
    private readonly IEmailService _email;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<PayoutReceiptService> _logger;

    public PayoutReceiptService(
        AppDbContext db, IReceiptPdfRenderer renderer, IReceiptStorage storage,
        IEmailService email, IDateTimeProvider dateTime, ILogger<PayoutReceiptService> logger)
    {
        _db = db; _renderer = renderer; _storage = storage;
        _email = email; _dateTime = dateTime; _logger = logger;
    }

    public async Task IssueReceiptAsync(PayoutAttempt attempt, CancellationToken ct = default)
    {
        try
        {
            await EnsureReceiptAsync(attempt, ct);
            if (await IsAutoSendOnAsync(ct))
                await SendEmailAsync(attempt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Payout receipt issuance failed for attempt {AttemptId} (payout already settled).", attempt.Id);
        }
    }

    public async Task<bool> ResendReceiptAsync(PayoutAttempt attempt, CancellationToken ct = default)
    {
        await EnsureReceiptAsync(attempt, ct);
        return await SendEmailAsync(attempt, ct);
    }

    private async Task EnsureReceiptAsync(PayoutAttempt attempt, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(attempt.ReceiptUrl) && !string.IsNullOrEmpty(attempt.ReceiptSha256))
            return; // already issued

        var earnings = await _db.PayoutAttemptEarnings
            .Where(x => x.PayoutAttemptId == attempt.Id)
            .Select(x => new ReceiptEarningLine(x.CommissionEarningId, x.Amount))
            .ToListAsync(ct);

        var fullName = await _db.MemberProfiles
            .Where(m => m.MemberId == attempt.MemberId)
            .Select(m => (m.FirstName + " " + m.LastName).Trim())
            .FirstOrDefaultAsync(ct) ?? attempt.MemberId;

        var data = new PayoutReceiptData(
            attempt.Id, attempt.MemberId, fullName, attempt.WalletTypeSnapshot,
            attempt.PayoutAccountSnapshot, attempt.AmountUsd, attempt.ProcessDateUtc,
            attempt.CompletedAtUtc ?? _dateTime.Now, attempt.GatewayTransactionId, earnings);

        var bytes = _renderer.Render(data);
        var fileName = PayoutReceiptFileNaming.Build(attempt.Id, attempt.MemberId);
        var url = await _storage.SaveAsync(fileName, bytes, ct);

        attempt.ReceiptUrl = url;
        attempt.ReceiptSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        attempt.LastUpdateDate = _dateTime.Now;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> SendEmailAsync(PayoutAttempt attempt, CancellationToken ct)
    {
        var member = await _db.MemberProfiles
            .Where(m => m.MemberId == attempt.MemberId)
            .Select(m => new { m.Email, m.FirstName, m.LastName, m.DefaultLanguage })
            .FirstOrDefaultAsync(ct);
        if (member is null || string.IsNullOrWhiteSpace(member.Email)) return false;

        var fullName = $"{member.FirstName} {member.LastName}".Trim();
        await _email.SendAsync(member.Email, fullName, member.DefaultLanguage,
            NotificationEvents.PayoutReceiptIssued,
            new Dictionary<string, string>
            {
                ["FullName"] = fullName,
                ["AmountUsd"] = attempt.AmountUsd.ToString("F2"),
                ["Gateway"] = attempt.WalletTypeSnapshot.ToString(),
                ["ProcessDate"] = attempt.ProcessDateUtc.ToString("yyyy-MM-dd"),
                ["ReceiptUrl"] = attempt.ReceiptUrl ?? string.Empty
            }, ct);
        return true;
    }

    private async Task<bool> IsAutoSendOnAsync(CancellationToken ct)
    {
        var p = await _db.GlobalParameters.AsNoTracking().FirstOrDefaultAsync(x => x.Key == AutoSendKey, ct);
        return p is null || !bool.TryParse(p.Value, out var on) || on; // default ON
    }
}
