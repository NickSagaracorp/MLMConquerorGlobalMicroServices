using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;

/// <summary>
/// Payout gateway de PayQuicker. Reemplaza a Dwolla en la oferta.
///
/// No habla HTTP directamente: delega en <see cref="IPayQuickerClient"/>, del cual hay una
/// implementación por versión de API. Cuál corre lo decide el admin con el selector
/// ApiVersion de PaymentGatewayInfo, y contra qué ambiente con el selector Environment.
/// Ambos se resuelven POR LLAMADA, así que cambiar de sandbox a producción o de v1 a v2
/// no requiere reiniciar nada.
///
/// El AccountIdentifier de una wallet PayQuicker es el MemberId (programUserId): el
/// programa es "hosted portal" y el destinatario se direcciona por programUserId + email.
/// </summary>
public class PayQuickerPayoutGatewayService : IPayoutGatewayService
{
    private readonly IPayQuickerSettingsProvider              _settings;
    private readonly IEnumerable<IPayQuickerClient>           _clients;
    private readonly ILogger<PayQuickerPayoutGatewayService>  _logger;

    public PayQuickerPayoutGatewayService(
        IPayQuickerSettingsProvider settings,
        IEnumerable<IPayQuickerClient> clients,
        ILogger<PayQuickerPayoutGatewayService> logger)
    {
        _settings = settings;
        _clients  = clients;
        _logger   = logger;
    }

    public WalletType GatewayType => WalletType.PayQuicker;

    public async Task<Result<PayoutAccountResult>> SubscribeAccountAsync(
        PayoutAccountContext ctx, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);
        if (!resolved.IsSuccess)
            return Result<PayoutAccountResult>.Failure(resolved.ErrorCode!, resolved.Error!);

        var (client, settings) = resolved.Value!;

        var email = ExtractEmail(ctx.AccountMeta);
        if (string.IsNullOrWhiteSpace(email))
            return Result<PayoutAccountResult>.Failure(
                "PAYQUICKER_EMAIL_REQUIRED",
                "PayQuicker addresses payees by program user id + email; no email was supplied for this member.");

        var result = await client.CreateInvitationAsync(
            new PayQuickerAccountRequest { ProgramUserId = ctx.MemberId, Email = email }, settings, ct);

        if (!result.IsSuccess)
            return Result<PayoutAccountResult>.Failure(result.ErrorCode!, result.Error!);

