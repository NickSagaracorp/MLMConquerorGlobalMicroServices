using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

public enum EntraxCallbackOutcome
{
    /// <summary>200: procesado o ya resuelto. Entrax deja de reintentar.</summary>
    Accepted,
    /// <summary>401: sin firma, firma mala o sin secreto con qué comprobarla.</summary>
    Unauthorized,
    /// <summary>400: cuerpo ilegible o que no corresponde al pago.</summary>
    Invalid,
    /// <summary>400: timestamp fuera de la tolerancia.</summary>
    Stale,
    /// <summary>404: el pago no es nuestro o todavía no existe. Entrax reintenta.</summary>
    NotFound,
    /// <summary>503: no se pudo resolver ahora (Entrax caído o aún "pending"). Entrax reintenta.</summary>
    RetryLater
}

public interface IEntraxCallbackProcessor
{
    Task<EntraxCallbackOutcome> ProcessAsync(byte[] rawBody, string? signature, CancellationToken ct);
}

/// <summary>
/// El callback de Entrax. Lo que el legado hacía mal y aquí no: la firma se compara en tiempo
/// constante sobre el cuerpo crudo; sin firma (Entrax la omite si no tiene clave) se rechaza; nunca
/// se devuelve 200 sin haber procesado (para que Entrax reintente si fallamos); y el cuerpo del aviso
/// no decide nada: solo dice QUÉ pago mirar, y EntraxSignupSettlement vuelve a preguntar a Entrax.
///
/// La comprobación del timestamp usa UtcNow porque el campo es un unix UTC del protocolo.
/// </summary>
public sealed class EntraxCallbackProcessor : IEntraxCallbackProcessor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly AppDbContext                       _db;
    private readonly IEntraxSignupSettlement            _settlement;
    private readonly EntraxOptions                      _options;
    private readonly IDateTimeProvider                  _clock;
    private readonly ILogger<EntraxCallbackProcessor>   _logger;

    public EntraxCallbackProcessor(
        AppDbContext db, IEntraxSignupSettlement settlement, EntraxOptions options,
        IDateTimeProvider clock, ILogger<EntraxCallbackProcessor> logger)
    {
        _db         = db;
        _settlement = settlement;
        _options    = options;
        _clock      = clock;
        _logger     = logger;
    }

    public async Task<EntraxCallbackOutcome> ProcessAsync(byte[] rawBody, string? signature, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.Secret))
        {
            _logger.LogError("Entrax callback rechazado: no hay Entrax:Secret con el que comprobar la firma.");
            return EntraxCallbackOutcome.Unauthorized;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Entrax callback rechazado: llegó sin X-Signature.");
            return EntraxCallbackOutcome.Unauthorized;
        }

        if (!EntraxSignature.VerifyCallback(rawBody, signature, _options.Secret))
        {
            _logger.LogWarning("Entrax callback rechazado: la firma no es válida.");
            return EntraxCallbackOutcome.Unauthorized;
        }

        Payload? payload;
        try { payload = JsonSerializer.Deserialize<Payload>(rawBody, Json); }
        catch (JsonException) { return EntraxCallbackOutcome.Invalid; }

        var paymentId = EntraxGateway.NormalizePaymentId(payload?.PaymentId);
        if (payload is null || paymentId is null)
            return EntraxCallbackOutcome.Invalid;

        var sent = ToInstant(payload.Timestamp);
        var utcNow = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        if (sent is null || (utcNow - sent.Value).Duration() > _options.CallbackTolerance)
        {
            _logger.LogWarning("Entrax callback del pago {PaymentId} rechazado por antigüedad.", paymentId);
            return EntraxCallbackOutcome.Stale;
        }

        var now = _clock.Now;
        var payment = await _db.EntraxPayments.FirstOrDefaultAsync(p => p.ExternalPaymentId == paymentId, ct);
        if (payment is null && !string.IsNullOrWhiteSpace(payload.OrderId))
        {
            // El QR se emitió pero no llegamos a guardar su paymentId: nuestro orderId lo identifica.
            payment = await _db.EntraxPayments.FirstOrDefaultAsync(
                p => p.ExternalOrderId == payload.OrderId && p.ExternalPaymentId == null, ct);
            if (payment is not null)
            {
                payment.ExternalPaymentId = paymentId;
                payment.LastUpdateDate    = now;
                payment.LastUpdateBy      = EntraxGateway.Name;
            }
        }

        if (payment is null)
        {
            _logger.LogWarning("Entrax callback de un pago que no es nuestro o aún no existe: {PaymentId}.", paymentId);
            return EntraxCallbackOutcome.NotFound;
        }

        if (!string.IsNullOrWhiteSpace(payload.OrderId) && payload.OrderId != payment.ExternalOrderId)
        {
            _logger.LogWarning("Entrax callback del pago {PaymentId} con un orderId que no es el suyo.", paymentId);
            return EntraxCallbackOutcome.Invalid;
        }

        _db.EntraxApiCalls.Add(EntraxCallLog.From(EntraxCallLog.Callback, 0, null, 0,
            payment.ExternalOrderId, paymentId, payload.Status, now, $"{EntraxGateway.Name}:Webhook"));
        await _db.SaveChangesAsync(CancellationToken.None);

        var outcome = await _settlement.SettleAsync(payment.Id, EntraxSettlementSource.Webhook, ct);
        return outcome switch
        {
            EntraxSettlementOutcome.Activated
                or EntraxSettlementOutcome.AlreadyResolved
                or EntraxSettlementOutcome.Failed
                or EntraxSettlementOutcome.Expired
                or EntraxSettlementOutcome.ToReview => EntraxCallbackOutcome.Accepted,
            EntraxSettlementOutcome.NotFound => EntraxCallbackOutcome.NotFound,
            _ => EntraxCallbackOutcome.RetryLater
        };
    }

    /// <summary>Unix UTC en segundos; por encima de 10¹¹ se entiende en milisegundos.</summary>
    private static DateTimeOffset? ToInstant(long timestamp)
    {
        if (timestamp <= 0) return null;
        try
        {
            return timestamp > 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                : DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private sealed class Payload
    {
        public string? PaymentId { get; set; }
        public string? OrderId   { get; set; }
        public string? Status    { get; set; }
        public long    Timestamp { get; set; }
    }
}
