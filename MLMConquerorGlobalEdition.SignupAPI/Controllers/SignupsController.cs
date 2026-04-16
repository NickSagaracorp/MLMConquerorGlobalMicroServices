using MediatR;
using Microsoft.AspNetCore.Mvc;
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

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

[ApiController]
[Route("api/v1/signups")]
public class SignupsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SignupsController(IMediator mediator) => _mediator = mediator;


    [HttpPost("ambassador")]
    public async Task<IActionResult> SignupAmbassador(
        [FromBody] AmbassadorSignupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SignupAmbassadorCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<SignupResponse>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<SignupResponse>.Ok(result.Value!));
    }

    [HttpPost("member")]
    public async Task<IActionResult> SignupMember(
        [FromBody] MemberSignupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SignupMemberCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<SignupResponse>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<SignupResponse>.Ok(result.Value!));
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
