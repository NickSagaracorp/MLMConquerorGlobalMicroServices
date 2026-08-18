using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Wallets;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Abre la cuenta del proveedor para los ambassadors que YA ganaron su primera comisión pero
/// todavía no tienen cuenta abierta.
///
/// POR QUÉ EXISTE
/// En i-Payout cada cuenta le cuesta dinero a la compañía. El signup le asigna a cada
/// ambassador la wallet por defecto de su país, pero NO abre la cuenta: si abriéramos una por
/// cada alta, se pagaría por miles de personas que nunca llegan a cobrar nada. La cuenta se
/// abre cuando aparece la primera comisión, que es la señal de que esa persona sí va a cobrar.
///
/// POR QUÉ AL REGISTRARSE LA COMISIÓN Y NO AL PAGARLA
/// i-Payout manda un mail de verificación y el ambassador tarda en responderlo. Si esperáramos
/// al día del pago, el primer payout fallaría por una cuenta sin verificar. Abriéndola cuando
/// se registra la comisión, hay días de margen antes de la corrida.
///
/// Quien configuró su método de cobro a mano desde el BizCenter o el admin ya tiene la cuenta
/// abierta en ese momento (ver MemberWalletService), así que este job no lo toca.
///
/// Es idempotente: sólo mira wallets con ProviderAccountCreatedAt nulo y marca la fecha al
/// terminar, así que re-ejecutarlo no vuelve a crear nada ni genera costo de más.
/// </summary>
[Queue("billing")]
public class DeferredPayoutAccountJob
{
    /// <summary>Tope por corrida. Evita que un backlog dispare miles de altas de golpe.</summary>
    private const int BatchSize = 200;

    private readonly AppDbContext                       _db;
    private readonly IPayoutAccountRegistrar            _registrar;
    private readonly ILogger<DeferredPayoutAccountJob>  _logger;

    public DeferredPayoutAccountJob(
        AppDbContext db,
        IPayoutAccountRegistrar registrar,
        ILogger<DeferredPayoutAccountJob> logger)
    {
        _db        = db;
        _registrar = registrar;
        _logger    = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // Candidatos: wallet sin cuenta abierta, de un gateway que sí requiere cuenta, y cuyo
        // dueño ya tiene al menos una comisión registrada (da igual si está pagada o pendiente
        // — lo que importa es que generó ingreso).
        var candidates = await (
            from w in _db.Wallets
            where !w.IsDeleted
                  && w.ProviderAccountCreatedAt == null
                  && w.WalletType != WalletType.Crypto
                  && _db.CommissionEarnings.Any(e => e.BeneficiaryMemberId == w.MemberId)
            join m in _db.MemberProfiles on w.MemberId equals m.MemberId into profiles
            from m in profiles.DefaultIfEmpty()
            orderby w.CreationDate
            select new
            {
                Wallet    = w,
                m.Email,
                m.FirstName,
                m.LastName
            })
            .Take(BatchSize)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            _logger.LogInformation("DeferredPayoutAccountJob: no wallets are waiting for a provider account.");
            return;
        }

        _logger.LogInformation(
            "DeferredPayoutAccountJob: {Count} wallet(s) have a first commission and no provider account yet.",
            candidates.Count);

        var opened = 0;
        var failed = 0;

        foreach (var c in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var wallet = c.Wallet;

            // En eWallet el UserID es el MemberId. Si está vacío —la wallet la sembró el
            // signup y nadie la configuró— se completa acá con el MemberId del dueño.
            var identifier = string.IsNullOrWhiteSpace(wallet.AccountIdentifier)
                ? wallet.MemberId
                : wallet.AccountIdentifier!;

            // Si el identificador apunta a OTRO miembro, la cuenta es de ese otro: alguien
            // más paga esa membresía y su eWallet ya existe. No se abre nada.
            if (!string.Equals(identifier, wallet.MemberId, StringComparison.OrdinalIgnoreCase)
                && wallet.WalletType == WalletType.eWallet)
            {
                _logger.LogInformation(
                    "Wallet {WalletId} points at another member's account ({Identifier}); nothing to open.",
                    wallet.Id, identifier);
                continue;
            }

            var result = await _registrar.RegisterAsync(new PayoutAccountRegistrationRequest
            {
                MemberId          = wallet.MemberId,
                WalletType        = wallet.WalletType,
                AccountIdentifier = identifier,
                Email             = c.Email,
                FirstName         = c.FirstName,
                LastName          = c.LastName
            }, ct);

            _db.WalletApiLogs.Add(new Domain.Entities.Wallet.MemberWalletApiLog
            {
                MemberId       = wallet.MemberId,
                WalletType     = wallet.WalletType,
                Operation      = "RegisterAccount (first commission)",
                Endpoint       = result.Endpoint,
                HttpMethod     = "POST",
                RequestBody    = result.RequestBody,
                HttpStatusCode = result.Success ? 200 : 400,
                ResponseBody   = result.ResponseBody,
                Success        = result.Success,
                ErrorMessage   = result.Success ? null : result.GatewayMessage,
                DurationMs     = (int)result.DurationMs,
                CreationDate   = DateTime.UtcNow,
                CreatedBy      = "job:DeferredPayoutAccount"
            });

            if (!result.Success)
            {
                // Se deja como está para que la próxima corrida lo reintente. No se marca la
                // fecha: marcarla haría que nunca más se intente y el miembro no podría cobrar.
                failed++;
                _logger.LogWarning(
                    "Could not open the {WalletType} account for {MemberId}: {Code} {Message}",
                    wallet.WalletType, wallet.MemberId, result.GatewayCode, result.GatewayMessage);
                continue;
            }

            wallet.AccountIdentifier        = result.AssignedAccountIdentifier ?? identifier;
            wallet.ProviderAccountCreatedAt = DateTime.UtcNow;
            wallet.LastUpdateDate           = DateTime.UtcNow;
            wallet.LastUpdateBy             = "job:DeferredPayoutAccount";

            // Recién con la cuenta abierta la wallet queda operable.
            if (wallet.Status == WalletStatus.Pending)
                wallet.Status = WalletStatus.Approved;

            opened++;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DeferredPayoutAccountJob: {Opened} account(s) opened, {Failed} failed and will be retried.",
            opened, failed);
    }
}
