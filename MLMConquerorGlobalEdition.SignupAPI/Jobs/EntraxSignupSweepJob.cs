using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Jobs;

/// <summary>
/// HangFire, cada 2 minutos, cola "signups": liquida los pagos de Entrax del alta que nadie más
/// liquidó — el callback no llegó (o se rechazó) y la pantalla se cerró —, y re-mira los QR que
/// caducaron hace menos de Entrax:LatePaymentWindowMinutes por si se pagaron tarde.
///
/// Como mucho Entrax:SweepBatchSize por pasada, los menos consultados primero: Entrax limita a 100
/// peticiones por minuto e IP. Corre aunque Entrax esté apagado: si se apaga con pagos en vuelo,
/// esos pagos tienen que acabar en algún sitio.
///
/// Cada pago se liquida en su propio scope (su propio DbContext): si una liquidación revienta a
/// medias, lo que dejó marcado en su contexto no puede guardarse con la del pago siguiente.
/// </summary>
[Queue("signups")]
public class EntraxSignupSweepJob
{
    public const string RecurringJobId = "entrax-signup-sweep";
    public const string Cron           = "*/2 * * * *";

    private readonly AppDbContext                      _db;
    private readonly IServiceScopeFactory              _scopes;
    private readonly EntraxOptions                     _options;
    private readonly IDateTimeProvider                 _clock;
    private readonly ILogger<EntraxSignupSweepJob>     _logger;

    public EntraxSignupSweepJob(
        AppDbContext db, IServiceScopeFactory scopes, EntraxOptions options,
        IDateTimeProvider clock, ILogger<EntraxSignupSweepJob> logger)
    {
        _db      = db;
        _scopes  = scopes;
        _options = options;
        _clock   = clock;
        _logger  = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now      = _clock.Now;
        var lateFrom = now - _options.LatePaymentWindow;

        var ids = await _db.EntraxPayments.AsNoTracking()
            .Where(p => p.Status == EntraxPaymentStatus.AwaitingPayment
                     || (p.Status == EntraxPaymentStatus.Expired && p.ExpiresAt != null && p.ExpiresAt >= lateFrom))
            .OrderBy(p => p.LastCheckedAt ?? DateTime.MinValue)
            .ThenBy(p => p.CreationDate)
            .Select(p => p.Id)
            .Take(Math.Max(1, _options.SweepBatchSize))
            .ToListAsync(ct);

        if (!_options.Enabled)
        {
            var waiting = await _db.EntraxPayments.AsNoTracking()
                .CountAsync(p => p.Status == EntraxPaymentStatus.AwaitingPayment, ct);
            if (waiting > 0)
                _logger.LogCritical(
                    "Entrax está apagado y hay {Waiting} pagos del alta esperando. El barrido los sigue liquidando: " +
                    "no quites Entrax:KeyId ni Entrax:Secret (apagar es dejar de ofrecerlo, no quitar credenciales).",
                    waiting);
        }

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var settlement = scope.ServiceProvider.GetRequiredService<IEntraxSignupSettlement>();
                await settlement.SettleAsync(id, EntraxSettlementSource.Sweep, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Entrax: el barrido no pudo liquidar el pago {EntraxPaymentId}; se reintentará.", id);
            }
        }
    }
}
