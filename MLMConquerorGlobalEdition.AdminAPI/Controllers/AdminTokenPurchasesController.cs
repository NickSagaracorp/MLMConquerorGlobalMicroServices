using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Compras de tokens que necesitan a una persona: las pagadas en cripto (confirmar o rechazar la
/// transferencia) y las que quedaron a conciliar (cobro con eWallet o tarjeta de resultado dudoso,
/// o una compra que se quedó en Pending tras una caída).
///
/// LAS ACCIONES LAS EJECUTA BILLING, que es quien emite y quien es dueño del dinero. AdminAPI solo
/// lista (lectura) y reenvía con el Bearer del administrador, sin sustituirlo por una credencial de
/// servicio: Billing vuelve a comprobar el rol por su cuenta y el rastro lo firma la persona real.
/// Llamar a Billing saltándose AdminAPI no abre nada que aquí esté cerrado.
/// </summary>
[ApiController]
[Route("api/v1/admin/token-purchases")]
[Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
public class AdminTokenPurchasesController : ControllerBase
{
    // Conciliar es decidir si el dinero salió: tesorería, no soporte. Mismo conjunto que en Billing.
    private const string Treasury = AppRoles.SuperAdmin + "," + AppRoles.Admin + "," + AppRoles.BillingManager;

    private readonly AppDbContext       _db;
    private readonly IHttpClientFactory _httpFactory;

    public AdminTokenPurchasesController(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db          = db;
        _httpFactory = httpFactory;
    }

    /// <summary>GET /api/v1/admin/token-purchases/crypto-pending — compras en cripto esperando la transferencia.</summary>
    [HttpGet("crypto-pending")]
    public async Task<IActionResult> ListCryptoPending(CancellationToken ct = default)
    {
        // Las dos condiciones: la confirmación sin pagar Y la compra esperándola. Cancelar, rechazar o
        // caducar mueven las dos, pero si alguna vez divergen es mejor no ofrecer un "Confirmar" que
        // Billing va a rechazar.
        var rows = await (
            from c in _db.CryptoPaymentConfirmations.AsNoTracking()
            where c.Purpose == CryptoPaymentPurpose.TokenPurchase
               && c.Status  == CryptoPaymentConfirmationStatus.AwaitingPayment
            join p in _db.TokenPurchases.AsNoTracking() on c.OrderId equals p.OrderId
            where p.Status == TokenPurchaseStatus.AwaitingCryptoConfirmation
            join o in _db.Orders.AsNoTracking() on c.OrderId equals o.Id
            join m in _db.MemberProfiles.AsNoTracking() on c.MemberId equals m.MemberId
            join t in _db.TokenTypes.AsNoTracking() on p.TokenTypeId equals t.Id
            orderby c.CreationDate descending
            select new PendingCryptoTokenPurchaseDto(
                c.OrderId, p.Id, o.OrderNo, c.MemberId, (m.FirstName + " " + m.LastName).Trim(), c.MemberEmail,
                t.Name, p.Quantity, c.CryptoCurrency, c.AmountDue, c.CreationDate)
        ).ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<PendingCryptoTokenPurchaseDto>>.Ok(rows));
    }

    /// <summary>POST /api/v1/admin/token-purchases/crypto/{orderId}/confirm — la transferencia llegó; Billing emite.</summary>
    [HttpPost("crypto/{orderId}/confirm")]
    public async Task<IActionResult> ConfirmCrypto(
        string orderId, [FromBody] ConfirmCryptoRequest request, CancellationToken ct = default)
    {
        if (!await IsCryptoTokenPurchaseAsync(orderId, ct))
            return CryptoNotFound();

        return await ForwardAsync(
            $"api/v1/billing/tokens/purchases/crypto/{Uri.EscapeDataString(orderId)}/confirm",
            new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = request.CryptoTransactionId, Notes = request.Notes },
            ct);
    }

    /// <summary>
    /// POST /api/v1/admin/token-purchases/crypto/{orderId}/reject — la transferencia no llegó o no vale.
    /// Billing cierra la compra sin emitir y libera el cupo (D18).
    /// </summary>
    [HttpPost("crypto/{orderId}/reject")]
    public async Task<IActionResult> RejectCrypto(
        string orderId, [FromBody] RejectCryptoRequest request, CancellationToken ct = default)
    {
        if (!await IsCryptoTokenPurchaseAsync(orderId, ct))
            return CryptoNotFound();

        return await ForwardAsync(
            $"api/v1/billing/tokens/purchases/crypto/{Uri.EscapeDataString(orderId)}/reject",
            new RejectCryptoTokenPurchaseRequest { Reason = request.Reason },
            ct);
    }

    /// <summary>GET /api/v1/admin/token-purchases/review — compras a conciliar (resultado del cobro dudoso).</summary>
    [HttpGet("review")]
    [Authorize(Roles = Treasury)]
    public async Task<IActionResult> ListToReview(CancellationToken ct = default)
    {
        // Las más antiguas primero: son las que más tiempo llevan con el cupo del miembro ocupado.
        var rows = await (
            from p in _db.TokenPurchases.AsNoTracking()
            where p.Status == TokenPurchaseStatus.NeedsReconciliation
            join o in _db.Orders.AsNoTracking() on p.OrderId equals o.Id
            join m in _db.MemberProfiles.AsNoTracking() on p.MemberId equals m.MemberId
            join t in _db.TokenTypes.AsNoTracking() on p.TokenTypeId equals t.Id
            orderby p.CreationDate
            select new TokenPurchaseToReviewDto(
                p.Id, o.OrderNo, p.MemberId, (m.FirstName + " " + m.LastName).Trim(), m.Email,
                t.Name, p.Quantity, p.TotalAmount, p.PaymentMethod.ToString(), p.FailureCode, p.Notes, p.CreationDate)
        ).ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<TokenPurchaseToReviewDto>>.Ok(rows));
    }

    /// <summary>POST /api/v1/admin/token-purchases/{purchaseId}/reconcile — cobrada (emite) o no cobrada (libera el cupo).</summary>
    [HttpPost("{purchaseId}/reconcile")]
    [Authorize(Roles = Treasury)]
    public async Task<IActionResult> Reconcile(
        string purchaseId, [FromBody] ReconcileRequest request, CancellationToken ct = default)
    {
        var exists = await _db.TokenPurchases.AsNoTracking()
            .AnyAsync(p => p.Id == purchaseId && p.Status == TokenPurchaseStatus.NeedsReconciliation, ct);
        if (!exists)
            return NotFound(ApiResponse<object>.Fail(
                TokenPurchaseErrors.NotUnderReview, "No token purchase awaiting reconciliation was found."));

        return await ForwardAsync(
            $"api/v1/billing/tokens/purchases/{Uri.EscapeDataString(purchaseId)}/reconcile",
            new ReconcileTokenPurchaseRequest { Charged = request.Charged, GatewayReference = request.GatewayReference, Notes = request.Notes },
            ct);
    }

    // Solo compras de tokens: un alta en cripto la confirma SignupAPI (AdminCryptoSignupsController),
    // que es quien activa la membresía y genera las comisiones del alta.
    private Task<bool> IsCryptoTokenPurchaseAsync(string orderId, CancellationToken ct) =>
        _db.CryptoPaymentConfirmations.AsNoTracking()
            .AnyAsync(c => c.OrderId == orderId && c.Purpose == CryptoPaymentPurpose.TokenPurchase, ct);

    private NotFoundObjectResult CryptoNotFound() =>
        NotFound(ApiResponse<object>.Fail(
            TokenPurchaseErrors.CryptoPaymentNotFound, "No crypto token purchase awaiting confirmation was found for that order."));

    /// <summary>
    /// Reenvía a Billing con el Bearer del administrador y devuelve su respuesta tal cual (estado y
    /// cuerpo): los códigos de error de Billing (409 TOKEN_ISSUE_FAILED, CRYPTO_PAYMENT_ALREADY_CONFIRMED…)
    /// son los que la pantalla sabe leer.
    /// </summary>
    private async Task<IActionResult> ForwardAsync<TBody>(string path, TBody body, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("BillingApi");

        var bearer = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(bearer) && AuthenticationHeaderValue.TryParse(bearer, out var parsed))
            http.DefaultRequestHeaders.Authorization = parsed;

        HttpResponseMessage resp;
        try
        {
            resp = await http.PostAsJsonAsync(path, body, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Billing no respondió (caído o se agotó el tiempo): no se sabe si llegó a ejecutar. Confirmar y conciliar son
            // idempotentes en Billing (RowVersion y estado), así que repetir es seguro.
            return StatusCode(StatusCodes.Status502BadGateway, ApiResponse<object>.Fail(
                TokenPurchaseErrors.BillingUnavailable, "Billing did not respond. Refresh the list and try again."));
        }

        using (resp)
        {
            return new ContentResult
            {
                Content     = await resp.Content.ReadAsStringAsync(ct),
                ContentType = "application/json",
                StatusCode  = (int)resp.StatusCode
            };
        }
    }

    public record ConfirmCryptoRequest(string CryptoTransactionId, string? Notes);
    public record ReconcileRequest(bool Charged, string? GatewayReference, string? Notes);
    public record RejectCryptoRequest(string? Reason);

    public record PendingCryptoTokenPurchaseDto(
        string OrderId, string PurchaseId, string? OrderNo, string MemberId, string FullName, string Email,
        string TokenTypeName, int Quantity, string CryptoCurrency, decimal AmountDue, DateTime RequestedAt);

    public record TokenPurchaseToReviewDto(
        string PurchaseId, string? OrderNo, string MemberId, string FullName, string Email, string TokenTypeName,
        int Quantity, decimal TotalAmount, string PaymentMethod, string? Reason, string? Notes, DateTime RequestedAt);
}
