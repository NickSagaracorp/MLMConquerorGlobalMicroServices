using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;
using MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.GetSettings;
using MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.UpdateSettings;
using MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.PreviewPlan;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Admin endpoints for the recurring billing / dunning engine.
/// Routes under /api/v1/admin/billing/recurring-*
/// Also exposes the high-volume performance tunable endpoints under
/// /api/v1/admin/billing/recurring-performance/
/// </summary>
[ApiController]
[Route("api/v1/admin/billing")]
[Authorize(Roles = "SuperAdmin,Admin,BillingManager")]
public class AdminRecurringBillingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMediator    _mediator;

    private const string ConsolidationMinimumKey = "DailyResidualConsolidationMinimum";

    public AdminRecurringBillingController(AppDbContext db, IMediator mediator)
    {
        _db       = db;
        _mediator = mediator;
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

    /// <summary>
    /// Filas por página como máximo en los grids de estados e intentos. Estas tablas crecen con cada miembro y cada
    /// cobro (con un millón de miembros son millones de filas) y el grid no pinta bien más de 100; el selector de
    /// tamaño del AdminWeb ofrece lo mismo.
    /// </summary>
    internal const int MaxGridPageSize = 100;

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
        var query = FiltrarEstados(memberId, status, planId);

        var total = await query.CountAsync(ct);
        var items = await ProyectarEstados(query
                .OrderByDescending(s => s.LastAttemptAt ?? s.CreationDate)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<RecurringStateDto>>.Ok(new PagedResult<RecurringStateDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    /// <summary>
    /// POST /api/v1/admin/billing/recurring-states/grid — lectura del grid de servidor del AdminWeb. El grid manda
    /// en el cuerpo la página, los filtros de columna y el orden, y así filtran y ordenan sobre TODA la tabla y no
    /// solo sobre la página cargada. Estado, plan y miembro (los controles de arriba de la página) siguen llegando
    /// por query string y se aplican antes de proyectar, sobre las columnas de la tabla.
    /// </summary>
    [HttpPost("recurring-states/grid")]
    public async Task<IActionResult> GetStatesGrid(
        [FromBody] GridDataRequest request,
        [FromQuery] string? memberId = null,
        [FromQuery] RecurringBillingStatus? status = null,
        [FromQuery] int? planId = null,
        CancellationToken ct = default)
    {
        var query = FiltrarEstados(memberId, status, planId);

        // Sin orden pedido por el grid, lo más reciente primero: el último intento y, si nunca se intentó, el alta
        // del estado (el mismo orden del GET). Va antes de proyectar porque la fecha de alta no viaja en el DTO; si
        // el usuario ordena por una columna, ToGridResultAsync lo reemplaza. El Id desempata para que el paginado
        // sea estable y una fila no salte de una página a otra.
        if (request.Sorts is not { Count: > 0 })
            query = query.OrderByDescending(s => s.LastAttemptAt ?? s.CreationDate).ThenByDescending(s => s.Id);

        var result = await ProyectarEstados(query).ToGridResultAsync(
            request, EstadosBuscables, MaxGridPageSize, ct);

        return Ok(ApiResponse<PagedResult<RecurringStateDto>>.Ok(result));
    }

    /// <summary>Columnas de texto en las que busca el término libre del grid de estados.</summary>
    private static readonly string[] EstadosBuscables =
    {
        nameof(RecurringStateDto.MemberId),
        nameof(RecurringStateDto.PlanName),
        nameof(RecurringStateDto.MembershipSubscriptionId),
    };

    /// <summary>Los filtros de la página (miembro, estado, plan), comunes al GET y al grid.</summary>
    private IQueryable<SubscriptionBillingState> FiltrarEstados(
        string? memberId, RecurringBillingStatus? status, int? planId)
    {
        var query = _db.SubscriptionBillingStates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(memberId))
        {
            var id = memberId.Trim();
            query = query.Where(s => s.MemberId == id);
        }
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        if (planId.HasValue) query = query.Where(s => s.RecurringBillingPlanId == planId.Value);

        return query;
    }

    /// <summary>
    /// Proyección al DTO asignando cada propiedad por nombre (no por constructor): así EF puede componer encima los
    /// filtros y el orden dinámicos del grid y traducirlos a las columnas de la tabla.
    /// </summary>
    private static IQueryable<RecurringStateDto> ProyectarEstados(IQueryable<SubscriptionBillingState> query) =>
        query.Select(s => new RecurringStateDto
        {
            Id                        = s.Id,
            MemberId                  = s.MemberId,
            MembershipSubscriptionId  = s.MembershipSubscriptionId,
            RecurringBillingPlanId    = s.RecurringBillingPlanId,
            PlanName                  = s.Plan!.Name,
            BillingAnchorDate         = s.BillingAnchorDate,
            LastSuccessfulBillingDate = s.LastSuccessfulBillingDate,
            NextBillingDate           = s.NextBillingDate,
            CurrentAttemptIndex       = s.CurrentAttemptIndex,
            NextAttemptDate           = s.NextAttemptDate,
            Status                    = s.Status,
            LastAttemptAt             = s.LastAttemptAt,
            LastAttemptOutcome        = s.LastAttemptOutcome,
            LastFailureReason         = s.LastFailureReason,
            Purpose                   = s.Purpose,
        });

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
        var query = FiltrarIntentos(memberId, outcome, fundingSource, fromDate, toDate);

        var total = await query.CountAsync(ct);
        var items = await ProyectarIntentos(query
                .OrderByDescending(a => a.AttemptedAt)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<RecurringAttemptDto>>.Ok(new PagedResult<RecurringAttemptDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    /// <summary>
    /// POST /api/v1/admin/billing/recurring-attempts/grid — lectura del grid de servidor del AdminWeb. Resultado,
    /// fuente de fondos, miembro y rango de fechas (los controles de la página) llegan por query string y se aplican
    /// antes de proyectar; filtros de columna, orden y página llegan en el cuerpo y cubren toda la tabla.
    /// </summary>
    [HttpPost("recurring-attempts/grid")]
    public async Task<IActionResult> GetAttemptsGrid(
        [FromBody] GridDataRequest request,
        [FromQuery] string? memberId = null,
        [FromQuery] RecurringAttemptOutcome? outcome = null,
        [FromQuery] RecurringFundingSource? fundingSource = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var query = FiltrarIntentos(memberId, outcome, fundingSource, fromDate, toDate);

        // Sin orden pedido, el intento más reciente primero (como el GET). El Id desempata los del mismo instante,
        // que el motor registra en lote, para que una fila no salte de página al paginar.
        if (request.Sorts is not { Count: > 0 })
            request.Sorts = new List<GridSort>
            {
                new() { Field = nameof(RecurringAttemptDto.AttemptedAt), Direction = "desc" },
                new() { Field = nameof(RecurringAttemptDto.Id),          Direction = "desc" },
            };

        var result = await ProyectarIntentos(query).ToGridResultAsync(
            request, IntentosBuscables, MaxGridPageSize, ct);

        return Ok(ApiResponse<PagedResult<RecurringAttemptDto>>.Ok(result));
    }

    /// <summary>Columnas de texto en las que busca el término libre del grid de intentos.</summary>
    private static readonly string[] IntentosBuscables =
    {
        nameof(RecurringAttemptDto.MemberId),
        nameof(RecurringAttemptDto.ProductId),
        nameof(RecurringAttemptDto.OrderId),
        nameof(RecurringAttemptDto.PaymentHistoryId),
    };

    /// <summary>Los filtros de la página, comunes al GET y al grid.</summary>
    private IQueryable<RecurringBillingAttempt> FiltrarIntentos(
        string? memberId, RecurringAttemptOutcome? outcome, RecurringFundingSource? fundingSource,
        DateTime? fromDate, DateTime? toDate)
    {
        var query = _db.RecurringBillingAttempts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(memberId))
        {
            var id = memberId.Trim();
            query = query.Where(a => a.MemberId == id);
        }
        if (outcome.HasValue)       query = query.Where(a => a.Outcome == outcome.Value);
        if (fundingSource.HasValue) query = query.Where(a => a.FundingSource == fundingSource.Value);
        if (fromDate.HasValue)      query = query.Where(a => a.AttemptedAt >= fromDate.Value);
        if (toDate.HasValue)        query = query.Where(a => a.AttemptedAt <= toDate.Value);

        return query;
    }

    /// <summary>Proyección asignando por nombre, para que EF componga encima el filtro y el orden del grid.</summary>
    private static IQueryable<RecurringAttemptDto> ProyectarIntentos(IQueryable<RecurringBillingAttempt> query) =>
        query.Select(a => new RecurringAttemptDto
        {
            Id                           = a.Id,
            SubscriptionBillingStateId   = a.SubscriptionBillingStateId,
            MemberId                     = a.MemberId,
            ProductId                    = a.ProductId,
            AttemptIndex                 = a.AttemptIndex,
            AttemptedAt                  = a.AttemptedAt,
            Amount                       = a.Amount,
            FundingSource                = a.FundingSource,
            Outcome                      = a.Outcome,
            PaymentHistoryId             = a.PaymentHistoryId,
            OrderId                      = a.OrderId,
            TokenTransactionId           = a.TokenTransactionId,
            CommissionDeductionEarningId = a.CommissionDeductionEarningId,
            GatewayChargeAttemptId       = a.GatewayChargeAttemptId,
            FailureReason                = a.FailureReason,
        });

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

    // ── Recurring Performance Settings (§10.7 high-volume tunables) ───────────

    /// <summary>
    /// GET /api/v1/admin/billing/recurring-performance/settings
    /// Returns all §10.7 high-volume tunables as a grouped structured payload.
    /// Includes supported* arrays for dropdown population.
    /// </summary>
    [HttpGet("recurring-performance/settings")]
    public async Task<IActionResult> GetRecurringPerformanceSettings(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetRecurringPerformanceSettingsQuery(), ct);

        if (!result.IsSuccess)
            return StatusCode(500, ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<RecurringPerformanceSettingsDto>.Ok(result.Value!));
    }

    /// <summary>
    /// PUT /api/v1/admin/billing/recurring-performance/settings
    /// Validates and persists all §10.7 high-volume tunables.
    /// </summary>
    [HttpPut("recurring-performance/settings")]
    public async Task<IActionResult> UpdateRecurringPerformanceSettings(
        [FromBody] UpdateRecurringPerformanceSettingsRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new UpdateRecurringPerformanceSettingsCommand(request), ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "VALIDATION")
                return BadRequest(ApiResponse<object>.Fail(result.ErrorCode, result.Error!));

            return StatusCode(500, ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));
        }

        return Ok(ApiResponse<RecurringPerformanceSettingsDto>.Ok(result.Value!));
    }

    /// <summary>
    /// GET /api/v1/admin/billing/recurring-performance/preview
    /// Runs the Stage 1 planner in dry-run mode against today's pending workload.
    /// No RecurringBillingBatch or RecurringBillingBatchShard rows are written.
    /// </summary>
    [HttpGet("recurring-performance/preview")]
    public async Task<IActionResult> GetRecurringPerformancePreview(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new PreviewRecurringPerformanceQuery(), ct);

        if (!result.IsSuccess)
            return StatusCode(500, ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<RecurringPerformancePreviewDto>.Ok(result.Value!));
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

    // Records con propiedades init y no posicionales: el grid compone filtros y orden dinámicos sobre la proyección
    // y EF solo traduce el acceso a una propiedad cuando la proyección la asigna por nombre. El JSON no cambia.
    public record RecurringStateDto
    {
        public string Id { get; init; } = string.Empty;
        public string MemberId { get; init; } = string.Empty;
        public string? MembershipSubscriptionId { get; init; }
        public int RecurringBillingPlanId { get; init; }
        public string PlanName { get; init; } = string.Empty;
        public DateTime BillingAnchorDate { get; init; }
        public DateTime? LastSuccessfulBillingDate { get; init; }
        public DateTime NextBillingDate { get; init; }
        public int CurrentAttemptIndex { get; init; }
        public DateTime NextAttemptDate { get; init; }
        public RecurringBillingStatus Status { get; init; }
        public DateTime? LastAttemptAt { get; init; }
        public string? LastAttemptOutcome { get; init; }
        public string? LastFailureReason { get; init; }
        public BillingStatePurpose Purpose { get; init; }
    }

    public record RecurringAttemptDto
    {
        public long Id { get; init; }
        public string SubscriptionBillingStateId { get; init; } = string.Empty;
        public string MemberId { get; init; } = string.Empty;
        public string ProductId { get; init; } = string.Empty;
        public int AttemptIndex { get; init; }
        public DateTime AttemptedAt { get; init; }
        public decimal Amount { get; init; }
        public RecurringFundingSource FundingSource { get; init; }
        public RecurringAttemptOutcome Outcome { get; init; }
        public string? PaymentHistoryId { get; init; }
        public string? OrderId { get; init; }
        public long? TokenTransactionId { get; init; }
        public string? CommissionDeductionEarningId { get; init; }
        public long? GatewayChargeAttemptId { get; init; }
        public string? FailureReason { get; init; }
    }

    public record RecurringBillingProcessorResultDto(
        string BillingStateId, string Outcome, string? FundingSource,
        string? PaymentHistoryId, string? FailureReason);

    public record RecurringSettingsDto(string Key, decimal Value, string Description);

    public record UpdateRecurringSettingsRequest(decimal DailyResidualConsolidationMinimum);
}
