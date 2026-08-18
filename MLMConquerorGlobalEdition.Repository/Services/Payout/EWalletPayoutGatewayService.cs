using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout;

/// <summary>
/// Payout gateway de i-Payout (eWallet). Integración real contra la API RPC del proveedor.
///
/// IMPORTANTE sobre el identificador: el AccountIdentifier de una wallet eWallet es el
/// UserName/UserID que ASIGNA i-Payout al registrar la cuenta, no un email. Toda la
/// operatoria (saldo, acreditación) va contra ese UserName.
/// </summary>
public class EWalletPayoutGatewayService : IPayoutGatewayService
{
    private readonly IEWalletClient                         _client;
    private readonly ILogger<EWalletPayoutGatewayService>   _logger;

    public EWalletPayoutGatewayService(IEWalletClient client, ILogger<EWalletPayoutGatewayService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public WalletType GatewayType => WalletType.eWallet;

    public async Task<Result<PayoutAccountResult>> SubscribeAccountAsync(
        PayoutAccountContext ctx, CancellationToken ct = default)
    {
        // El alta necesita un email de contacto; el UserName lo asigna el gateway y vuelve
        // en la respuesta para que el llamador lo persista como AccountIdentifier.
        var email = ctx.AccountMeta;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Result<PayoutAccountResult>.Failure(
                "EWALLET_EMAIL_REQUIRED",
                "i-Payout needs a contact email to register the account.");

        var result = await _client.CreateUserAsync(new EWalletCreateUserRequest
        {
            UserName = ctx.MemberId,   // se propone el MemberId; el gateway confirma o reasigna
            Email    = email
        }, ct);

        if (!result.IsSuccess)
            return Result<PayoutAccountResult>.Failure(result.ErrorCode!, result.Error!);

        return Result<PayoutAccountResult>.Success(new PayoutAccountResult
        {
            Exists         = true,
            GatewayCode    = "OK",
            // El UserName asignado viaja acá: es lo que hay que guardar en el wallet.
            GatewayMessage = result.Value
        });
    }

    public async Task<Result<PayoutAccountResult>> ValidateAccountAsync(
        PayoutAccountContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.AccountIdentifier))
            return Result<PayoutAccountResult>.Failure(
                "EWALLET_NO_USER_ID",
                "The wallet has no i-Payout User ID recorded.");

        var result = await _client.UserExistsAsync(ctx.AccountIdentifier, ct);
        if (!result.IsSuccess)
            return Result<PayoutAccountResult>.Failure(result.ErrorCode!, result.Error!);

        return Result<PayoutAccountResult>.Success(new PayoutAccountResult
        {
            Exists         = result.Value,
            GatewayCode    = result.Value ? "OK" : "NOT_FOUND",
            GatewayMessage = result.Value ? "User exists" : "i-Payout has no user with that User ID"
        });
    }

    public async Task<Result<PayoutBalanceResult>> GetBalanceAsync(
        string memberId, string accountIdentifier, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountIdentifier))
            return Result<PayoutBalanceResult>.Failure(
                "EWALLET_NO_USER_ID",
                "The wallet has no i-Payout User ID recorded.");

        var result = await _client.GetBalanceAsync(accountIdentifier, "USD", ct);
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
        if (string.IsNullOrWhiteSpace(ctx.AccountIdentifier))
            return Result<PayoutTransferResult>.Failure(
                "EWALLET_NO_USER_ID",
                "The wallet has no i-Payout User ID recorded; cannot disburse.");

        // ctx.Reference es el PayoutAttempt.Id y viaja como MerchantReferenceID. Con
        // AllowDuplicates = false, un reintento del mismo intento es rechazado por el
        // gateway en vez de pagar dos veces.
        var result = await _client.LoadAsync(
            ctx.AccountIdentifier, ctx.AmountUsd, ctx.Reference,
            $"Commission payout {ctx.Reference}", ct);

        if (!result.IsSuccess)
            return Result<PayoutTransferResult>.Failure(result.ErrorCode!, result.Error!);

        return Result<PayoutTransferResult>.Success(new PayoutTransferResult
        {
            GatewayTransactionId = result.Value!,
            GatewayCode          = "OK"
        });
    }

    public Task<Result<PayoutTransferStatusResult>> GetTransferStatusAsync(
        string reference, CancellationToken ct = default)
    {
        // i-Payout no expone (en el contrato que usa MWRLife) una consulta de estado por
        // MerchantReferenceID. Se devuelve Unknown a propósito: deja el intento Pending
        // para revisión manual en vez de arriesgar liberar comisiones de un pago que sí salió.
        _logger.LogWarning(
            "i-Payout has no status lookup by merchant reference; leaving attempt {Ref} pending for manual review.",
            reference);

        return Task.FromResult(Result<PayoutTransferStatusResult>.Success(new PayoutTransferStatusResult
        {
            State          = PayoutTransferState.Unknown,
            GatewayCode    = "EWALLET_NO_STATUS_LOOKUP",
            GatewayMessage = "i-Payout does not expose a transfer lookup by merchant reference."
        }));
    }
}
