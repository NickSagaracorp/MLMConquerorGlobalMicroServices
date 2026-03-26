using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StartImpersonation;
using MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StopImpersonation;
using MLMConquerorGlobalEdition.SharedKernel;
using System.Security.Claims;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Admin,SupportManager")]
public class ImpersonationController : ControllerBase
{
    private readonly IMediator _mediator;

    public ImpersonationController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Generates an impersonation token allowing an admin to view a member's data.
    /// SupportManager gets read-only impersonation; SuperAdmin/Admin get full access.
    /// </summary>
    [HttpPost("members/{memberId}/impersonate")]
    public async Task<IActionResult> StartImpersonation(
        string memberId,
        CancellationToken ct)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("sub")
                       ?? string.Empty;
        var adminRoles  = User.Claims
                             .Where(c => c.Type == ClaimTypes.Role)
                             .Select(c => c.Value)
                             .ToList();

        var result = await _mediator.Send(
            new StartImpersonationCommand(adminUserId, adminRoles, memberId), ct);

        return result.IsSuccess
            ? Ok(ApiResponse<StartImpersonationResult>.Ok(result.Value!))
            : BadRequest(ApiResponse<StartImpersonationResult>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>
    /// Invalidates the impersonation token and returns the admin to their own context.
    /// </summary>
    [HttpPost("impersonation/exit")]
    public async Task<IActionResult> StopImpersonation(CancellationToken ct)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("sub")
                       ?? string.Empty;

        var result = await _mediator.Send(new StopImpersonationCommand(adminUserId), ct);

        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Impersonation ended."))
            : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
    }
}
