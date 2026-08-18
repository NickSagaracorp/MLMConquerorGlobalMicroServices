using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Payout.Volet;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout;

/// <summary>
/// Payout gateway de Volet (ex AdvCash). Integración real contra su web service SOAP.
///
/// El identificador de cuenta es el EMAIL del ambassador — a diferencia de eWallet, donde es
/// el MemberId. Volet no expone alta de cuentas: nadie puede crear una cuenta Volet a nombre
/// de otro. En su lugar, pagarle a un email sin cuenta dispara el flujo "sendMoneyToEmail",
/// donde el destinatario recibe un aviso, reclama los fondos y abre su cuenta en el proceso.
/// Por eso SubscribeAccountAsync no crea nada: no hay nada que crear.
/// </summary>
public class VoletPayoutGatewayService : IPayoutGatewayService
{
    private readonly IVoletClient                        _client;
    private readonly ILogger<VoletPayoutGatewayService>  _logger;

    public VoletPayoutGatewayService(IVoletClient client, ILogger<VoletPayoutGatewayService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public WalletType GatewayType => WalletType.Volet;

    public async Task<Result<PayoutAccountResult>> ValidateAccountAsync(
        PayoutAccountContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.AccountIdentifier))
            return Result<PayoutAccountResult>.Failure(
                "VOLET_NO_EMAIL", "The wallet has no Volet email recorded.");

        var result = await _client.ValidateAccountAsync(ctx.AccountIdentifier, ct);
        if (!result.IsSuccess)
            return Result<PayoutAccountResult>.Failure(result.ErrorCode!, result.Error!);

        return Result<PayoutAccountResult>.Success(new PayoutAccountResult
        {
            Exists         = result.Value!.Present,
            GatewayCode    = result.Value.Present ? "OK" : "NOT_FOUND",
            GatewayMessage = result.Value.Present
                ? (result.Value.IsUserVerified ? "Account exists and is verified" : "Account exists but is not verified yet")
                : "No Volet account for this email; a payout will be sent as a claimable transfer"
        });
    }

    public Task<Result<PayoutAccountResult>> SubscribeAccountAsync(
        PayoutAccountContext ctx, CancellationToken ct = default)
    {
        // Volet no tiene API de alta: la cuenta la abre el propio destinatario al reclamar el
        // primer pago. Se responde Exists = true para que el orquestador siga adelante — el
        // pago es justamente el mecanismo de onboarding.
        _logger.LogInformation(
            "Volet has no account-creation API; {MemberId} will be onboarded by claiming the first transfer.",
            ctx.MemberId);

        return Task.FromResult(Result<PayoutAccountResult>.Success(new PayoutAccountResult
        {
            Exists         = true,
            GatewayCode    = "NO_REGISTRATION_REQUIRED",
            GatewayMessage = "Volet opens the account when the recipient claims the first transfer."
        }));
    }

    public Task<Result<PayoutBalanceResult>> GetBalanceAsync(
        string memberId, string accountIdentifier, CancellationToken ct = default)
    {
        // getBalances de Volet devuelve el saldo de las billeteras del MERCHANT, no el del
        // destinatario. No hay forma de consultar el saldo de un ambassador, así que se dice
        // en vez de devolver un cero que parecería un saldo real.
        return Task.FromResult(Result<PayoutBalanceResult>.Failure(
            "VOLET_BALANCE_NOT_SUPPORTED",
            "Volet only exposes the merchant's own wallet balances, not a recipient's."));
    }

    public async Task<Result<PayoutTransferResult>> DisburseAsync(
        PayoutTransferContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.AccountIdentifier))
            return Result<PayoutTransferResult>.Failure(
                "VOLET_NO_EMAIL", "The wallet has no Volet email recorded; cannot disburse.");

        // El cliente decide solo entre sendMoney (cuenta existente) y sendMoneyToEmail
        // (cuenta por abrir), consultando antes. La referencia va en el note, que es lo único
        // que Volet propaga y permite conciliar después contra el PayoutAttempt.
        var result = await _client.SendMoneyAsync(
            ctx.AccountIdentifier, ctx.AmountUsd, "USD", $"Commission payout {ctx.Reference}", ct);

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
        // El WSDL de Volet no expone consulta de transacción por referencia del cliente.
        // Unknown deja el intento Pending para revisión manual: reportar NotFound liberaría
        // comisiones de un pago que quizá sí salió.
        _logger.LogWarning(
            "Volet has no transaction lookup by client reference; leaving attempt {Ref} pending for manual review.",
            reference);

        return Task.FromResult(Result<PayoutTransferStatusResult>.Success(new PayoutTransferStatusResult
        {
            State          = PayoutTransferState.Unknown,
            GatewayCode    = "VOLET_NO_STATUS_LOOKUP",
            GatewayMessage = "Volet does not expose a transaction lookup by client reference."
        }));
    }
}
