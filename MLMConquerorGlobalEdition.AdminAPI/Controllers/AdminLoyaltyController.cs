using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Features.LoyaltyAdmin.UnlockLoyaltyPoints;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/loyalty")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminLoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminLoyaltyController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// POST /api/v1/admin/loyalty/{memberId}/unlock
    /// Unlocks loyalty points for a member.
    /// Body: { "loyaltyPointsId": "..." } — optional. Omit to unlock all locked records.
    /// </summary>
    [HttpPost("{memberId}/unlock")]
    public async Task<IActionResult> Unlock(
        string memberId,
        [FromBody] UnlockLoyaltyRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new UnlockLoyaltyPointsCommand(memberId, request.LoyaltyPointsId), ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "MEMBER_NOT_FOUND" => NotFound(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!)),
                "RECORD_NOT_FOUND" => NotFound(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!)),
                _ => BadRequest(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!))
            };
        }

        return Ok(ApiResponse<int>.Ok(result.Value!,
            $"{result.Value} loyalty record(s) unlocked successfully."));
    }

    public record UnlockLoyaltyRequest(string? LoyaltyPointsId = null);
}
