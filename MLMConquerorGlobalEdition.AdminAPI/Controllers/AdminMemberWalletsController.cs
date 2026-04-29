using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Wallets;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Admin view of a member's payment-gateway wallets — including the full
/// change history and the gateway API call/response log for fraud and
/// support investigations. All logic lives in <see cref="IMemberWalletService"/>
/// so admin and bizcenter views never diverge.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/wallets")]
[Authorize(Roles = "SuperAdmin,Admin,SupportManager,SupportLevel1,SupportLevel2,SupportLevel3")]
public class AdminMemberWalletsController : ControllerBase
{
    private readonly IMemberWalletService _service;

    public AdminMemberWalletsController(IMemberWalletService service)
        => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(string memberId, CancellationToken ct = default)
    {
        var result = await _service.GetAccountsAsync(memberId, ct);
        return Ok(ApiResponse<List<WalletAccountView>>.Ok(result));
    }

    /// <summary>Admin-side update — same code path as bizcenter, audited under the admin's identity.</summary>
    [Authorize(Roles = "SuperAdmin,Admin")]
    [HttpPut("{walletType}")]
    public async Task<IActionResult> Save(
        string memberId,
        WalletType walletType,
        [FromBody] AdminSaveWalletPayload payload,
        CancellationToken ct = default)
    {
        var actor = User?.Identity?.Name ?? "admin";
        var req = new SaveWalletRequest
        {
            WalletType               = walletType,
            AccountIdentifier        = payload.AccountIdentifier,
            Notes                    = payload.Notes,
            EWalletPasswordEncrypted = payload.EWalletPasswordEncrypted
        };
        var result = await _service.SaveAccountAsync(memberId, req, actor, ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<WalletAccountView>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<WalletAccountView>.Ok(result.Value!));
    }

    [Authorize(Roles = "SuperAdmin,Admin")]
    [HttpPut("{walletType}/default")]
    public async Task<IActionResult> SetDefault(
        string memberId, WalletType walletType, CancellationToken ct = default)
    {
        var actor = User?.Identity?.Name ?? "admin";
        var result = await _service.SetDefaultAsync(memberId, walletType, actor, ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<WalletAccountView>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<WalletAccountView>.Ok(result.Value!));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(
        string memberId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _service.GetHistoryAsync(memberId, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<WalletHistoryView>>.Ok(result));
    }

    /// <summary>API call/response log — every interaction with the gateway, for support escalations.</summary>
    [HttpGet("api-logs")]
    public async Task<IActionResult> ApiLogs(
        string memberId,
        [FromQuery] WalletType? walletType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _service.GetApiLogsAsync(memberId, walletType, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<WalletApiLogView>>.Ok(result));
    }

    public record AdminSaveWalletPayload(
        string AccountIdentifier,
        string? Notes = null,
        string? EWalletPasswordEncrypted = null);
}
