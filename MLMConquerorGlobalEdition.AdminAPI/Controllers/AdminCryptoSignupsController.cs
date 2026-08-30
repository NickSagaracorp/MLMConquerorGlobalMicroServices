using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Altas pagadas en cripto que esperan que alguien confirme a mano que el dinero entró.
///
/// No hay pasarela de cripto integrada y no la va a haber: el cobro llega por fuera y una persona
/// lo coteja. Este controlador es esa persona vista desde la API.
///
/// EL ROL SE COMPRUEBA EN EL SERVIDOR, dos veces. Aquí, con el atributo de abajo, y otra vez en
/// SignupAPI, que es quien de verdad activa la membresía y dispara las comisiones. Esconder el
/// botón en AdminWeb no cuenta como control de acceso: una llamada directa con curl a cualquiera
/// de las dos APIs rebota igual.
/// </summary>
[ApiController]
[Route("api/v1/admin/crypto-signups")]
[Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
public class AdminCryptoSignupsController : ControllerBase
{
    private readonly AppDbContext       _db;
    private readonly IHttpClientFactory _httpFactory;

    public AdminCryptoSignupsController(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db          = db;
        _httpFactory = httpFactory;
    }

    /// <summary>
    /// GET /api/v1/admin/crypto-signups/pending — altas en cripto esperando confirmación de cobro.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> ListPending(CancellationToken ct = default)
    {
        var rows = await (
            from c in _db.CryptoPaymentConfirmations.AsNoTracking()
            where c.Status == CryptoPaymentConfirmationStatus.AwaitingPayment
            join m in _db.MemberProfiles.AsNoTracking() on c.MemberId equals m.MemberId
            join o in _db.Orders.AsNoTracking() on c.OrderId equals o.Id
            orderby c.CreationDate descending
            select new PendingCryptoSignupDto(
                c.OrderId,
                o.OrderNo,
                c.MemberId,
                (m.FirstName + " " + m.LastName).Trim(),
                c.MemberEmail,
                m.Country,
                m.SponsorMemberId,
                c.CryptoCurrency,
                c.AmountDue,
                m.EnrollDate,
                c.CreationDate)
        ).ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<PendingCryptoSignupDto>>.Ok(rows));
    }

    /// <summary>
    /// GET /api/v1/admin/crypto-signups/confirmed — el rastro: qué se aprobó, quién lo aprobó,
    /// cuándo y contra qué identificador de transacción. Es la vista de auditoría del cobro.
    /// </summary>
    [HttpGet("confirmed")]
    public async Task<IActionResult> ListConfirmed([FromQuery] int take = 100, CancellationToken ct = default)
    {
        if (take is < 1 or > 500) take = 100;

        var rows = await (
            from c in _db.CryptoPaymentConfirmations.AsNoTracking()
            where c.Status == CryptoPaymentConfirmationStatus.Confirmed
            join m in _db.MemberProfiles.AsNoTracking() on c.MemberId equals m.MemberId
            orderby c.ConfirmedAt descending
            select new ConfirmedCryptoSignupDto(
                c.OrderId,
                c.MemberId,
                (m.FirstName + " " + m.LastName).Trim(),
                c.MemberEmail,
                c.CryptoCurrency,
                c.AmountDue,
                c.CryptoTransactionId,
                c.ConfirmedByEmail,
                c.ConfirmedAt,
                c.Notes)
        ).Take(take).ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<ConfirmedCryptoSignupDto>>.Ok(rows));
    }

    /// <summary>
    /// POST /api/v1/admin/crypto-signups/{orderId}/confirm — confirma el cobro.
    ///
    /// Reenvía a SignupAPI porque es allí donde vive la activación del alta: el cierre del
    /// pedido, los deltas del upline, el bono de patrocinador y el Fast Start. Duplicar aquí ese
    /// cálculo sería tener dos versiones de las comisiones de alta y que la segunda se quedara
    /// atrás en cuanto alguien tocara la primera.
    ///
    /// Se reenvía TAMBIÉN el Bearer del administrador que pulsó, sin sustituirlo por ninguna
    /// credencial de servicio. Así SignupAPI vuelve a comprobar el rol por su cuenta y el rastro
    /// de auditoría lo firma la persona real, no "el sistema".
    /// </summary>
    [HttpPost("{orderId}/confirm")]
    public async Task<IActionResult> Confirm(
        string orderId, [FromBody] ConfirmCryptoRequest request, CancellationToken ct = default)
    {
        var exists = await _db.CryptoPaymentConfirmations.AsNoTracking()
            .AnyAsync(c => c.OrderId == orderId, ct);

        if (!exists)
            return NotFound(ApiResponse<object>.Fail(
                "CRYPTO_PAYMENT_NOT_FOUND", "No crypto payment awaiting confirmation was found for that order."));

        var http = _httpFactory.CreateClient("SignupApi");

        var bearer = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(bearer)
            && AuthenticationHeaderValue.TryParse(bearer, out var parsed))
        {
            http.DefaultRequestHeaders.Authorization = parsed;
        }

        var resp = await http.PostAsJsonAsync(
            $"api/v1/signups/{orderId}/confirm-crypto-payment",
            new { cryptoTransactionId = request.CryptoTransactionId, notes = request.Notes },
            ct);

        var body = await resp.Content.ReadAsStringAsync(ct);

        return new ContentResult
        {
            Content     = body,
            ContentType = "application/json",
            StatusCode  = (int)resp.StatusCode
        };
    }

    public record ConfirmCryptoRequest(string CryptoTransactionId, string? Notes);

    public record PendingCryptoSignupDto(
        string    OrderId,
        string?   OrderNo,
        string    MemberId,
        string    FullName,
        string    Email,
        string    Country,
        string?   SponsorMemberId,
        string    CryptoCurrency,
        decimal   AmountDue,
        DateTime  EnrollDate,
        DateTime  RequestedAt);

    public record ConfirmedCryptoSignupDto(
        string    OrderId,
        string    MemberId,
        string    FullName,
        string    Email,
        string    CryptoCurrency,
        decimal   AmountDue,
        string?   CryptoTransactionId,
        string?   ConfirmedByEmail,
        DateTime? ConfirmedAt,
        string?   Notes);
}
