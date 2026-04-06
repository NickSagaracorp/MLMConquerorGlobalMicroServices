using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetProfile;
using MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetSecurityLog;
using MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateEmail;
using MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdatePassword;
using MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateProfile;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ProfileController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>GET /api/v1/bizcenter/profile — returns current member profile</summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProfileQuery(), ct);
        if (!result.IsSuccess)
            return NotFound(ApiResponse<ProfileDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<ProfileDto>.Ok(result.Value!));
    }

    /// <summary>PUT /api/v1/bizcenter/profile — update profile fields</summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProfileCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<ProfileDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<ProfileDto>.Ok(result.Value!));
    }

    /// <summary>PUT /api/v1/bizcenter/profile/photo — upload profile photo (placeholder S3)</summary>
    [HttpPut("profile/photo")]
    public async Task<IActionResult> UpdatePhoto([FromBody] UpdatePhotoRequest request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        // Placeholder: return a URL without actual S3 upload
        var photoUrl = $"/profile-photos/{memberId}.jpg";
        return Ok(ApiResponse<string>.Ok(photoUrl, "Profile photo updated successfully."));
    }

    /// <summary>PUT /api/v1/bizcenter/profile/credentials/email — update email (stub)</summary>
    [HttpPut("profile/credentials/email")]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateEmailCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Email update request recorded. A verification email will be sent."));
    }

    /// <summary>PUT /api/v1/bizcenter/profile/credentials/password — update password (not implemented)</summary>
    [HttpPut("profile/credentials/password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdatePasswordCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>GET /api/v1/bizcenter/profile/security-log — paged audit log for the current member.</summary>
    [HttpGet("profile/security-log")]
    public async Task<IActionResult> GetSecurityLog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSecurityLogQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<SecurityLogDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<SecurityLogDto>>.Ok(result.Value!));
    }
}
