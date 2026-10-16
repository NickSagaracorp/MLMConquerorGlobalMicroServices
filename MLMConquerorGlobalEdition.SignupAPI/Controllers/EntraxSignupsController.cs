using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

/// <summary>
/// Entrax (rublos por SBP) en el alta: métodos disponibles, estado para la pantalla del QR,
/// callback de Entrax y Re-check del admin. Todo lo que activa pasa por EntraxSignupSettlement.
///
/// Ninguna respuesta lleva detalles de una excepción (el legado devolvía ex.ToString() en endpoints
/// anónimos): el detalle va al log.
/// </summary>
[ApiController]
[Route("api/v1/signups")]
public class EntraxSignupsController : ControllerBase
{
    /// <summary>Un callback de Entrax es un JSON de unos cientos de bytes.</summary>
    public const int MaxCallbackBytes = 64 * 1024;

    private readonly IEntraxCallbackProcessor           _callbacks;
    private readonly IEntraxSignupStatusService         _status;
    private readonly IEntraxSignupSettlement            _settlement;
    private readonly ILogger<EntraxSignupsController>   _logger;

    public EntraxSignupsController(
        IEntraxCallbackProcessor callbacks,
        IEntraxSignupStatusService status,
        IEntraxSignupSettlement settlement,
        ILogger<EntraxSignupsController> logger)
    {
        _callbacks  = callbacks;
        _status     = status;
        _settlement = settlement;
        _logger     = logger;
    }

    /// <summary>GET api/v1/signups/payment-options — países donde se ofrece pagar en rublos.</summary>
    [HttpGet("payment-options")]
    [AllowAnonymous]
    public IActionResult GetPaymentOptions()
        => Ok(ApiResponse<SignupPaymentOptionsDto>.Ok(_status.GetPaymentOptions()));

    /// <summary>
    /// GET api/v1/signups/{signupId}/entrax-status?ref={paymentRef} — lo consulta la pantalla del QR
    /// cada pocos segundos. Anónimo: exige las dos referencias y solo devuelve estado y tiempo.
    /// </summary>
    [HttpGet("{signupId}/entrax-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEntraxStatus(
        string signupId, [FromQuery(Name = "ref")] string? paymentRef, CancellationToken ct)
    {
        var dto = await _status.GetAsync(signupId, paymentRef, ct);
        return dto is null
            ? NotFound(ApiResponse<EntraxSignupStatusDto>.Fail(EntraxSignupErrors.PaymentNotFound, "Payment not found."))
            : Ok(ApiResponse<EntraxSignupStatusDto>.Ok(dto));
    }

    /// <summary>
    /// POST api/v1/signups/entrax/callback — el aviso de Entrax. Se lee el cuerpo CRUDO (la firma es
    /// sobre esos bytes) y se devuelve 2xx solo si quedó procesado o ya estaba resuelto: si no,
    /// Entrax reintenta (25 veces en 35 minutos).
    /// </summary>
    [HttpPost("entrax/callback")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxCallbackBytes)]
    public async Task<IActionResult> EntraxCallback(CancellationToken ct)
    {
        var body = await ReadBodyAsync(Request.Body, MaxCallbackBytes, ct);
        if (body is null)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        EntraxCallbackOutcome outcome;
        try
        {
            outcome = await _callbacks.ProcessAsync(body, Request.Headers["X-Signature"].FirstOrDefault(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Entrax callback: error inesperado; Entrax lo reintentará.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return outcome switch
        {
            EntraxCallbackOutcome.Accepted     => Ok(new { received = true }),
            EntraxCallbackOutcome.Unauthorized => Unauthorized(),
            EntraxCallbackOutcome.Invalid      => BadRequest(new { error = "INVALID_CALLBACK" }),
            EntraxCallbackOutcome.Stale        => BadRequest(new { error = "STALE_CALLBACK" }),
            EntraxCallbackOutcome.NotFound     => NotFound(),
            _                                  => StatusCode(StatusCodes.Status503ServiceUnavailable)
        };
    }

    /// <summary>
    /// POST api/v1/signups/entrax/{entraxPaymentId}/recheck — vuelve a preguntar a Entrax y liquida.
    /// Lo llama AdminAPI reenviando el Bearer del administrador; el rol se comprueba aquí también.
    /// </summary>
    [HttpPost("entrax/{entraxPaymentId}/recheck")]
    [Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
    public async Task<IActionResult> Recheck(string entraxPaymentId, CancellationToken ct)
    {
        var outcome = await _settlement.SettleAsync(entraxPaymentId, EntraxSettlementSource.Admin, ct);
        return outcome == EntraxSettlementOutcome.NotFound
            ? NotFound(ApiResponse<EntraxRecheckResponse>.Fail(EntraxSignupErrors.PaymentNotFound, "Payment not found."))
            : Ok(ApiResponse<EntraxRecheckResponse>.Ok(new EntraxRecheckResponse(outcome.ToString())));
    }

    public sealed record EntraxRecheckResponse(string Outcome);

    /// <summary>Lee el cuerpo entero o devuelve null si pasa de <paramref name="max"/> bytes.</summary>
    internal static async Task<byte[]?> ReadBodyAsync(Stream body, int max, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await body.ReadAsync(buffer, ct)) > 0)
        {
            if (ms.Length + read > max) return null;
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }
}
