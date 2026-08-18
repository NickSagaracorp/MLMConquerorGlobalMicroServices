using System.Globalization;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker.Contracts;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;

/// <summary>
/// Cliente de la API v2 de PayQuicker (la que el proveedor mantiene hacia adelante).
///
/// Programa "hosted portal": el destinatario se direcciona por programUserId + email y NO
/// por destinationToken. Confirmado contra el sandbox — mandar destinationToken responde
/// "Use ProgramUserId and Email instead", y POST /users/search da 403 para este tipo de programa.
/// </summary>
public class PayQuickerV2Client : IPayQuickerClient
{
    private readonly IHttpClientFactory        _httpFactory;
    private readonly IPayQuickerTokenProvider  _tokens;
    private readonly ILogger<PayQuickerV2Client> _logger;

    public PayQuickerV2Client(
        IHttpClientFactory httpFactory,
        IPayQuickerTokenProvider tokens,
        ILogger<PayQuickerV2Client> logger)
    {
        _httpFactory = httpFactory;
        _tokens      = tokens;
        _logger      = logger;
    }

    public string Version => "V2";

    public async Task<Result<PayQuickerAccountResult>> CreateInvitationAsync(
        PayQuickerAccountRequest request, PayQuickerSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ProgramToken))
            return Result<PayQuickerAccountResult>.Failure(
                "PAYQUICKER_NO_PROGRAM_TOKEN",
                "PayQuicker v2 needs a program token (prog-…) to create invitations. Set it as the additional secret on the credential.");

        var payload = new V2InvitationRequest
        {
            ProgramToken  = settings.ProgramToken,
            ProgramUserId = request.ProgramUserId,
            Email         = request.Email,
            NotifyUser    = request.NotifyUser,
            IssueCard     = request.IssueCard,
            Language      = request.Language,
            FirstName     = request.FirstName,
            LastName      = request.LastName
        };

        var result = await SendAsync<V2InvitationResponse>(
            HttpMethod.Post, "invitations", payload, settings, "create invitation", ct);

        if (!result.IsSuccess)
            return Result<PayQuickerAccountResult>.Failure(result.ErrorCode!, result.Error!);

        var body = result.Value!;

        // Se persiste `key`, NO `token`. El key es el que va en la URL de bienvenida y el
        // equivalente directo del invitationKey de v1; guardar el token invt-… rompería
        // cualquier link que se arme con el valor almacenado.
        if (string.IsNullOrWhiteSpace(body.Key))
            return Result<PayQuickerAccountResult>.Failure(
                "PAYQUICKER_NO_INVITATION_KEY",
                "PayQuicker accepted the invitation but returned no 'key'.");

        return Result<PayQuickerAccountResult>.Success(new PayQuickerAccountResult
        {
            Exists         = true,
            InvitationKey  = body.Key,
            Status         = body.RegistrationStatus ?? body.Status,
            GatewayCode    = "OK",
            GatewayMessage = body.Status
        });
    }

    public async Task<Result<PayQuickerAccountResult>> GetAccountAsync(
        PayQuickerAccountRequest request, PayQuickerSettings settings, CancellationToken ct = default)
    {
        // No hay un GET por programUserId en hosted portal, pero un balance search por
        // PROGRAM_USER_ID sólo devuelve filas si el usuario existe — sirve de prueba de vida
        // sin necesidad de tocar /users/search, que este tipo de programa tiene vedado.
        var payload = new V2BalanceSearchRequest { Scope = request.ProgramUserId, ScopeType = "PROGRAM_USER_ID" };

        var result = await SendAsync<V2BalanceSearchResponse>(
            HttpMethod.Post, "balances/search", payload, settings, "look up account", ct);

        if (!result.IsSuccess)
            return Result<PayQuickerAccountResult>.Failure(result.ErrorCode!, result.Error!);

        var exists = result.Value!.Payload is { Count: > 0 };
        return Result<PayQuickerAccountResult>.Success(new PayQuickerAccountResult
        {
            Exists         = exists,
            GatewayCode    = exists ? "OK" : "NOT_FOUND",
            GatewayMessage = exists ? "Account found" : "No PayQuicker account for this program user id"
        });
    }

    public async Task<Result<decimal>> GetBalanceAsync(
        string programUserId, string currency, PayQuickerSettings settings, CancellationToken ct = default)
    {
        var payload = new V2BalanceSearchRequest { Scope = programUserId, ScopeType = "PROGRAM_USER_ID" };

        var result = await SendAsync<V2BalanceSearchResponse>(
            HttpMethod.Post, "balances/search", payload, settings, "get balance", ct);

        if (!result.IsSuccess)
            return Result<decimal>.Failure(result.ErrorCode!, result.Error!);

        var rows = result.Value!.Payload;
        if (rows is null || rows.Count == 0)
            return Result<decimal>.Success(0m);

        var match = rows.FirstOrDefault(b =>
                        string.Equals(b.Currency, currency, StringComparison.OrdinalIgnoreCase))
                    ?? rows[0];

        return Result<decimal>.Success(
            decimal.TryParse(match.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)
                ? amount
                : 0m);
    }

    public async Task<Result<PayQuickerTransferResult>> SendPaymentAsync(
        PayQuickerPaymentRequest request, PayQuickerSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.FundingAccountToken))
            return Result<PayQuickerTransferResult>.Failure(
                "PAYQUICKER_NO_FUNDING_ACCOUNT",
                "PayQuicker needs the company funding account token (acct-…). Set it as the merchant id on the credential.");

        var payload = new V2PaymentRequest
        {
            TransferType     = "PAYMENT",
            SourceToken      = settings.FundingAccountToken,
            ProgramUserId    = request.ProgramUserId,
            Email            = request.Email,
            Amount           = PayQuickerHttp.FormatAmount(request.AmountUsd),
            ClientPaymentRef = request.ClientPaymentRef,
            Purpose          = request.Purpose,
            AcceptanceMode   = request.AcceptanceMode,
            Memo             = request.Memo,
            Note             = request.Note
        };

        var result = await SendAsync<V2TransferResponse>(
            HttpMethod.Post, "transfers", payload, settings, "send payment", ct);

        if (!result.IsSuccess)
            return Result<PayQuickerTransferResult>.Failure(result.ErrorCode!, result.Error!);

        var body = result.Value!;

        // POST /transfers crea una COTIZACIÓN. Con AUTO_ACCEPT se ejecuta en la misma
        // llamada; con MANUAL_ACCEPT queda PENDING_ACCEPTANCE y haría falta un
        // POST /transfers/{token}/accept que hoy no exponemos. Se rechaza explícitamente
        // en vez de reportar un pago que en realidad no salió.
        if (string.Equals(body.QuoteStatus, "PENDING_ACCEPTANCE", StringComparison.OrdinalIgnoreCase))
            return Result<PayQuickerTransferResult>.Failure(
                "PAYQUICKER_PENDING_ACCEPTANCE",
                $"PayQuicker left transfer {body.Token} pending acceptance. Only AUTO_ACCEPT is supported today.");

        if (string.IsNullOrWhiteSpace(body.Token))
            return Result<PayQuickerTransferResult>.Failure(
                "PAYQUICKER_NO_TRANSFER_TOKEN",
                "PayQuicker accepted the payment but returned no transfer token.");

        return Result<PayQuickerTransferResult>.Success(new PayQuickerTransferResult
        {
            GatewayTransactionId = body.Token!,
            GatewayCode          = body.QuoteStatus,
            GatewayMessage       = body.ReceiptStatus
        });
    }

    public async Task<Result<PayQuickerTransferStatus>> GetTransferStatusAsync(
        string clientPaymentRef, PayQuickerSettings settings, CancellationToken ct = default)
    {
        var payload = new V2TransferSearchRequest
        {
            Filters = [new V2SearchFilter { Field = "CLIENT_PAYMENT_REF", Comparison = "EQUAL_TO", Value = clientPaymentRef }]
        };

        var result = await SendAsync<V2TransferSearchResponse>(
            HttpMethod.Post, "transfers/search", payload, settings, "get transfer status", ct);

        if (!result.IsSuccess)
            // No se pudo consultar: Unknown deja el intento Pending y el sweep reintenta.
            // Reportar Failed acá liberaría comisiones de un pago que quizá SÍ salió.
            return Result<PayQuickerTransferStatus>.Success(new PayQuickerTransferStatus
            {
                State          = PayoutTransferState.Unknown,
                GatewayCode    = result.ErrorCode,
                GatewayMessage = result.Error
            });

        var rows = result.Value!.Payload;
        if (rows is null || rows.Count == 0)
            return Result<PayQuickerTransferStatus>.Success(new PayQuickerTransferStatus
            {
                State          = PayoutTransferState.NotFound,
                GatewayCode    = "NOT_FOUND",
                GatewayMessage = $"PayQuicker has no transfer with clientPaymentRef '{clientPaymentRef}'."
            });

        var transfer = rows[0];
        var state = transfer.QuoteStatus?.ToUpperInvariant() switch
        {
            "ACCEPTED" or "COMPLETE" or "COMPLETED" => PayoutTransferState.Succeeded,
            "REJECTED" or "CANCELLED" or "EXPIRED"  => PayoutTransferState.Failed,
            "PENDING_ACCEPTANCE"                    => PayoutTransferState.Unknown,
            _                                       => PayoutTransferState.Unknown
        };

        return Result<PayQuickerTransferStatus>.Success(new PayQuickerTransferStatus
        {
            State                = state,
            GatewayTransactionId = transfer.Token,
            GatewayCode          = transfer.QuoteStatus,
            GatewayMessage       = transfer.ReceiptStatus
        });
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

        using var request  = PayQuickerHttp.BuildRequest(method, url, token.Value!, "V2", payload);
        using var response = await client.SendAsync(request, ct);

        _logger.LogInformation("PayQuicker V2 {Method} {Path} → {Status}", method, path, (int)response.StatusCode);

        return await PayQuickerHttp.ReadAsync<T>(response, operation, ct);
    }
}
