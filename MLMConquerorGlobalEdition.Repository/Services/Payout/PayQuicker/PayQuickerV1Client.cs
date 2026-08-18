using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker.Contracts;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;

/// <summary>
/// Cliente de la API v1 de PayQuicker — la que hoy mueve dinero real en MWRLife.
///
/// Diferencias de fondo con v2, no cosméticas:
///   · el usuario se identifica por userCompanyAssignedUniqueKey (nuestro MemberId);
///   · el monto va anidado en "monetary": { "amount": … } como número, no string;
///   · los pagos se mandan siempre como lote, aunque sea de uno;
///   · NO existe consulta de estado por referencia (ver GetTransferStatusAsync).
/// </summary>
public class PayQuickerV1Client : IPayQuickerClient
{
    private readonly IHttpClientFactory          _httpFactory;
    private readonly IPayQuickerTokenProvider    _tokens;
    private readonly ILogger<PayQuickerV1Client> _logger;

    public PayQuickerV1Client(
        IHttpClientFactory httpFactory,
        IPayQuickerTokenProvider tokens,
        ILogger<PayQuickerV1Client> logger)
    {
        _httpFactory = httpFactory;
        _tokens      = tokens;
        _logger      = logger;
    }

    public string Version => "V1";

    public async Task<Result<PayQuickerAccountResult>> CreateInvitationAsync(
        PayQuickerAccountRequest request, PayQuickerSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.FundingAccountToken))
            return Result<PayQuickerAccountResult>.Failure(
                "PAYQUICKER_NO_FUNDING_ACCOUNT",
                "PayQuicker v1 needs the funding account public id. Set it as the merchant id on the credential.");

        var payload = new V1InvitationRequest
        {
            FundingAccountPublicId       = settings.FundingAccountToken,
            UserCompanyAssignedUniqueKey = request.ProgramUserId,
            UserNotificationEmailAddress = request.Email
        };

        var result = await SendAsync<V1InvitationResponse>(
            HttpMethod.Post, "api/v1/companies/users/invitations", payload, settings, "create invitation", ct);

        if (!result.IsSuccess)
            return Result<PayQuickerAccountResult>.Failure(result.ErrorCode!, result.Error!);

        var key = result.Value!.InvitationKey;
        if (string.IsNullOrWhiteSpace(key))
            return Result<PayQuickerAccountResult>.Failure(
                "PAYQUICKER_NO_INVITATION_KEY",
                "PayQuicker v1 accepted the invitation but returned no invitationKey.");

        return Result<PayQuickerAccountResult>.Success(new PayQuickerAccountResult
        {
            Exists         = true,
            InvitationKey  = key,
            Status         = result.Value!.Status,
            GatewayCode    = "OK"
        });
    }

    public async Task<Result<PayQuickerAccountResult>> GetAccountAsync(
        PayQuickerAccountRequest request, PayQuickerSettings settings, CancellationToken ct = default)
    {
        var query = $"api/v1/companies/users/invitations" +
                    $"?fundingAccountPublicId={Uri.EscapeDataString(settings.FundingAccountToken ?? string.Empty)}" +
                    $"&userCompanyAssignedUniqueKey={Uri.EscapeDataString(request.ProgramUserId)}";

        var result = await SendAsync<List<V1InvitationResponse>>(
            HttpMethod.Get, query, null, settings, "look up account", ct);

        if (!result.IsSuccess)
            return Result<PayQuickerAccountResult>.Failure(result.ErrorCode!, result.Error!);

        var found = result.Value is { Count: > 0 } ? result.Value[0] : null;
        return Result<PayQuickerAccountResult>.Success(new PayQuickerAccountResult
        {
            Exists         = found is not null,
            InvitationKey  = found?.InvitationKey,
            Status         = found?.Status,
            GatewayCode    = found is not null ? "OK" : "NOT_FOUND",
            GatewayMessage = found is not null ? "Invitation found" : "No PayQuicker invitation for this member"
        });
    }

    public async Task<Result<decimal>> GetBalanceAsync(
        string programUserId, string currency, PayQuickerSettings settings, CancellationToken ct = default)
    {
        var path = $"api/v1/users/{Uri.EscapeDataString(programUserId)}/accounts/action?balance=";

        var result = await SendAsync<List<V1Balance>>(
            HttpMethod.Get, path, null, settings, "get balance", ct);

        if (!result.IsSuccess)
            return Result<decimal>.Failure(result.ErrorCode!, result.Error!);

        var rows = result.Value;
        if (rows is null || rows.Count == 0)
            return Result<decimal>.Success(0m);

        // v1 rotula la moneda como "Currency_USD", no "USD".
        var wanted = $"Currency_{currency}";
        var match  = rows.FirstOrDefault(b =>
                         string.Equals(b.Currency, wanted, StringComparison.OrdinalIgnoreCase))
                     ?? rows[0];

        return Result<decimal>.Success(match.Amount ?? 0m);
    }

    public async Task<Result<PayQuickerTransferResult>> SendPaymentAsync(
        PayQuickerPaymentRequest request, PayQuickerSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.FundingAccountToken))
            return Result<PayQuickerTransferResult>.Failure(
                "PAYQUICKER_NO_FUNDING_ACCOUNT",
                "PayQuicker v1 needs the funding account public id. Set it as the merchant id on the credential.");

        var payload = new V1PaymentBatchRequest
        {
            Payments =
            [
                new V1PaymentItem
                {
                    FundingAccountPublicId       = settings.FundingAccountToken,
                    Monetary                     = new V1Monetary { Amount = request.AmountUsd },
                    UserCompanyAssignedUniqueKey = request.ProgramUserId,
                    UserNotificationEmailAddress = request.Email,
                    AccountingId                 = request.ClientPaymentRef,
                    RecipientUserLanguageCode    = "en-us"
                }
            ]
        };

        var result = await SendAsync<List<V1PaymentResponse>>(
            HttpMethod.Post, "api/v1/companies/accounts/payments", payload, settings, "send payment", ct);

        if (!result.IsSuccess)
            return Result<PayQuickerTransferResult>.Failure(result.ErrorCode!, result.Error!);

        var first = result.Value is { Count: > 0 } ? result.Value[0] : null;
        if (first is null || string.IsNullOrWhiteSpace(first.TransactionPublicId))
            return Result<PayQuickerTransferResult>.Failure(
                "PAYQUICKER_NO_TRANSACTION_ID",
                "PayQuicker v1 accepted the payment but returned no transactionPublicId.");

        return Result<PayQuickerTransferResult>.Success(new PayQuickerTransferResult
        {
            GatewayTransactionId = first.TransactionPublicId!,
            GatewayCode          = first.Status,
            GatewayMessage       = first.AccountingId
        });
    }

    public Task<Result<PayQuickerTransferStatus>> GetTransferStatusAsync(
        string clientPaymentRef, PayQuickerSettings settings, CancellationToken ct = default)
    {
        // LIMITACIÓN REAL DE v1, no una implementación pendiente: la API no expone forma de
        // consultar un pago por referencia del cliente. El sweep de reconciliación necesita
        // esto para resolver intentos colgados tras un crash entre el disburse y el commit.
        //
        // Se devuelve Unknown a propósito: deja el intento en Pending para revisión manual.
        // Devolver NotFound liberaría comisiones de un pago que quizá sí se ejecutó, y
        // devolver Succeeded las daría por pagadas sin evidencia. Ambas mueven dinero mal.
        _logger.LogWarning(
            "PayQuicker v1 cannot resolve transfer status for ref {Ref}; the API has no lookup by client reference. " +
            "Leaving the attempt pending for manual review. Switch the gateway to V2 to enable automatic reconciliation.",
            clientPaymentRef);

        return Task.FromResult(Result<PayQuickerTransferStatus>.Success(new PayQuickerTransferStatus
        {
            State          = PayoutTransferState.Unknown,
            GatewayCode    = "V1_NO_STATUS_LOOKUP",
            GatewayMessage = "PayQuicker v1 does not support looking up a transfer by client reference."
        }));
    }

    private async Task<Result<T>> SendAsync<T>(
        HttpMethod method, string path, object? payload,
        PayQuickerSettings settings, string operation, CancellationToken ct)
    {
        var token = await _tokens.GetAccessTokenAsync(settings, ct);
        if (!token.IsSuccess)
            return Result<T>.Failure(token.ErrorCode!, token.Error!);

        var client = _httpFactory.CreateClient(PayQuickerHttp.ClientName);
        var url    = $"{settings.BaseUrl}/{path.TrimStart('/')}";

        using var request  = PayQuickerHttp.BuildRequest(method, url, token.Value!, "V1", payload);
        using var response = await client.SendAsync(request, ct);

        _logger.LogInformation("PayQuicker V1 {Method} {Path} → {Status}", method, path, (int)response.StatusCode);

        return await PayQuickerHttp.ReadAsync<T>(response, operation, ct);
    }
}
