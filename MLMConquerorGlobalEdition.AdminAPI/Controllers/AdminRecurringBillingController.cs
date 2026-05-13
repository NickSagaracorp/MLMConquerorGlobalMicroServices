using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Admin endpoints for the recurring billing / dunning engine.
/// Routes under /api/v1/admin/billing/recurring-*
/// </summary>
[ApiController]
[Route("api/v1/admin/billing")]
[Authorize(Roles = "SuperAdmin,Admin,BillingManager")]
public class AdminRecurringBillingController : ControllerBase
{
    private readonly AppDbContext _db;

    private const string ConsolidationMinimumKey = "DailyResidualConsolidationMinimum";

    public AdminRecurringBillingController(AppDbContext db)
    {
        _db = db;
    }

    // ── Recurring Plans ─────────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/billing/recurring-plans — list all plans.</summary>
    [HttpGet("recurring-plans")]
    public async Task<IActionResult> GetPlans(
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = _db.RecurringBillingPlans
            .AsNoTracking()
            .Include(p => p.PlanProducts)
            .AsQueryable();

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var items = await query.OrderBy(p => p.Id)
            .Select(p => ToPlanDto(p))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<RecurringPlanDto>>.Ok(items));
    }

    /// <summary>GET /api/v1/admin/billing/recurring-plans/{id}</summary>
    [HttpGet("recurring-plans/{id:int}")]
    public async Task<IActionResult> GetPlan(int id, CancellationToken ct = default)
    {
        var entity = await _db.RecurringBillingPlans
            .AsNoTracking()
            .Include(p => p.PlanProducts)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("PLAN_NOT_FOUND", $"RecurringBillingPlan {id} not found."));

        return Ok(ApiResponse<RecurringPlanDto>.Ok(ToPlanDto(entity)));
    }

    /// <summary>POST /api/v1/admin/billing/recurring-plans — create a new plan.</summary>
    [HttpPost("recurring-plans")]
    public async Task<IActionResult> CreatePlan(
        [FromBody] RecurringPlanFormRequest request, CancellationToken ct = default)
    {
        var cadenceError = ValidateCadence(request.RetryCadenceDays);
        if (cadenceError is not null)
            return BadRequest(ApiResponse<object>.Fail("INVALID_CADENCE", cadenceError));

        var actor = User.Identity?.Name ?? "admin";
        var now   = DateTime.UtcNow;

        var entity = new RecurringBillingPlan
        {
            Name                          = request.Name,
            CycleType                     = request.CycleType,
            RetryCadenceDays              = request.RetryCadenceDays,
            OnAllRetriesFail              = request.OnAllRetriesFail,
            StopAfterUnbilledDays         = request.StopAfterUnbilledDays,
            PayFromCommissionBalanceFirst  = request.PayFromCommissionBalanceFirst,
            TokenTypeId                   = request.TokenTypeId,
            FixedAmountOverride           = request.FixedAmountOverride,
            IsActive                      = request.IsActive,
            CreatedBy                     = actor,
            CreationDate                  = now
        };

        foreach (var pp in request.PlanProducts)
            entity.PlanProducts.Add(new RecurringBillingPlanProduct
            {
                ProductId          = pp.ProductId,
                TokenTypeIdOverride = pp.TokenTypeIdOverride,
                CreatedBy          = actor,
                CreationDate       = now
            });

        _db.RecurringBillingPlans.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetPlan), new { id = entity.Id },
            ApiResponse<RecurringPlanDto>.Ok(ToPlanDto(entity)));
    }

    /// <summary>PUT /api/v1/admin/billing/recurring-plans/{id} — update a plan.</summary>
    [HttpPut("recurring-plans/{id:int}")]
    public async Task<IActionResult> UpdatePlan(
        int id, [FromBody] RecurringPlanFormRequest request, CancellationToken ct = default)
    {
        var entity = await _db.RecurringBillingPlans
            .Include(p => p.PlanProducts)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("PLAN_NOT_FOUND", $"RecurringBillingPlan {id} not found."));

        var cadenceError = ValidateCadence(request.RetryCadenceDays);
        if (cadenceError is not null)
            return BadRequest(ApiResponse<object>.Fail("INVALID_CADENCE", cadenceError));

        var actor = User.Identity?.Name ?? "admin";
        var now   = DateTime.UtcNow;

        entity.Name                         = request.Name;
        entity.CycleType                    = request.CycleType;
        entity.RetryCadenceDays             = request.RetryCadenceDays;
        entity.OnAllRetriesFail             = request.OnAllRetriesFail;
        entity.StopAfterUnbilledDays        = request.StopAfterUnbilledDays;
        entity.PayFromCommissionBalanceFirst  = request.PayFromCommissionBalanceFirst;
        entity.TokenTypeId                  = request.TokenTypeId;
        entity.FixedAmountOverride          = request.FixedAmountOverride;
        entity.IsActive                     = request.IsActive;
        entity.LastUpdateBy                 = actor;
        entity.LastUpdateDate               = now;

        _db.RecurringBillingPlanProducts.RemoveRange(entity.PlanProducts);
        entity.PlanProducts.Clear();
        foreach (var pp in request.PlanProducts)
            entity.PlanProducts.Add(new RecurringBillingPlanProduct
            {
                ProductId          = pp.ProductId,
                TokenTypeIdOverride = pp.TokenTypeIdOverride,
                CreatedBy          = actor,
                CreationDate       = now
            });

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<RecurringPlanDto>.Ok(ToPlanDto(entity)));
    }

    /// <summary>DELETE /api/v1/admin/billing/recurring-plans/{id} — deactivate a plan.</summary>
    [HttpDelete("recurring-plans/{id:int}")]
    public async Task<IActionResult> DeactivatePlan(int id, CancellationToken ct = default)
    {
        var entity = await _db.RecurringBillingPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("PLAN_NOT_FOUND", $"RecurringBillingPlan {id} not found."));

        entity.IsActive       = false;
        entity.LastUpdateBy   = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { id, isActive = false }, "Plan deactivated."));
    }

    // ── Recurring States ────────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/billing/recurring-states — paged, read-only.</summary>
    [HttpGet("recurring-states")]
    public async Task<IActionResult> GetStates(
        [FromQuery] string? memberId = null,
        [FromQuery] RecurringBillingStatus? status = null,
        [FromQuery] int? planId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.SubscriptionBillingStates
            .AsNoTracking()
            .Include(s => s.Plan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(memberId)) query = query.Where(s => s.MemberId == memberId);
        if (status.HasValue)                       query = query.Where(s => s.Status == status.Value);
        if (planId.HasValue)                       query = query.Where(s => s.RecurringBillingPlanId == planId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.LastAttemptAt ?? s.CreationDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new RecurringStateDto(
                s.Id, s.MemberId, s.MembershipSubscriptionId, s.RecurringBillingPlanId,
                s.Plan!.Name, s.BillingAnchorDate, s.LastSuccessfulBillingDate,
                s.NextBillingDate, s.CurrentAttemptIndex, s.NextAttemptDate,
                s.Status, s.LastAttemptAt, s.LastAttemptOutcome, s.LastFailureReason))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<RecurringStateDto>>.Ok(new PagedResult<RecurringStateDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    // ── Recurring Attempts ──────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/billing/recurring-attempts — paged audit log, read-only.</summary>
    [HttpGet("recurring-attempts")]
    public async Task<IActionResult> GetAttempts(
        [FromQuery] string? memberId = null,
        [FromQuery] RecurringAttemptOutcome? outcome = null,
        [FromQuery] RecurringFundingSource? fundingSource = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.RecurringBillingAttempts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(memberId)) query = query.Where(a => a.MemberId == memberId);
        if (outcome.HasValue)       query = query.Where(a => a.Outcome == outcome.Value);
        if (fundingSource.HasValue) query = query.Where(a => a.FundingSource == fundingSource.Value);
        if (fromDate.HasValue)      query = query.Where(a => a.AttemptedAt >= fromDate.Value);
        if (toDate.HasValue)        query = query.Where(a => a.AttemptedAt <= toDate.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.AttemptedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new RecurringAttemptDto(
                a.Id, a.SubscriptionBillingStateId, a.MemberId, a.ProductId,
                a.AttemptIndex, a.AttemptedAt, a.Amount, a.FundingSource, a.Outcome,
                a.PaymentHistoryId, a.OrderId, a.TokenTransactionId,
                a.CommissionDeductionEarningId, a.GatewayChargeAttemptId, a.FailureReason))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<RecurringAttemptDto>>.Ok(new PagedResult<RecurringAttemptDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    // ── Bill Now ────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/v1/admin/billing/recurring-states/{id}/bill-now
    /// Support action: immediately process the billing attempt for this state,
    /// reviving a Stopped state if needed.
    /// </summary>
    [HttpPost("recurring-states/{id}/bill-now")]
    public async Task<IActionResult> BillNow(
        string id,
        [FromServices] MLMConquerorGlobalEdition.Billing.Services.Recurring.IRecurringBillingProcessor processor,
        CancellationToken ct = default)
    {
        var state = await _db.SubscriptionBillingStates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (state is null)
            return NotFound(ApiResponse<object>.Fail("STATE_NOT_FOUND",
                $"SubscriptionBillingState '{id}' not found."));

        var result = await processor.ProcessAsync(id, forceBillNow: true, ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<RecurringBillingProcessorResultDto>.Ok(
            new RecurringBillingProcessorResultDto(
                result.Value!.BillingStateId,
                result.Value.Outcome,
                result.Value.FundingSource,
                result.Value.PaymentHistoryId,
                result.Value.FailureReason)));
    }

    // ── Recurring Settings (DailyResidualConsolidationMinimum) ─────────────

    /// <summary>GET /api/v1/admin/billing/recurring-settings</summary>
    [HttpGet("recurring-settings")]
    public async Task<IActionResult> GetRecurringSettings(CancellationToken ct = default)
    {
        var param = await _db.GlobalParameters
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Key == ConsolidationMinimumKey, ct);

        var value = param is not null && decimal.TryParse(param.Value, out var parsed) ? parsed : 100m;

        return Ok(ApiResponse<RecurringSettingsDto>.Ok(new RecurringSettingsDto(
            ConsolidationMinimumKey,
            value,
            "Minimum pending daily-residual balance (USD) before consolidation into a CommissionEarning credit.")));
    }

    /// <summary>PUT /api/v1/admin/billing/recurring-settings</summary>
    [HttpPut("recurring-settings")]
    public async Task<IActionResult> UpdateRecurringSettings(
        [FromBody] UpdateRecurringSettingsRequest request, CancellationToken ct = default)
    {
        if (request.DailyResidualConsolidationMinimum < 0)
            return BadRequest(ApiResponse<object>.Fail("INVALID_VALUE",
                "DailyResidualConsolidationMinimum must be >= 0."));

        var now   = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "admin";

        var param = await _db.GlobalParameters
            .FirstOrDefaultAsync(p => p.Key == ConsolidationMinimumKey, ct);

        if (param is null)
        {
            param = new Domain.Entities.General.GlobalParameter
            {
                Key          = ConsolidationMinimumKey,
                Value        = request.DailyResidualConsolidationMinimum.ToString("F2"),
                CreatedBy    = actor,
                CreationDate = now
            };
            _db.GlobalParameters.Add(param);
        }
        else
        {
            param.Value         = request.DailyResidualConsolidationMinimum.ToString("F2");
            param.LastUpdateBy  = actor;
            param.LastUpdateDate = now;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<RecurringSettingsDto>.Ok(new RecurringSettingsDto(
            ConsolidationMinimumKey,
            request.DailyResidualConsolidationMinimum,
            "Minimum pending daily-residual balance (USD) before consolidation.")));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string? ValidateCadence(string cadence)
    {
        if (string.IsNullOrWhiteSpace(cadence))
            return "RetryCadenceDays is required.";

        var parts = cadence.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "RetryCadenceDays must contain at least one entry.";

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var days) || days <= 0)
                return $"RetryCadenceDays entry '{part}' is not a valid positive integer.";
        }

        return null;
    }

    private static RecurringPlanDto ToPlanDto(RecurringBillingPlan p) => new(
        p.Id, p.Name, p.CycleType, p.RetryCadenceDays, p.OnAllRetriesFail,
        p.StopAfterUnbilledDays, p.PayFromCommissionBalanceFirst,
        p.TokenTypeId, p.FixedAmountOverride, p.IsActive,
        p.PlanProducts.Select(pp => new PlanProductDto(pp.Id, pp.ProductId, pp.TokenTypeIdOverride)).ToList());

    // ── DTOs ────────────────────────────────────────────────────────────────

    public record RecurringPlanDto(
        int Id, string Name, RecurringCycleType CycleType, string RetryCadenceDays,
        RecurringFailurePolicy OnAllRetriesFail, int? StopAfterUnbilledDays,
        bool PayFromCommissionBalanceFirst, int? TokenTypeId, decimal? FixedAmountOverride,
        bool IsActive, List<PlanProductDto> PlanProducts);

    public record PlanProductDto(int Id, string ProductId, int? TokenTypeIdOverride);

    public record RecurringPlanFormRequest(
        string Name, RecurringCycleType CycleType, string RetryCadenceDays,
        RecurringFailurePolicy OnAllRetriesFail, int? StopAfterUnbilledDays,
        bool PayFromCommissionBalanceFirst, int? TokenTypeId, decimal? FixedAmountOverride,
        bool IsActive, List<PlanProductFormDto> PlanProducts);

    public record PlanProductFormDto(string ProductId, int? TokenTypeIdOverride);

    public record RecurringStateDto(
        string Id, string MemberId, string MembershipSubscriptionId, int RecurringBillingPlanId,
        string PlanName, DateTime BillingAnchorDate, DateTime? LastSuccessfulBillingDate,
        DateTime NextBillingDate, int CurrentAttemptIndex, DateTime NextAttemptDate,
        RecurringBillingStatus Status, DateTime? LastAttemptAt, string? LastAttemptOutcome,
        string? LastFailureReason);

    public record RecurringAttemptDto(
        long Id, string SubscriptionBillingStateId, string MemberId, string ProductId,
        int AttemptIndex, DateTime AttemptedAt, decimal Amount,
        RecurringFundingSource FundingSource, RecurringAttemptOutcome Outcome,
        string? PaymentHistoryId, string? OrderId, long? TokenTransactionId,
        string? CommissionDeductionEarningId, long? GatewayChargeAttemptId, string? FailureReason);

    public record RecurringBillingProcessorResultDto(
        string BillingStateId, string Outcome, string? FundingSource,
        string? PaymentHistoryId, string? FailureReason);

    public record RecurringSettingsDto(string Key, decimal Value, string Description);

    public record UpdateRecurringSettingsRequest(decimal DailyResidualConsolidationMinimum);
}
