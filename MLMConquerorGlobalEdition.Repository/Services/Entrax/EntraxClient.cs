using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Cliente de la API Cash Register v1 de Entrax (HttpClient tipado, timeout 30 s en AddEntrax).
///
/// El Timestamp de la firma es UTC unix porque el protocolo lo exige (Entrax rechaza los que se
/// separan más de 5 minutos de su reloj); es el único uso de UtcNow aquí.
///
/// No registra cuerpos ni cabeceras: el log estructurado lo escribe quien llama (EntraxApiCalls).
/// </summary>
public sealed class EntraxClient : IEntraxClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient            _http;
    private readonly EntraxOptions         _options;
    private readonly IDateTimeProvider     _clock;
    private readonly ILogger<EntraxClient> _logger;

    public EntraxClient(HttpClient http, EntraxOptions options, IDateTimeProvider clock, ILogger<EntraxClient> logger)
    {
        _http    = http;
        _options = options;
        _clock   = clock;
        _logger  = logger;
    }

    private Uri BaseUri => new(_options.BaseUrl.TrimEnd('/') + "/");

    public Task<EntraxCall<EntraxQr>> CreateQrAsync(EntraxQrRequest r, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            amount      = r.Amount,
            currency    = r.Currency,
            orderId     = r.OrderId,
            description = r.Description,
            callbackUrl = r.CallbackUrl
        }, Json);

        return SendAsync<QrData, EntraxQr>(HttpMethod.Post, "qr", body, MapQr, ct);
    }

    public Task<EntraxCall<EntraxPaymentState>> GetPaymentAsync(string paymentId, CancellationToken ct)
    {
        if (!Guid.TryParse(paymentId, out var id))
            return Task.FromResult(EntraxCall<EntraxPaymentState>.Fail(0, EntraxErrorCodes.InvalidPaymentId, "paymentId no es un GUID.", 0));

        return SendAsync<StatusData, EntraxPaymentState>(HttpMethod.Get, $"payment/{id:D}", string.Empty, MapStatus, ct);
    }

    private async Task<EntraxCall<TOut>> SendAsync<TWire, TOut>(
        HttpMethod method, string relative, string body, Func<TWire, TOut?> map, CancellationToken ct)
        where TWire : class
        where TOut : class
    {
        var uri       = new Uri(BaseUri, relative);
        var timestamp = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var nonce     = Guid.NewGuid().ToString("N");
        var signature = EntraxSignature.Sign(
            EntraxSignature.Canonical(method.Method, uri.AbsolutePath, timestamp, nonce, body), _options.Secret ?? string.Empty);

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("Authorization",
            EntraxSignature.AuthorizationHeader(_options.KeyId ?? string.Empty, timestamp, nonce, signature));
        if (method != HttpMethod.Get)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var sw = Stopwatch.StartNew();
        HttpResponseMessage response;
        string text;
        try
        {
            response = await _http.SendAsync(request, ct);
            text     = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning("Entrax {Method} {Path}: sin respuesta ({Error}).", method.Method, uri.AbsolutePath, ex.GetType().Name);
            return EntraxCall<TOut>.Fail(0, EntraxErrorCodes.Transport, ex.GetType().Name, sw.ElapsedMilliseconds);
        }

        using (response)
        {
            var status = (int)response.StatusCode;

            Envelope<TWire>? envelope = null;
            try { envelope = JsonSerializer.Deserialize<Envelope<TWire>>(text, Json); }
            catch (JsonException) { }

            if (envelope is null)
                return EntraxCall<TOut>.Fail(status, EntraxErrorCodes.BadResponse, null, sw.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode || !envelope.Success || envelope.Data is null)
            {
                var code = envelope.Error?.Code ?? (response.IsSuccessStatusCode ? EntraxErrorCodes.BadResponse : $"HTTP_{status}");
                _logger.LogWarning("Entrax {Method} {Path}: {Status} {Code}.", method.Method, uri.AbsolutePath, status, code);
                return EntraxCall<TOut>.Fail(status, code, envelope.Error?.Message, sw.ElapsedMilliseconds);
            }

            var mapped = map(envelope.Data);
            return mapped is null
                ? EntraxCall<TOut>.Fail(status, EntraxErrorCodes.BadResponse, "Respuesta incompleta.", sw.ElapsedMilliseconds)
                : EntraxCall<TOut>.Success(mapped, status, sw.ElapsedMilliseconds);
        }
    }

    private static EntraxQr? MapQr(QrData d)
    {
        if (d.QrCode?.PaymentId is not { } id || !Guid.TryParse(id, out _) || d.Payment is null)
            return null;

        return new EntraxQr(id, d.QrCode.Image, d.QrCode.Link, d.Payment.AmountInRubles, d.Payment.ExchangeRate,
            d.Payment.Currency ?? string.Empty, d.Payment.OriginalAmount, d.Payment.ExpiresAt, d.Order?.OrderId);
    }

    private static EntraxPaymentState? MapStatus(StatusData d)
    {
        if (string.IsNullOrWhiteSpace(d.PaymentId) || string.IsNullOrWhiteSpace(d.Status))
            return null;

        return new EntraxPaymentState(d.PaymentId, d.Status, d.OrderId,
            d.Amount?.Original ?? 0m, d.Amount?.Currency ?? string.Empty,
            d.Amount?.Rubles ?? 0m, d.Amount?.ExchangeRate ?? 0m, d.Timestamps?.Completed);
    }

    // ── Forma del cable (documentación de Entrax, Cash Register API v1) ──────────────────────

    private sealed class Envelope<T>
    {
        public bool       Success { get; set; }
        public T?         Data    { get; set; }
        public ErrorBody? Error   { get; set; }
    }

    private sealed class ErrorBody
    {
        public string? Code    { get; set; }
        public string? Message { get; set; }
    }

    private sealed class QrData
    {
        public QrCodePart?  QrCode  { get; set; }
        public PaymentPart? Payment { get; set; }
        public OrderPart?   Order   { get; set; }
    }

    private sealed class QrCodePart
    {
        public string? Image     { get; set; }
        public string? Link      { get; set; }
        public string? PaymentId { get; set; }
    }

    private sealed class PaymentPart
    {
        public decimal         AmountInRubles { get; set; }
        public decimal         ExchangeRate   { get; set; }
        public string?         Currency       { get; set; }
        public decimal         OriginalAmount { get; set; }
        public DateTimeOffset? ExpiresAt      { get; set; }
    }

    private sealed class OrderPart
    {
        public string? OrderId { get; set; }
        public string? Status  { get; set; }
    }

    private sealed class StatusData
    {
        public string?         PaymentId  { get; set; }
        public string?         Status     { get; set; }
        public string?         OrderId    { get; set; }
        public AmountPart?     Amount     { get; set; }
        public TimestampsPart? Timestamps { get; set; }
    }

    private sealed class AmountPart
    {
        public decimal Original     { get; set; }
        public string? Currency     { get; set; }
        public decimal Rubles       { get; set; }
        public decimal ExchangeRate { get; set; }
    }

    private sealed class TimestampsPart
    {
        public DateTimeOffset? Created   { get; set; }
        public DateTimeOffset? Completed { get; set; }
    }
}
