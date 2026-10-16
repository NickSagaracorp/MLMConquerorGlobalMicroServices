using System.Collections.Concurrent;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Entrax de mentira para Development (Entrax:UseFake = true). Singleton en memoria: emite QR de
/// prueba y deja marcarlos pagados, pagados de menos, fallidos o caducados desde
/// POST /api/v1/dev/entrax/{paymentId}/{resultado} de SignupAPI. EntraxOptionsGuard impide
/// arrancar con él fuera de Development.
/// </summary>
public sealed class FakeEntraxClient : IEntraxClient
{
    public const decimal Rate = 95.50m;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>PNG de 1×1: la pantalla pinta algo; en Development se paga con el endpoint, no escaneando.</summary>
    public const string PlaceholderPng =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private sealed class Entry
    {
        public required string         OrderId   { get; init; }
        public required decimal        Amount    { get; init; }
        public required string         Currency  { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public string   Status       { get; set; } = "pending";
        public decimal? PaidOriginal { get; set; }
    }

    private readonly ConcurrentDictionary<string, Entry> _payments = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDateTimeProvider _clock;

    public FakeEntraxClient(IDateTimeProvider clock) => _clock = clock;

    private DateTimeOffset UtcNow => new(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));

    public Task<EntraxCall<EntraxQr>> CreateQrAsync(EntraxQrRequest request, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("D");
        var expires = UtcNow.Add(Lifetime);
        _payments[id] = new Entry
        {
            OrderId = request.OrderId, Amount = request.Amount, Currency = request.Currency, ExpiresAt = expires
        };

        var qr = new EntraxQr(id, PlaceholderPng, $"https://qr.nspk.ru/FAKE{id.Replace("-", string.Empty)}",
            Math.Round(request.Amount * Rate, 2), Rate, request.Currency, request.Amount, expires, request.OrderId);
        return Task.FromResult(EntraxCall<EntraxQr>.Success(qr, 200, 1));
    }

    public Task<EntraxCall<EntraxPaymentState>> GetPaymentAsync(string paymentId, CancellationToken ct)
    {
        if (!_payments.TryGetValue(paymentId, out var e))
            return Task.FromResult(EntraxCall<EntraxPaymentState>.Fail(404, "PAYMENT_NOT_FOUND", "Payment not found.", 1));

        var status   = e.Status == "pending" && UtcNow > e.ExpiresAt ? "expired" : e.Status;
        var original = e.PaidOriginal ?? e.Amount;
        var state = new EntraxPaymentState(paymentId, status, e.OrderId, original, e.Currency,
            Math.Round(original * Rate, 2), Rate, status == "paid" ? UtcNow : null);
        return Task.FromResult(EntraxCall<EntraxPaymentState>.Success(state, 200, 1));
    }

    public bool MarkPaid(string paymentId, decimal underpayBy = 0m)
    {
        if (!_payments.TryGetValue(paymentId, out var e)) return false;
        e.PaidOriginal = e.Amount - underpayBy;
        e.Status = "paid";
        return true;
    }

    public bool MarkFailed(string paymentId) => Set(paymentId, "failed");

    public bool MarkExpired(string paymentId) => Set(paymentId, "expired");

    private bool Set(string paymentId, string status)
    {
        if (!_payments.TryGetValue(paymentId, out var e)) return false;
        e.Status = status;
        return true;
    }
}
