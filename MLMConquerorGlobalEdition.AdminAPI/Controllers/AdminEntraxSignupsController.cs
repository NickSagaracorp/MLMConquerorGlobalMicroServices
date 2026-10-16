using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Services;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Altas pagadas en rublos por SBP con Entrax (Plan H): las que esperan pago, las que quedaron a
/// revisar (pagos que no cuadran o dobles cobros, con dinero en juego) y las recientes.
///
/// Re-check NO liquida aquí: reenvía a SignupAPI, que es quien activa el alta por el camino único
/// (EntraxSignupSettlement), con el Bearer del administrador. Los mismos cuatro roles que confirmar
/// una cripto, comprobados aquí y otra vez en SignupAPI.
///
/// Lo que sale de aquí son datos de presentación: ni el enlace del QR (se puede pagar con él), ni las
/// notas internas, ni nada de lo que Entrax manda o firma. Los cuatro roles ven lo mismo que en la
/// pantalla de altas en cripto: no hay ningún dato al portador que tapar a soporte.
/// </summary>
[ApiController]
[Route("api/v1/admin/entrax-signups")]
[Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
public class AdminEntraxSignupsController : ControllerBase
{
    public const string ViewOpen   = "open";
    public const string ViewReview = "review";
    public const string ViewRecent = "recent";

    public const int DefaultPageSize = 50;
    public const int MaxPageSize     = 200;

    /// <summary>
    /// "Recientes" mira solo estos días: sin filtro de estado no hay índice que la sostenga (el que hay
    /// es Status + CreationDate) y la tabla crece con cada QR. Esperando y a revisar no tienen límite.
    /// </summary>
    public const int RecentDays = 30;

    /// <summary>Tope del Re-check: Entrax + activación. Si no contesta, 502 y se puede repetir.</summary>
    public static readonly TimeSpan DefaultRecheckTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Acción de la fila de AuditTracking que deja cada Re-check.</summary>
    public const string AuditRecheck = "EntraxRecheck";

    /// <summary>Lo que se audita cuando SignupAPI no contestó (o no contestó un JSON).</summary>
    public const string SignupApiUnavailable = "SIGNUP_API_UNAVAILABLE";

    /// <summary>Lo que se audita cuando el administrador cerró la petición antes de la respuesta.</summary>
    public const string ClientCancelled = "CLIENT_CANCELLED";

    private readonly AppDbContext                           _db;
    private readonly IHttpClientFactory                     _httpFactory;
    private readonly ICurrentUserService                    _user;
    private readonly IDateTimeProvider                      _clock;
    private readonly ILogger<AdminEntraxSignupsController>  _logger;

    public AdminEntraxSignupsController(
        AppDbContext db, IHttpClientFactory httpFactory, ICurrentUserService user, IDateTimeProvider clock,
        ILogger<AdminEntraxSignupsController> logger)
    {
        _db          = db;
        _httpFactory = httpFactory;
        _user        = user;
        _clock       = clock;
        _logger      = logger;
    }

    /// <summary>Tope del Re-check; las pruebas lo acortan.</summary>
    public TimeSpan RecheckTimeout { get; init; } = DefaultRecheckTimeout;

    /// <summary>GET api/v1/admin/entrax-signups?view=open|review|recent&amp;page=1&amp;pageSize=50</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? view = ViewOpen, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        // Acotada para que (page - 1) * pageSize quepa en un int.
        page     = Math.Clamp(page, 1, int.MaxValue / pageSize);

        var recentFrom = _clock.Now.AddDays(-RecentDays);
        var payments   = _db.EntraxPayments.AsNoTracking();
        payments = view switch
        {
            ViewReview => payments.Where(p => p.Status == EntraxPaymentStatus.ToReview),
            ViewRecent => payments.Where(p => p.CreationDate >= recentFrom),
            _          => payments.Where(p => p.Status == EntraxPaymentStatus.AwaitingPayment)
        };

        var total = await payments.CountAsync(ct);

        // Uniones a la izquierda: un pago a revisar tiene dinero en juego y no puede desaparecer de la
        // lista porque falte el perfil o el pedido.
        var raw = await (
            from p in payments
            join m0 in _db.MemberProfiles.AsNoTracking() on p.MemberId equals m0.MemberId into ms
            from m in ms.DefaultIfEmpty()
            join o0 in _db.Orders.AsNoTracking() on p.OrderId equals o0.Id into os
            from o in os.DefaultIfEmpty()
            orderby p.CreationDate descending, p.Id
            select new
            {
                Payment  = p,
                OrderNo  = o != null ? o.OrderNo : null,
                FullName = m != null ? (m.FirstName + " " + m.LastName).Trim() : null,
                Email    = m != null ? m.Email : null,
                Country  = m != null ? m.Country : null
            }
        ).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var rows = raw.Select(x => new EntraxSignupRowDto(
            x.Payment.Id, x.Payment.OrderId, x.OrderNo, x.Payment.MemberId, x.FullName, x.Email, x.Country,
            x.Payment.Status.ToString(), x.Payment.AmountUsd, x.Payment.AmountRub, x.Payment.PaidAmountUsd,
            x.Payment.ExternalOrderId, x.Payment.ExternalPaymentId, x.Payment.ExpiresAt, x.Payment.PaidAt,
            x.Payment.LastCheckedAt, x.Payment.FailureCode, x.Payment.CreationDate)).ToList();

        return Ok(ApiResponse<PagedResult<EntraxSignupRowDto>>.Ok(new PagedResult<EntraxSignupRowDto>
        {
            Items      = rows,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        }));
    }

    /// <summary>
    /// POST api/v1/admin/entrax-signups/{id}/recheck — vuelve a preguntar a Entrax y liquida.
    ///
    /// Cada Re-check deja una fila en AuditTracking con quién lo pidió, en qué estado estaba el pago y
    /// qué contestó SignupAPI (código HTTP y desenlace), también si SignupAPI no respondió. El Bearer
    /// se reenvía en la petición y no se guarda.
    /// </summary>
    [HttpPost("{id}/recheck")]
    public async Task<IActionResult> Recheck(string id, CancellationToken ct = default)
    {
        var before = await _db.EntraxPayments.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => (EntraxPaymentStatus?)p.Status)
            .FirstOrDefaultAsync(ct);
        if (before is null)
            return NotFound(ApiResponse<object>.Fail("ENTRAX_PAYMENT_NOT_FOUND", "Payment not found."));

        var http = _httpFactory.CreateClient(SignupApiRegistration.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/signups/entrax/{Uri.EscapeDataString(id)}/recheck");

        var bearer = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(bearer) && AuthenticationHeaderValue.TryParse(bearer, out var parsed))
            request.Headers.Authorization = parsed;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RecheckTimeout);

        int     status;
        string  body;
        try
        {
            using var resp = await http.SendAsync(request, timeout.Token);
            status = (int)resp.StatusCode;
            // Con ResponseContentRead el cuerpo ya está en memoria: leerlo no depende del token.
            body   = await resp.Content.ReadAsStringAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El administrador cerró la pantalla; SignupAPI pudo liquidar igualmente. Queda auditado.
            await AuditAsync(id, before.Value, httpStatus: 0, ClientCancelled);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // Caído o sin respuesta a tiempo: no se sabe si llegó a liquidar; es idempotente, repetir es seguro.
            await AuditAsync(id, before.Value, httpStatus: 0, SignupApiUnavailable);
            return Unavailable();
        }

        if (!IsJsonObject(body))
        {
            // El [Authorize] de SignupAPI rechaza sin cuerpo: un problema de permisos no es un servicio caído.
            if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
            {
                await AuditAsync(id, before.Value, status, "FORBIDDEN");
                return StatusCode(status, ApiResponse<object>.Fail("FORBIDDEN",
                    "You do not have permission to re-check Entrax payments."));
            }

            // Un proxy o una página de error delante de SignupAPI: no se reenvía a la pantalla.
            await AuditAsync(id, before.Value, status, SignupApiUnavailable);
            return Unavailable();
        }

        await AuditAsync(id, before.Value, status, OutcomeOf(body));
        return new ContentResult { Content = body, ContentType = "application/json", StatusCode = status };
    }

    private ObjectResult Unavailable() =>
        StatusCode(StatusCodes.Status502BadGateway,
            ApiResponse<object>.Fail(SignupApiUnavailable, "The signup service is not reachable. Try again."));

    private static bool IsJsonObject(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// La fila de auditoría se escribe después de reenviar: el Re-check no decide nada (liquida según lo
    /// que diga Entrax y es idempotente), así que si guardarla falla se registra en el log y se devuelve
    /// igualmente lo que contestó SignupAPI, que ya actuó.
    /// </summary>
    private async Task AuditAsync(string id, EntraxPaymentStatus before, int httpStatus, string? outcome)
    {
        try
        {
            _db.AuditTracking.Add(new GeneralAuditTracking
            {
                EntityName = nameof(EntraxPayment),
                EntityId   = id,
                Action     = AuditRecheck,
                OldValues  = JsonSerializer.Serialize(new { Status = before.ToString() }),
                NewValues  = JsonSerializer.Serialize(new { HttpStatus = httpStatus, Outcome = outcome }),
                // Mismo criterio que el resto de AuditTracking: el id del usuario, que no cambia con el correo.
                ChangedBy  = string.IsNullOrWhiteSpace(_user.UserId) ? (_user.Email ?? "unknown") : _user.UserId,
                ChangedAt  = _clock.Now
            });
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Entrax Re-check de {EntraxPaymentId} por {UserId}: no se pudo guardar la auditoría ({HttpStatus}, {Outcome}).",
                id, _user.UserId, httpStatus, outcome);
        }
    }

    /// <summary>data.outcome si fue bien, errorCode si no; null si el cuerpo no es el ApiResponse esperado.</summary>
    private static string? OutcomeOf(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            string? Prop(JsonElement e, string name) =>
                e.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                 .Value is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

            var data = root.EnumerateObject()
                .FirstOrDefault(p => string.Equals(p.Name, "data", StringComparison.OrdinalIgnoreCase)).Value;
            var outcome = data.ValueKind == JsonValueKind.Object ? Prop(data, "outcome") : null;
            var result  = outcome ?? Prop(root, "errorCode");
            return result is { Length: > 64 } ? result[..64] : result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public sealed record EntraxSignupRowDto(
        string    Id,
        string    OrderId,
        string?   OrderNo,
        string    MemberId,
        string?   FullName,
        string?   Email,
        string?   Country,
        string    Status,
        decimal   AmountUsd,
        decimal?  AmountRub,
        decimal?  PaidAmountUsd,
        string    ExternalOrderId,
        string?   ExternalPaymentId,
        DateTime? ExpiresAt,
        DateTime? PaidAt,
        DateTime? LastCheckedAt,
        string?   Reason,
        DateTime  CreatedAt);
}
