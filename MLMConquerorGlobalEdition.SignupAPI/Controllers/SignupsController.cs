using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.ConfirmCryptoPayment;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SelectProducts;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupAmbassador;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupMember;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetMembershipLevels;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetProducts;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.ValidateReplicateSite;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.ValidateSponsor;
using MLMConquerorGlobalEdition.SignupAPI.Features.Countries;
using MLMConquerorGlobalEdition.SignupAPI.Features.Tokens.ValidateToken;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

[ApiController]
[Route("api/v1/signups")]
public class SignupsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFraudFingerprintService _fraud;

    public SignupsController(IMediator mediator, IFraudFingerprintService fraud)
    {
        _mediator = mediator;
        _fraud    = fraud;
    }

    [HttpPost("ambassador")]
    public async Task<IActionResult> SignupAmbassador(
        [FromBody] AmbassadorSignupRequest request, CancellationToken ct)
    {
        var fp = await _fraud.RecordAsync(
            request.VisitorId, SignupRiskFlow.AmbassadorSignup,
            request.SponsorReplicateSite, ResolveClientIp(), Request.Headers.UserAgent.ToString(),
            null, null, ct);

        if (fp.IsFlagged)
            return BadRequest(ApiResponse<SignupResponse>.Fail(
                "SIGNUP_BLOCKED", "We can't process this signup right now. Please contact support."));

        var result = await _mediator.Send(new SignupAmbassadorCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<SignupResponse>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<SignupResponse>.Ok(result.Value!));
    }

    [HttpPost("member")]
    public async Task<IActionResult> SignupMember(
        [FromBody] MemberSignupRequest request, CancellationToken ct)
    {
        var fp = await _fraud.RecordAsync(
            request.VisitorId, SignupRiskFlow.MemberSignup,
            request.SponsorReplicateSite, ResolveClientIp(), Request.Headers.UserAgent.ToString(),
            null, null, ct);

        if (fp.IsFlagged)
            return BadRequest(ApiResponse<SignupResponse>.Fail(
                "SIGNUP_BLOCKED", "We can't process this signup right now. Please contact support."));

        var result = await _mediator.Send(new SignupMemberCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<SignupResponse>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<SignupResponse>.Ok(result.Value!));
    }

    /// <summary>Returns the public client IP, preferring the standard forward header when behind a proxy.</summary>
    private string? ResolveClientIp()
    {
        var fwd = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(fwd))
        {
            // First entry is the originating client per RFC 7239.
            var first = fwd.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }


    [HttpPost("{signupId}/select-products")]
    public async Task<IActionResult> SelectProducts(
        string signupId, [FromBody] SelectProductsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SelectProductsCommand(signupId, request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Products selected successfully."));
    }


    [HttpPost("{signupId}/complete")]
    public async Task<IActionResult> CompleteSignup(
        string signupId, [FromBody] CompleteSignupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CompleteSignupCommand(signupId, request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<SignupResponse>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<SignupResponse>.Ok(result.Value!));
    }


    /// <summary>
    /// POST /api/v1/signups/{signupId}/confirm-crypto-payment — confirma a mano que el cobro en
    /// cripto de un alta entró: activa miembro y suscripción y dispara comisiones y deltas.
    ///
    /// LA COMPROBACIÓN DE ROL ESTÁ AQUÍ, EN EL SERVIDOR, y no solo en el botón de AdminWeb ni
    /// solo en AdminAPI. AdminAPI reenvía a este endpoint el Bearer del administrador que pulsó,
    /// así que quien intente llamar a cualquiera de los dos directamente con un token de un rol
    /// que no está en la lista se lleva un 403 igual. Los dos servicios validan contra el mismo
    /// emisor, la misma audiencia y la misma clave pública, de modo que el JWT que emite AdminAPI
    /// vale aquí sin ningún puente de confianza inventado.
    /// </summary>
    [HttpPost("{signupId}/confirm-crypto-payment")]
    [Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
    public async Task<IActionResult> ConfirmCryptoPayment(
        string signupId, [FromBody] ConfirmCryptoPaymentRequest request, CancellationToken ct)
    {
        // Quién aprueba sale de los claims, nunca del cuerpo: si el llamante pudiera elegir a
        // nombre de quién queda el rastro, el rastro no auditaría nada.
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var email  = User.FindFirstValue(ClaimTypes.Email)
                  ?? User.FindFirstValue("email")
                  ?? string.Empty;

        var result = await _mediator.Send(
            new ConfirmCryptoPaymentCommand(signupId, request, userId, email), ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CRYPTO_PAYMENT_NOT_FOUND" => NotFound(
                    ApiResponse<ConfirmCryptoPaymentResponse>.Fail(result.ErrorCode!, result.Error!)),
                "CRYPTO_PAYMENT_ALREADY_CONFIRMED" => Conflict(
                    ApiResponse<ConfirmCryptoPaymentResponse>.Fail(result.ErrorCode!, result.Error!)),
                _ => BadRequest(
                    ApiResponse<ConfirmCryptoPaymentResponse>.Fail(result.ErrorCode!, result.Error!))
            };
        }

        return Ok(ApiResponse<ConfirmCryptoPaymentResponse>.Ok(
            result.Value!, "Crypto payment confirmed. Membership activated."));
    }


    [HttpPost("validate-replicate-site")]
    public async Task<IActionResult> ValidateReplicateSite(
        [FromBody] ValidateReplicateSiteRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ValidateReplicateSiteQuery(request.Slug), ct);
        if (!result.IsSuccess)
            return Conflict(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Slug is available."));
    }

    [HttpPost("validate-sponsor")]
    public async Task<IActionResult> ValidateSponsor(
        [FromBody] ValidateSponsorRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ValidateSponsorQuery(request.SponsorMemberId), ct);
        if (!result.IsSuccess)
            return NotFound(ApiResponse<SponsorInfoResponse>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<SponsorInfoResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Real-time token validation for the join page.
    /// Always returns 200 with a structured ValidateTokenResponse to prevent enumeration attacks.
    /// </summary>
    [HttpPost("validate-token")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> ValidateToken(
        [FromBody] ValidateTokenRequest request, CancellationToken ct)
    {
        // Telemetry — token validation is informational. Awaited (not fire-and-forget) because
        // both this call and the mediator handler share the request-scoped AppDbContext, and
        // running them concurrently triggers EF Core's ConcurrencyDetector. The recording is
        // a single INSERT plus a COUNT, so the latency cost is negligible.
        await _fraud.RecordAsync(
            request.VisitorId, SignupRiskFlow.TokenValidation,
            request.SponsorReplicateSite, ResolveClientIp(), Request.Headers.UserAgent.ToString(),
            null, null, ct);

        var result = await _mediator.Send(new ValidateTokenQuery(request), ct);

        // The handler is designed to ALWAYS return Result.Success with a structured payload.
        // If a pipeline behavior (e.g. ErrorHandlingBehavior) converts an unhandled exception
        // into Result.Failure, surface a generic invalid response to the client — never leak
        // the internal error.
        if (!result.IsSuccess || result.Value is null)
        {
            return Ok(ApiResponse<ValidateTokenResponse>.Ok(new ValidateTokenResponse
            {
                Valid   = false,
                Message = "This token is not valid for this signup."
            }));
        }

        return Ok(ApiResponse<ValidateTokenResponse>.Ok(result.Value));
    }

    [HttpGet("membership-levels")]
    public async Task<IActionResult> GetMembershipLevels(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMembershipLevelsQuery(), ct);
        return Ok(ApiResponse<IEnumerable<MembershipLevelDto>>.Ok(result.Value!));
    }

    [HttpGet("products")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? countryIso2 = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductsQuery(countryIso2), ct);
        if (!result.IsSuccess)
            return StatusCode(500, ApiResponse<IEnumerable<ProductDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<IEnumerable<ProductDto>>.Ok(result.Value!));
    }

    [HttpGet("countries")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetCountries(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCountriesQuery(), ct);
        return Ok(ApiResponse<List<CountryLookupDto>>.Ok(result.Value!));
    }
}