        return Result<PayoutAccountResult>.Success(new PayoutAccountResult
        {
            Exists         = result.Value!.Exists,
            GatewayCode    = result.Value!.GatewayCode,
            // El InvitationKey vuelve en el mensaje para que el llamador pueda persistirlo;
            // es el valor que arma la URL de bienvenida del ambassador.
            GatewayMessage = result.Value!.InvitationKey
        });
    }

    public async Task<Result<PayoutAccountResult>> ValidateAccountAsync(
        PayoutAccountContext ctx, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);
        if (!resolved.IsSuccess)
            return Result<PayoutAccountResult>.Failure(resolved.ErrorCode!, resolved.Error!);

        var (client, settings) = resolved.Value!;

        var result = await client.GetAccountAsync(
            new PayQuickerAccountRequest
            {
                ProgramUserId = string.IsNullOrWhiteSpace(ctx.AccountIdentifier) ? ctx.MemberId : ctx.AccountIdentifier,
                Email         = ExtractEmail(ctx.AccountMeta) ?? string.Empty
            }, settings, ct);

        if (!result.IsSuccess)
            return Result<PayoutAccountResult>.Failure(result.ErrorCode!, result.Error!);

        return Result<PayoutAccountResult>.Success(new PayoutAccountResult
        {
            Exists         = result.Value!.Exists,
            GatewayCode    = result.Value!.GatewayCode,
            GatewayMessage = result.Value!.GatewayMessage
        });
    }

    public async Task<Result<PayoutBalanceResult>> GetBalanceAsync(
        string memberId, string accountIdentifier, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);
        if (!resolved.IsSuccess)
            return Result<PayoutBalanceResult>.Failure(resolved.ErrorCode!, resolved.Error!);

        var (client, settings) = resolved.Value!;
        var programUserId = string.IsNullOrWhiteSpace(accountIdentifier) ? memberId : accountIdentifier;

        var result = await client.GetBalanceAsync(programUserId, "USD", settings, ct);
        if (!result.IsSuccess)
            return Result<PayoutBalanceResult>.Failure(result.ErrorCode!, result.Error!);

        return Result<PayoutBalanceResult>.Success(new PayoutBalanceResult
        {
            Balance     = result.Value,
            Currency    = "USD",
            GatewayCode = "OK"
        });
    }

    public async Task<Result<PayoutTransferResult>> DisburseAsync(
        PayoutTransferContext ctx, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);
        if (!resolved.IsSuccess)
            return Result<PayoutTransferResult>.Failure(resolved.ErrorCode!, resolved.Error!);

        var (client, settings) = resolved.Value!;

        // ctx.Reference es el PayoutAttempt.Id. Se usa tal cual como clave de idempotencia:
        // si el orquestador reintenta el mismo intento, PayQuicker rechaza el ref repetido
        // en vez de pagar dos veces. El límite del contrato es 50 caracteres.
        var clientRef = ctx.Reference.Length <= 50 ? ctx.Reference : ctx.Reference[..50];

        var email = ExtractEmail(ctx.AccountIdentifier);
        if (string.IsNullOrWhiteSpace(email))
            return Result<PayoutTransferResult>.Failure(
                "PAYQUICKER_EMAIL_REQUIRED",
                "PayQuicker needs the payee email to disburse; the wallet has none recorded.");

        var result = await client.SendPaymentAsync(new PayQuickerPaymentRequest
        {
            ProgramUserId    = ctx.MemberId,
            Email            = email,
            AmountUsd        = ctx.AmountUsd,
            ClientPaymentRef = clientRef,
            Memo             = $"Commission payout {clientRef}"
        }, settings, ct);

        if (!result.IsSuccess)
            return Result<PayoutTransferResult>.Failure(result.ErrorCode!, result.Error!);

        return Result<PayoutTransferResult>.Success(new PayoutTransferResult
        {
            GatewayTransactionId = result.Value!.GatewayTransactionId,
            GatewayCode          = result.Value!.GatewayCode,
            GatewayMessage       = result.Value!.GatewayMessage
        });
    }

    public async Task<Result<PayoutTransferStatusResult>> GetTransferStatusAsync(
        string reference, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);
        if (!resolved.IsSuccess)
            // No se pudo ni resolver la configuración: Unknown mantiene el intento Pending
            // y el sweep vuelve a intentar. Nunca se asume que el dinero no salió.
            return Result<PayoutTransferStatusResult>.Success(new PayoutTransferStatusResult
            {
                State          = PayoutTransferState.Unknown,
                GatewayCode    = resolved.ErrorCode,
                GatewayMessage = resolved.Error
            });

        var (client, settings) = resolved.Value!;
        var clientRef = reference.Length <= 50 ? reference : reference[..50];

        var result = await client.GetTransferStatusAsync(clientRef, settings, ct);
        if (!result.IsSuccess)
            return Result<PayoutTransferStatusResult>.Success(new PayoutTransferStatusResult
            {
                State          = PayoutTransferState.Unknown,
                GatewayCode    = result.ErrorCode,
                GatewayMessage = result.Error
            });

        return Result<PayoutTransferStatusResult>.Success(new PayoutTransferStatusResult
        {
            State                = result.Value!.State,
            GatewayTransactionId = result.Value!.GatewayTransactionId,
            GatewayCode          = result.Value!.GatewayCode,
            GatewayMessage       = result.Value!.GatewayMessage
        });
    }

    /// <summary>Resuelve configuración + cliente de la versión que el admin dejó seleccionada.</summary>
    private async Task<Result<(IPayQuickerClient Client, PayQuickerSettings Settings)>> ResolveAsync(CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        if (!settings.IsSuccess)
            return Result<(IPayQuickerClient, PayQuickerSettings)>.Failure(settings.ErrorCode!, settings.Error!);

        var client = _clients.FirstOrDefault(c =>
            string.Equals(c.Version, settings.Value!.ApiVersion, StringComparison.OrdinalIgnoreCase));

        if (client is null)
            return Result<(IPayQuickerClient, PayQuickerSettings)>.Failure(
                "PAYQUICKER_NO_CLIENT",
                $"No PayQuicker client registered for API version '{settings.Value!.ApiVersion}'.");

        _logger.LogDebug("PayQuicker resolved to {Version} / {Environment}",
            settings.Value!.ApiVersion, settings.Value!.Environment);

        return Result<(IPayQuickerClient, PayQuickerSettings)>.Success((client, settings.Value!));
    }

    /// <summary>
    /// La wallet de PayQuicker guarda el MemberId como AccountIdentifier, pero el email hace
    /// falta igual para direccionar. Puede venir en AccountMeta o en el propio identificador
    /// si el admin cargó ahí el correo.
    /// </summary>
    private static string? ExtractEmail(string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate) && candidate.Contains('@') ? candidate.Trim() : null;
}
