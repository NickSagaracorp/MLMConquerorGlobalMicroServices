using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.CancelPayoutBatch;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ExportPayoutBatch;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetMemberPayoutBalance;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutAudit;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutAuditDetail;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutBatchDetail;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutBatches;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutGatewayLog;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutGatewaySettings;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutStats;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPendingPayouts;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.MarkPayoutBatchPaid;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ProcessMemberPayout;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ReconcilePayoutBatch;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ResendPayoutReceipt;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.UpdatePayoutGatewaySetting;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ValidateMemberPayoutAccount;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.VerifyPayoutReceipt;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Admin endpoints for payout gateway configuration and per-member payout operations.
/// Routes under /api/v1/admin/payout-gateway-settings and /api/v1/admin/members/{id}/payout/*
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Admin,BillingManager")]
public class AdminPayoutsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;
    private readonly IReceiptStorage _receiptStorage;

    public AdminPayoutsController(IMediator mediator, AppDbContext db, IReceiptStorage receiptStorage)
    {
        _mediator = mediator;
        _db = db;
        _receiptStorage = receiptStorage;
    }

    // ── Payout Gateways (PaymentGatewayInfo catalog) ────────────────────────

    /// <summary>GET /api/v1/admin/payout-gateways — list all payout gateways (active + inactive).</summary>
    [HttpGet("payout-gateways")]
    public async Task<IActionResult> GetGateways(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPayoutGatewaySettingsQuery(), ct);

        if (!result.IsSuccess)
            return StatusCode(500, ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<List<PayoutGatewayDto>>.Ok(result.Value!));
    }

    public record UpdateGatewayBody(
        string DisplayName,
        decimal AdminFee,
        AdminFeeKind AdminFeeKind,
        decimal? MinAdminFee,
        string Currency,
        decimal MinimumPayoutAmount,
        bool IsActive);

    /// <summary>PUT /api/v1/admin/payout-gateways/{walletType} — update fees, threshold and active flag for a gateway.</summary>
    [HttpPut("payout-gateways/{walletType}")]
    public async Task<IActionResult> UpdateGateway(
        WalletType walletType,
        [FromBody] UpdateGatewayBody body,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdatePayoutGatewaySettingCommand(
            walletType, body.DisplayName, body.AdminFee, body.AdminFeeKind,
            body.MinAdminFee, body.Currency, body.MinimumPayoutAmount, body.IsActive), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PayoutGatewayDto>.Ok(result.Value!));
    }

    // ── Per-Member Payout Operations ────────────────────────────────────────

    /// <summary>GET /api/v1/admin/members/{memberId}/payout/balance — check gateway balance for a member.</summary>
    [HttpGet("members/{memberId}/payout/balance")]
    public async Task<IActionResult> GetBalance(string memberId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMemberPayoutBalanceQuery(memberId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<MemberPayoutBalanceDto>.Ok(result.Value!));
    }

    /// <summary>POST /api/v1/admin/members/{memberId}/payout/validate — validate a member's payout account with the gateway.</summary>
    [HttpPost("members/{memberId}/payout/validate")]
    public async Task<IActionResult> Validate(string memberId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ValidateMemberPayoutAccountCommand(memberId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PayoutAccountValidationDto>.Ok(result.Value!));
    }

    public record ProcessPayoutBody(DateTime? ProcessDate);

    /// <summary>POST /api/v1/admin/members/{memberId}/payout — trigger a payout for a member via the orchestrator.</summary>
    [HttpPost("members/{memberId}/payout")]
    public async Task<IActionResult> Process(
        string memberId,
        [FromBody] ProcessPayoutBody body,
        CancellationToken ct = default)
    {
        // ProcessDate comes from the caller (admin's chosen date); falls back to UtcNow when omitted.
        // The orchestrator itself uses IDateTimeProvider for audit timestamps — this is the one
        // accepted DateTime.UtcNow usage per the plan note.
        var processDate = body.ProcessDate ?? DateTime.UtcNow;
        var result = await _mediator.Send(new ProcessMemberPayoutCommand(memberId, processDate), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PayoutResult>.Ok(result.Value!));
    }

    // ── Payments Dashboard ──────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/payouts/pending — paginated list of pending-payout candidates.</summary>
    [HttpGet("payouts/pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] DateTime? processDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] WalletType? walletType = null,
        [FromQuery] int? commissionTypeId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetPendingPayoutsQuery(processDate ?? DateTime.UtcNow, page, pageSize, walletType, commissionTypeId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<PendingPayoutRowDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/admin/payouts/stats — per-gateway pending vs paid-on-day summary.</summary>
    [HttpGet("payouts/stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? processDate,
        [FromQuery] int? commissionTypeId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPayoutStatsQuery(processDate ?? DateTime.UtcNow, commissionTypeId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PayoutStatsDto>.Ok(result.Value!));
    }

    // ── Payout Audit ────────────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/payouts/audit — filtered, paged list of payout attempts.</summary>
    [HttpGet("payouts/audit")]
    public async Task<IActionResult> GetAudit(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? memberId,
        [FromQuery] WalletType? walletType,
        [FromQuery] string? outcome,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetPayoutAuditQuery(from, to, memberId, walletType, outcome, page, pageSize), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<PagedResult<PayoutAuditRowDto>>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    /// <summary>GET /api/v1/admin/payouts/audit/{attemptId} — full detail including earnings snapshot.</summary>
    [HttpGet("payouts/audit/{attemptId:long}")]
    public async Task<IActionResult> GetAuditDetail(long attemptId, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetPayoutAuditDetailQuery(attemptId), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<PayoutAuditDetailDto>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    /// <summary>GET /api/v1/admin/payouts/audit/{attemptId}/gateway-log — raw wallet API log for the attempt's member.</summary>
    [HttpGet("payouts/audit/{attemptId:long}/gateway-log")]
    public async Task<IActionResult> GetGatewayLog(
        long attemptId,
        [FromQuery] string memberId,
        CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetPayoutGatewayLogQuery(memberId, attemptId), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<List<PayoutGatewayLogDto>>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    /// <summary>GET /api/v1/admin/payouts/audit/{attemptId}/verify — hash + chain + anchor authenticity check.</summary>
    [HttpGet("payouts/audit/{attemptId:long}/verify")]
    public async Task<IActionResult> Verify(long attemptId, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new VerifyPayoutReceiptCommand(attemptId), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<ReceiptVerificationDto>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    /// <summary>GET /api/v1/admin/payouts/audit/{attemptId}/receipt — download the PDF receipt file.</summary>
    [HttpGet("payouts/audit/{attemptId:long}/receipt")]
    public async Task<IActionResult> DownloadReceipt(long attemptId, CancellationToken ct = default)
    {
        var attempt = await _db.PayoutAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attemptId, ct);
        if (attempt is null || string.IsNullOrEmpty(attempt.ReceiptUrl))
            return NotFound(ApiResponse<object>.Fail("RECEIPT_NOT_FOUND", "No receipt for this payout"));

        var fileName = PayoutReceiptFileNaming.Build(attempt.Id, attempt.MemberId);
        var bytes = await _receiptStorage.ReadAsync(fileName, ct);
        if (bytes is null)
            return NotFound(ApiResponse<object>.Fail("RECEIPT_FILE_MISSING", "Receipt file missing"));

        return File(bytes, "application/pdf", fileName);
    }

    /// <summary>POST /api/v1/admin/payouts/{attemptId}/receipt/resend — resend the receipt email regardless of toggle.</summary>
    [HttpPost("payouts/{attemptId:long}/receipt/resend")]
    public async Task<IActionResult> ResendReceipt(long attemptId, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new ResendPayoutReceiptCommand(attemptId), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    // ── Bulk CSV Payout Batches ─────────────────────────────────────────────

    public record ExportBatchBody(WalletType WalletType, DateTime? ProcessDate);

    /// <summary>POST /api/v1/admin/payouts/batches/export — export pending payouts for a gateway as a CSV batch (reserves earnings).</summary>
    [HttpPost("payouts/batches/export")]
    public async Task<IActionResult> ExportBatch(
        [FromBody] ExportBatchBody body,
        CancellationToken ct = default)
    {
        var r = await _mediator.Send(
            new ExportPayoutBatchCommand(body.WalletType, body.ProcessDate ?? DateTime.UtcNow), ct);

        if (!r.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));

        return File(r.Value!.CsvBytes, "text/csv", r.Value.FileName);
    }

    /// <summary>GET /api/v1/admin/payouts/batches — paged list of payout batches, optionally filtered by status and wallet type.</summary>
    [HttpGet("payouts/batches")]
    public async Task<IActionResult> GetBatches(
        [FromQuery] string? status,
        [FromQuery] WalletType? walletType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetPayoutBatchesQuery(status, walletType, page, pageSize), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<PagedResult<PayoutBatchRowDto>>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    /// <summary>GET /api/v1/admin/payouts/batches/{batchId} — batch detail with per-member attempt outcomes.</summary>
    [HttpGet("payouts/batches/{batchId}")]
    public async Task<IActionResult> GetBatchDetail(string batchId, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetPayoutBatchDetailQuery(batchId), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<PayoutBatchDetailDto>.Ok(r.Value!))
            : NotFound(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    /// <summary>GET /api/v1/admin/payouts/batches/{batchId}/csv — download the export CSV for this batch.</summary>
    [HttpGet("payouts/batches/{batchId}/csv")]
    public async Task<IActionResult> DownloadBatchCsv(string batchId, CancellationToken ct = default)
    {
        var batch = await _db.PayoutBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

        if (batch is null)
            return NotFound(ApiResponse<object>.Fail("PAYOUT_BATCH_NOT_FOUND", "Batch not found"));

        var bytes = await _receiptStorage.ReadAsync($"payout-batch-{batchId}.csv", ct);
        if (bytes is null)
            return NotFound(ApiResponse<object>.Fail("BATCH_CSV_MISSING", "Export CSV missing"));

        return File(bytes, "text/csv", $"payout-batch-{batchId}.csv");
    }

    /// <summary>POST /api/v1/admin/payouts/batches/{batchId}/reconcile — upload gateway results CSV and reconcile per-row.</summary>
    [HttpPost("payouts/batches/{batchId}/reconcile")]
    [RequestSizeLimit(20_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20_000_000)]
    public async Task<IActionResult> Reconcile(
        string batchId,
        [FromForm] IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("INVALID_FILE", "No results file provided"));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var csv = System.Text.Encoding.UTF8.GetString(ms.ToArray());

        var r = await _mediator.Send(new ReconcilePayoutBatchCommand(batchId, csv), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<BatchReconcileResult>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    /// <summary>POST /api/v1/admin/payouts/batches/{batchId}/mark-paid — bulk-mark all pending attempts in this batch as paid.</summary>
    [HttpPost("payouts/batches/{batchId}/mark-paid")]
    public async Task<IActionResult> MarkPaid(string batchId, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new MarkPayoutBatchPaidCommand(batchId), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<BatchReconcileResult>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    /// <summary>POST /api/v1/admin/payouts/batches/{batchId}/cancel — cancel this batch and release all reserved earnings.</summary>
    [HttpPost("payouts/batches/{batchId}/cancel")]
    public async Task<IActionResult> CancelBatch(string batchId, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new CancelPayoutBatchCommand(batchId), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<BatchReconcileResult>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    // ── Receipt Email Toggle ─────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/payouts/receipt-email-setting — current auto-send toggle (default ON).</summary>
    [HttpGet("payouts/receipt-email-setting")]
    public async Task<IActionResult> GetReceiptEmailSetting(CancellationToken ct = default)
    {
        var p = await _db.GlobalParameters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == PayoutReceiptService.AutoSendKey, ct);
        var on = p is null || !bool.TryParse(p.Value, out var v) || v;
        return Ok(ApiResponse<bool>.Ok(on));
    }

    public record UpdateReceiptEmailSettingRequest(bool AutoSend);

    /// <summary>PUT /api/v1/admin/payouts/receipt-email-setting — enable or disable auto-send.</summary>
    [HttpPut("payouts/receipt-email-setting")]
    public async Task<IActionResult> UpdateReceiptEmailSetting(
        [FromBody] UpdateReceiptEmailSettingRequest body,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "admin";
        var p = await _db.GlobalParameters.FirstOrDefaultAsync(x => x.Key == PayoutReceiptService.AutoSendKey, ct);
        if (p is null)
        {
            p = new MLMConquerorGlobalEdition.Domain.Entities.General.GlobalParameter
            {
                Key = PayoutReceiptService.AutoSendKey,
                Value = body.AutoSend.ToString(),
                CreatedBy = actor,
                CreationDate = now
            };
            _db.GlobalParameters.Add(p);
        }
        else
        {
            p.Value = body.AutoSend.ToString();
            p.LastUpdateBy = actor;
            p.LastUpdateDate = now;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(body.AutoSend));
    }
}
