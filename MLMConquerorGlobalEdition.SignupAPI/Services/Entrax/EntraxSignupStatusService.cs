using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

public interface IEntraxSignupStatusService
{
    /// <summary>Estado del pago para la pantalla del alta, o null si las dos referencias no casan.</summary>
    Task<EntraxSignupStatusDto?> GetAsync(string signupId, string? paymentRef, CancellationToken ct);

    SignupPaymentOptionsDto GetPaymentOptions();
}

/// <summary>
/// Lo que consulta la pantalla del QR. Anónimo (la cuenta aún no puede iniciar sesión), así que:
/// exige el id del alta Y el de nuestro pago (GUID aleatorio), y solo devuelve estado y tiempo.
/// Mientras el pago espera, y si hace más de Entrax:PollThrottleSeconds de la última consulta,
/// lo liquida por el camino único. Los fallos de la liquidación no se le enseñan al navegador.
/// </summary>
public sealed class EntraxSignupStatusService : IEntraxSignupStatusService
{
    private readonly AppDbContext                         _db;
    private readonly IEntraxSignupSettlement              _settlement;
    private readonly EntraxOptions                        _options;
    private readonly IDateTimeProvider                    _clock;
    private readonly ILogger<EntraxSignupStatusService>   _logger;
    private readonly IConfiguration?                      _configuration;

    public EntraxSignupStatusService(
        AppDbContext db, IEntraxSignupSettlement settlement, EntraxOptions options,
        IDateTimeProvider clock, ILogger<EntraxSignupStatusService> logger,
        // Sin configuración la tarjeta se anuncia apagada. El contenedor inyecta IConfiguration.
        IConfiguration? configuration = null)
    {
        _db         = db;
        _settlement = settlement;
        _options    = options;
        _clock      = clock;
        _logger     = logger;
        _configuration = configuration;
    }

    public async Task<EntraxSignupStatusDto?> GetAsync(string signupId, string? paymentRef, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(signupId) || string.IsNullOrWhiteSpace(paymentRef))
            return null;

        var payment = await ReadAsync(signupId, paymentRef, ct);
        if (payment is null)
            return null;

        var now = _clock.Now;

        // El alta ya se completó por otro pago (otro QR, cripto, tarjeta): este QR no se paga. La pantalla
        // lo oculta; no se pregunta a Entrax (si alguien lo paga igual, el barrido lo deja a revisar).
        if (payment.Status is EntraxPaymentStatus.AwaitingPayment or EntraxPaymentStatus.Superseded
            && await _db.Orders.AsNoTracking().AnyAsync(o => o.Id == signupId && o.Status == OrderStatus.Completed, ct))
        {
            return new EntraxSignupStatusDto
            {
                Status           = EntraxSignupStatuses.Superseded,
                ExpiresAt        = payment.ExpiresAt,
                SecondsRemaining = 0
            };
        }

        if (payment.Status == EntraxPaymentStatus.AwaitingPayment
            && payment.ExternalPaymentId is not null
            && (payment.LastCheckedAt is null || now - payment.LastCheckedAt.Value >= _options.PollThrottle)
            && await TryReservePollAsync(payment, now, ct))
        {
            try
            {
                await _settlement.SettleAsync(payment.Id, EntraxSettlementSource.Poll, ct);
                payment = await ReadAsync(signupId, paymentRef, ct) ?? payment;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Entrax: falló la liquidación del pago {EntraxPaymentId} desde la pantalla.", payment.Id);
            }
        }

        return new EntraxSignupStatusDto
        {
            Status           = payment.Status.ToString(),
            ExpiresAt        = payment.ExpiresAt,
            SecondsRemaining = payment.Status == EntraxPaymentStatus.AwaitingPayment
                ? EntraxGateway.SecondsUntil(payment.ExpiresAt, now)
                : 0
        };
    }

    /// <summary>
    /// Reserva el turno de consulta a Entrax con un UPDATE condicional sobre LastCheckedAt: con dos
    /// pestañas o dos peticiones en el mismo instante, solo una pregunta (Entrax limita a 100/min por IP).
    /// </summary>
    private async Task<bool> TryReservePollAsync(EntraxPayment payment, DateTime now, CancellationToken ct)
    {
        if (!_db.Database.IsRelational())
            return true;   // EF InMemory (pruebas): ya se decidió con lo leído.

        var limit = now - _options.PollThrottle;
        var affected = await _db.EntraxPayments
            .Where(p => p.Id == payment.Id
                     && p.Status == EntraxPaymentStatus.AwaitingPayment
                     && (p.LastCheckedAt == null || p.LastCheckedAt <= limit))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastCheckedAt, now), ct);
        return affected == 1;
    }

    public SignupPaymentOptionsDto GetPaymentOptions()
        => new()
        {
            EntraxCountries = _options.Enabled ? _options.EffectiveCountries : [],
            CardEnabled     = SignupCardPayments.IsEnabled(_configuration)
        };

    private Task<EntraxPayment?> ReadAsync(string signupId, string paymentRef, CancellationToken ct)
        => _db.EntraxPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentRef && p.OrderId == signupId, ct);
}
