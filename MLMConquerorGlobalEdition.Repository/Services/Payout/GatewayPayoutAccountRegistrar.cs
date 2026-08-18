using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Wallets;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout;

/// <summary>
/// Da de alta la cuenta contra el proveedor real cuando un miembro registra o cambia su
/// método de cobro.
///
/// Antes esto no pasaba: MemberWalletService simulaba el alta con un Task.Delay y devolvía
/// "Approved" sin hablar con nadie. La cuenta recién se creaba de verdad en el PRIMER PAGO,
/// cuando el orquestador hacía ValidateAccount → SubscribeAccount. Eso dejaba una ventana en
/// la que la wallet figuraba lista y en el proveedor no existía nada; si el alta fallaba, se
/// enteraba el día del pago.
///
/// El orquestador CONSERVA su validate → subscribe: es la red de seguridad para wallets
/// creadas antes de este cambio, o para cuando el alta falló y el miembro no reintentó.
/// </summary>
public class GatewayPayoutAccountRegistrar : IPayoutAccountRegistrar
{
    private readonly IPayoutGatewayResolver                    _resolver;
    private readonly ILogger<GatewayPayoutAccountRegistrar>    _logger;

    public GatewayPayoutAccountRegistrar(
        IPayoutGatewayResolver resolver, ILogger<GatewayPayoutAccountRegistrar> logger)
    {
        _resolver = resolver;
        _logger   = logger;
    }

    public async Task<PayoutAccountRegistrationResult> RegisterAsync(
        PayoutAccountRegistrationRequest request, CancellationToken ct = default)
    {
        // Crypto no tiene cuenta que crear: el miembro provee una dirección de wallet y la
        // responsabilidad de que sea correcta es suya. No hay proveedor al que dar de alta.
        if (request.WalletType == WalletType.Crypto)
            return new PayoutAccountRegistrationResult
            {
                Success        = true,
                Skipped        = true,
                GatewayCode    = "NO_REGISTRATION_REQUIRED",
                GatewayMessage = "Crypto payouts go to an address the member supplies; there is no account to open.",
                Endpoint       = "(none)"
            };

        var resolved = _resolver.Resolve(request.WalletType);
        if (!resolved.IsSuccess)
        {
            // Sin implementación para ese gateway no se puede afirmar que la cuenta exista.
            // Se devuelve fallo para que la wallet quede Pending en vez de aparentar estar lista.
            _logger.LogWarning(
                "No payout gateway is implemented for {WalletType}; wallet for {MemberId} stays pending.",
                request.WalletType, request.MemberId);

            return new PayoutAccountRegistrationResult
            {
                Success        = false,
                GatewayCode    = resolved.ErrorCode,
                GatewayMessage = resolved.Error,
                Endpoint       = "(unresolved)"
            };
        }

        var gateway = resolved.Value!;
        var context = new PayoutAccountContext
        {
            MemberId          = request.MemberId,
            WalletType        = request.WalletType,
            AccountIdentifier = request.AccountIdentifier,
            // El email viaja como AccountMeta: es lo que PayQuicker usa para direccionar
            // (programUserId + email) y lo que i-Payout necesita para crear la cuenta.
            AccountMeta       = request.Email
        };

        var payload = JsonSerializer.Serialize(new
        {
            memberId          = request.MemberId,
            walletType        = request.WalletType.ToString(),
            accountIdentifier = request.AccountIdentifier,
            email             = request.Email
        });

        var endpoint = $"{request.WalletType}/SubscribeAccount";
        var sw = Stopwatch.StartNew();

        try
        {
            // Primero se pregunta si ya existe. Volver a dar de alta a alguien que ya está
            // registrado puede generarle una invitación duplicada.
            var validate = await gateway.ValidateAccountAsync(context, ct);

            // Un validate FALLIDO no es "la cuenta no existe": es "no se pudo averiguar"
            // —credencial mal cargada, proveedor caído, red—. Seguir de largo al alta sería
            // afirmar algo que no se comprobó, y en gateways como Volet, donde el alta es un
            // no-op que siempre responde OK, dejaría la wallet en Approved sin haber tocado
            // al proveedor. Se corta acá y la wallet queda Pending.
            if (!validate.IsSuccess)
            {
                sw.Stop();
                return new PayoutAccountRegistrationResult
                {
                    Success        = false,
                    GatewayCode    = validate.ErrorCode,
                    GatewayMessage = validate.Error,
                    Endpoint       = $"{request.WalletType}/ValidateAccount",
                    RequestBody    = payload,
                    ResponseBody   = validate.Error ?? string.Empty,
                    DurationMs     = sw.ElapsedMilliseconds
                };
            }

            if (validate.Value!.Exists)
            {
                sw.Stop();
                return new PayoutAccountRegistrationResult
                {
                    Success        = true,
                    GatewayCode    = validate.Value.GatewayCode,
                    GatewayMessage = "Account already exists at the provider; no invitation was sent.",
                    Endpoint       = $"{request.WalletType}/ValidateAccount",
                    RequestBody    = payload,
                    ResponseBody   = JsonSerializer.Serialize(validate.Value),
                    DurationMs     = sw.ElapsedMilliseconds
                };
            }

            var subscribe = await gateway.SubscribeAccountAsync(context, ct);
            sw.Stop();

            if (!subscribe.IsSuccess)
                return new PayoutAccountRegistrationResult
                {
                    Success        = false,
                    GatewayCode    = subscribe.ErrorCode,
                    GatewayMessage = subscribe.Error,
                    Endpoint       = endpoint,
                    RequestBody    = payload,
                    ResponseBody   = subscribe.Error ?? string.Empty,
                    DurationMs     = sw.ElapsedMilliseconds
                };

            // GatewayMessage trae el identificador que asignó el proveedor: el UserName de
            // i-Payout o la clave de invitación de PayQuicker. Sólo se adopta si el gateway
            // devolvió algo distinto de lo que mandó el usuario.
            var assigned = subscribe.Value!.GatewayMessage;
            var adopt = !string.IsNullOrWhiteSpace(assigned)
                        && !string.Equals(assigned, request.AccountIdentifier, StringComparison.Ordinal)
                        && NeedsProviderAssignedIdentifier(request.WalletType);

            return new PayoutAccountRegistrationResult
            {
                Success                   = true,
                AssignedAccountIdentifier = adopt ? assigned : null,
                GatewayCode               = subscribe.Value.GatewayCode,
                GatewayMessage            = subscribe.Value.GatewayMessage,
                Endpoint                  = endpoint,
                RequestBody               = payload,
                ResponseBody              = JsonSerializer.Serialize(subscribe.Value),
                DurationMs                = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Registering {MemberId} with {WalletType} threw; the wallet stays pending.",
                request.MemberId, request.WalletType);

            return new PayoutAccountRegistrationResult
            {
                Success        = false,
                GatewayCode    = "GATEWAY_EXCEPTION",
                GatewayMessage = ex.Message,
                Endpoint       = endpoint,
                RequestBody    = payload,
                ResponseBody   = ex.ToString(),
                DurationMs     = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Gateways cuyo identificador definitivo lo decide el proveedor, no el usuario.
    /// i-Payout asigna un UserName al registrar; en Volet o PayPal el email que puso el
    /// miembro sigue siendo el identificador válido y no hay que pisarlo.
    /// </summary>
    private static bool NeedsProviderAssignedIdentifier(WalletType type) =>
        type is WalletType.eWallet;
}
