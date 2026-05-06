using MediatR;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
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
        // Fire-and-forget telemetry — token validation is informational, we don't block here
        // since the user can keep typing variants on a single page load. The result lives in
        // the table for after-the-fact fraud review.
        _ = _fraud.RecordAsync(
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
