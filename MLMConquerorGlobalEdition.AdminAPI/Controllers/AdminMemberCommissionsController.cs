using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Commission data for a specific member — used by Admin Member Profile commissions tab.
/// Routes: /api/v1/admin/members/{memberId}/commissions/*
///
/// All read endpoints are thin pass-throughs to <see cref="ICommissionsService"/> — the
/// single source of truth shared with BizCenter. Admin-only mutations (FSB backfill) and
/// admin-shaped reports (week-selector enrollments) remain inline.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/commissions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberCommissionsController : ControllerBase
{
    private readonly AppDbContext        _db;
    private readonly ICommissionsService _service;

    public AdminMemberCommissionsController(AppDbContext db, ICommissionsService service)
    {
        _db      = db;
        _service = service;
    }

    // ─── Summary & Overview ───────────────────────────────────────────────

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(string memberId, CancellationToken ct = default)
        => Ok(ApiResponse<CommissionSummaryView>.Ok(await _service.GetSummaryAsync(memberId, ct)));

    [HttpGet]
    public async Task<IActionResult> GetCommissions(
        string memberId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        [FromQuery] string? status   = null,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        CancellationToken ct = default)
    {
        DateTime? fromDate = DateTime.TryParse(from, out var f) ? f : null;
        DateTime? toDate   = DateTime.TryParse(to,   out var t) ? t.AddDays(1) : null;

        var result = await _service.GetCommissionsAsync(memberId, page, pageSize, status, fromDate, toDate, ct);
        return Ok(ApiResponse<PagedResult<CommissionEarningView>>.Ok(result));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(string memberId, CancellationToken ct = default)
        => Ok(ApiResponse<List<CommissionHistoryYearView>>.Ok(await _service.GetHistoryAsync(memberId, ct)));

    [HttpGet("breakdown")]
    public async Task<IActionResult> GetBreakdown(
        string memberId,
        [FromQuery] DateTime  paymentDate,
        [FromQuery] DateTime? earnedDate = null,
        CancellationToken ct = default)
        => Ok(ApiResponse<List<CommissionBreakdownView>>.Ok(
            await _service.GetBreakdownAsync(memberId, paymentDate, earnedDate, ct)));

    [HttpGet("month-breakdown")]
    public async Task<IActionResult> GetMonthBreakdown(
        string memberId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
        => Ok(ApiResponse<List<CommissionMonthBreakdownView>>.Ok(
            await _service.GetMonthBreakdownAsync(memberId, year, month, ct)));

    // ─── Dual Residual ────────────────────────────────────────────────────

    [HttpGet("dual-residual")]
    public async Task<IActionResult> GetDualResidual(
        string memberId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<CommissionEarningView>>.Ok(
            await _service.GetDualResidualAsync(memberId, page, pageSize, ct)));

    // ─── Fast Start Bonus ─────────────────────────────────────────────────

    [HttpGet("fast-start-bonus/summary")]
    public async Task<IActionResult> GetFastStartBonusSummary(string memberId, CancellationToken ct = default)
        => Ok(ApiResponse<CommissionBonusSummaryView>.Ok(
            await _service.GetFastStartBonusSummaryAsync(memberId, ct)));

    [HttpGet("fast-start-bonus")]
    public async Task<IActionResult> GetFastStartBonus(
        string memberId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<CommissionEarningView>>.Ok(
            await _service.GetFastStartBonusAsync(memberId, page, pageSize, ct)));

    // ─── Presidential Bonus ───────────────────────────────────────────────

    [HttpGet("presidential-bonus/summary")]
    public async Task<IActionResult> GetPresidentialBonusSummary(string memberId, CancellationToken ct = default)
        => Ok(ApiResponse<CommissionBonusSummaryView>.Ok(
            await _service.GetPresidentialBonusSummaryAsync(memberId, ct)));

    [HttpGet("presidential-bonus")]
    public async Task<IActionResult> GetPresidentialBonus(
        string memberId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<CommissionEarningView>>.Ok(
            await _service.GetPresidentialBonusAsync(memberId, page, pageSize, ct)));

    // ─── Boost Bonus ──────────────────────────────────────────────────────

    [HttpGet("boost-bonus/week-stats")]
    public async Task<IActionResult> GetBoostBonusWeekStats(string memberId, CancellationToken ct = default)
        => Ok(ApiResponse<BoostBonusWeekStatsView>.Ok(
            await _service.GetBoostBonusWeekStatsAsync(memberId, ct)));

    [HttpGet("boost-bonus/summary")]
    public async Task<IActionResult> GetBoostBonusSummary(string memberId, CancellationToken ct = default)
        => Ok(ApiResponse<BoostBonusMemberSummaryView>.Ok(
            await _service.GetBoostBonusMemberSummaryAsync(memberId, ct)));

    [HttpGet("boost-bonus")]
    public async Task<IActionResult> GetBoostBonus(
        string memberId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<CommissionEarningView>>.Ok(
            await _service.GetBoostBonusAsync(memberId, page, pageSize, ct)));

    /// <summary>
    /// GET /boost-bonus/enrollments — admin-only week-selector view of new Elite/Turbo
    /// enrollments in the downline. Not duplicated in BizCenter.
    /// </summary>
    [HttpGet("boost-bonus/enrollments")]
    public async Task<IActionResult> GetBoostBonusEnrollments(
        string memberId,
        [FromQuery] string week = "current",
        CancellationToken ct = default)
    {
        var today            = DateTime.UtcNow;
        var daysToMon        = (int)today.DayOfWeek == 0 ? -6 : -((int)today.DayOfWeek - 1);
        var currentWeekStart = today.AddDays(daysToMon).Date;

        DateTime weekStart;
        if (week == "current" || !DateTime.TryParse(week, out var parsedDate))
            weekStart = currentWeekStart;
        else
            weekStart = parsedDate.Date.AddDays(-(((int)parsedDate.DayOfWeek + 6) % 7));

        var weekEnd = weekStart.AddDays(7);

        var availableWeeks = Enumerable.Range(0, 8)
            .Select(i =>
            {
                var ws = currentWeekStart.AddDays(-7 * i);
                return new BoostWeekOptionDto
                {
                    Value = i == 0 ? "current" : ws.ToString("yyyy-MM-dd"),
                    Label = i == 0 ? "Current Week" : $"Week of {ws:MMM d, yyyy}"
                };
            })
            .ToList();

        int[] eligibleLevelIds = [3, 4];
        var hierarchyFilter    = $"/{memberId}/";

        var rawEnrollments = await (
            from mp  in _db.MemberProfiles.AsNoTracking()
            join g   in _db.GenealogyTree.AsNoTracking()            on mp.MemberId           equals g.MemberId
            join sub in _db.MembershipSubscriptions.AsNoTracking()  on mp.MemberId           equals sub.MemberId
            join ml  in _db.MembershipLevels.AsNoTracking()         on sub.MembershipLevelId equals ml.Id
            where mp.EnrollDate >= weekStart
               && mp.EnrollDate <  weekEnd
               && eligibleLevelIds.Contains(sub.MembershipLevelId)
               && g.HierarchyPath.Contains(hierarchyFilter)
            select new
            {
                mp.MemberId,
                MemberName        = mp.FirstName + " " + mp.LastName,
                mp.Email,
                mp.EnrollDate,
                g.HierarchyPath,
                sub.MembershipLevelId,
                sub.SubscriptionStatus,
                sub.RenewalDate,
                MembershipType    = ml.Name
            }
        ).ToListAsync(ct);

        var deduped = rawEnrollments
            .GroupBy(m => m.MemberId)
            .Select(g => g.OrderByDescending(m => m.SubscriptionStatus == MembershipStatus.Active ? 1 : 0)
                          .ThenByDescending(m => m.EnrollDate)
                          .First())
            .OrderBy(m => m.EnrollDate)
            .ToList();

        var enrolledMemberIds = deduped.Select(m => m.MemberId).ToList();

        var orderNoData = enrolledMemberIds.Count > 0
            ? await _db.Orders.AsNoTracking()
                .Where(o => enrolledMemberIds.Contains(o.MemberId)
                         && o.Status == Domain.Entities.Orders.OrderStatus.Completed)
                .Select(o => new { o.MemberId, o.OrderNo, o.CreationDate })
                .ToListAsync(ct)
            : new();

        var orderNoMap = orderNoData
            .GroupBy(x => x.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreationDate).First().OrderNo ?? g.Key);

        var pointsData = enrolledMemberIds.Count > 0
            ? await (
                from o  in _db.Orders.AsNoTracking()
                join od in _db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
                join p  in _db.Products.AsNoTracking()     on od.ProductId equals p.Id
                where enrolledMemberIds.Contains(o.MemberId)
                   && o.Status == Domain.Entities.Orders.OrderStatus.Completed
                select new { o.MemberId, Points = od.Quantity * p.QualificationPoins }
              ).ToListAsync(ct)
            : new();

        var pointsMap = pointsData
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Points));

        static string GetLegMemberId(string path, string filter)
        {
            var idx = path.IndexOf(filter, StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            var after = path[(idx + filter.Length)..];
            var slash = after.IndexOf('/');
            return slash > 0 ? after[..slash] : after.TrimEnd('/');
        }

        var legMemberIds = deduped
            .Select(m => GetLegMemberId(m.HierarchyPath, hierarchyFilter))
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var legNameMap = await _db.MemberProfiles
            .AsNoTracking()
            .Where(mp => legMemberIds.Contains(mp.MemberId))
            .Select(mp => new { mp.MemberId, FullName = mp.FirstName + " " + mp.LastName })
            .ToDictionaryAsync(x => x.MemberId, x => x.FullName.Trim(), ct);

        var enrollmentItems = deduped
            .Select(m =>
            {
                var legId = GetLegMemberId(m.HierarchyPath, hierarchyFilter);
                return new BoostEnrollmentItemDto
                {
                    MemberId           = m.MemberId,
                    OrderNo            = orderNoMap.TryGetValue(m.MemberId, out var no) ? no : m.MemberId,
                    MemberName         = m.MemberName.Trim(),
                    MembershipType     = m.MembershipType,
                    Email              = m.Email ?? string.Empty,
                    LegName            = legNameMap.TryGetValue(legId, out var name) ? name : legId,
                    EnrollmentDate     = m.EnrollDate,
                    NextRebillingDate  = m.RenewalDate,
                    IsRebillingActive  = m.SubscriptionStatus == MembershipStatus.Active,
                    IsRebillingPending = m.SubscriptionStatus == MembershipStatus.Pending,
                    Points             = pointsMap.TryGetValue(m.MemberId, out var pts) ? pts : 0
                };
            })
            .ToList();

        var response = new BoostEnrollmentResponseDto
        {
            AvailableWeeks      = availableWeeks,
            GoldEnrollments     = enrollmentItems,
            PlatinumEnrollments = enrollmentItems
        };

        return Ok(ApiResponse<BoostEnrollmentResponseDto>.Ok(response));
    }

    // ─── Car Bonus ────────────────────────────────────────────────────────

    [HttpGet("car-bonus")]
    public async Task<IActionResult> GetCarBonus(
        string memberId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? from = null,
        [FromQuery] string? to   = null,
        CancellationToken ct = default)
    {
        DateTime? fromDate = DateTime.TryParse(from, out var f) ? f : null;
        DateTime? toDate   = DateTime.TryParse(to,   out var t) ? t.AddDays(1) : null;

        var result = await _service.GetCarBonusAsync(memberId, page, pageSize, fromDate, toDate, ct);
        return Ok(ApiResponse<PagedResult<CommissionEarningView>>.Ok(result));
    }

    [HttpGet("car-bonus/stats")]
    public async Task<IActionResult> GetCarBonusStats(string memberId, CancellationToken ct = default)
        => Ok(ApiResponse<CarBonusStatsView>.Ok(await _service.GetCarBonusStatsAsync(memberId, ct)));

    [HttpGet("car-bonus/ambassadors")]
    public async Task<IActionResult> GetCarBonusAmbassadors(string memberId, CancellationToken ct = default)
        => Ok(ApiResponse<List<CarBonusAmbassadorView>>.Ok(
            await _service.GetCarBonusAmbassadorsAsync(memberId, from: null, to: null, ct)));

    [HttpGet("car-bonus/ambassadors/{branchMemberId}/branch")]
    public async Task<IActionResult> GetCarBonusBranch(string branchMemberId, CancellationToken ct = default)
        => Ok(ApiResponse<CarBonusBranchView>.Ok(await _service.GetCarBonusBranchAsync(branchMemberId, ct)));

    // ─── Fast Start Bonus — Admin Backfill (mutating, admin-only) ─────────

    /// <summary>
    /// POST /fast-start-bonus/backfill — manually trigger FSB for a member who already has 2+
    /// Elite/Turbo sponsors in an open window but whose commission was never recorded.
    /// Idempotent: skips if earning already exists for the window.
    /// </summary>
    [HttpPost("fast-start-bonus/backfill")]
    public async Task<IActionResult> BackfillFastStartBonus(
        string memberId,
        [FromQuery] bool force  = false,
        [FromQuery] int? window = null,
        CancellationToken ct    = default)
    {
        var now = DateTime.UtcNow;

        // 1. Resolve UserId for countdown lookup
        var memberUserId = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => m.UserId)
            .FirstOrDefaultAsync(ct);

        if (memberUserId == default)
            return NotFound(ApiResponse<object>.Fail("MEMBER_NOT_FOUND", "Member profile not found."));

        var countdown = await _db.CommissionCountDowns
            .FirstOrDefaultAsync(c => c.MemberId == memberUserId, ct);

        if (countdown is null)
            return NotFound(ApiResponse<object>.Fail("COUNTDOWN_NOT_FOUND", "No FSB countdown record found for this member."));

        // If FSB3 dates were never written (e.g. FSB2 was backfilled against old code),
        // derive them from the FSB2 earning record so window=3 backfill works correctly.
        if (countdown.FastStartBonus3Start == default)
        {
            var fsb2TypeId = await _db.CommissionTypes.AsNoTracking()
                .Where(t => t.IsActive && t.IsPaidOnSignup && !t.IsSponsorBonus && t.TriggerOrder == 2)
                .Select(t => t.Id)
                .FirstOrDefaultAsync(ct);

            if (fsb2TypeId > 0)
            {
                var fsb2Earned = await _db.CommissionEarnings.AsNoTracking()
                    .Where(e => e.BeneficiaryMemberId == memberId
                             && e.CommissionTypeId == fsb2TypeId
                             && e.Status != CommissionEarningStatus.Cancelled)
                    .Select(e => (DateTime?)e.EarnedDate)
                    .FirstOrDefaultAsync(ct);

                if (fsb2Earned.HasValue)
                {
                    countdown.FastStartBonus3Start = fsb2Earned.Value;
                    countdown.FastStartBonus3End   = fsb2Earned.Value.AddDays(7);
                }
            }
        }

        // 2. Determine active window — iterate in priority order, skip already-earned ones.
        var candidates = new (int TriggerOrder, DateTime Start, DateTime End)[]
        {
            (1, countdown.FastStartBonus1Start,         countdown.FastStartBonus1End),
            (1, countdown.FastStartBonus1ExtendedStart, countdown.FastStartBonus1ExtendedEnd),
            (2, countdown.FastStartBonus2Start,         countdown.FastStartBonus2End),
            (3, countdown.FastStartBonus3Start,         countdown.FastStartBonus3End),
        };

        int activeWindow = 0;
        DateTime windowStart = default, windowEnd = default;

        foreach (var c2 in candidates)
        {
            if (window.HasValue && c2.TriggerOrder != window.Value) continue;
            if (!window.HasValue && (now < c2.Start || now > c2.End)) continue;

            var ct2 = await _db.CommissionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsActive && t.IsPaidOnSignup && !t.IsSponsorBonus
                                       && t.TriggerOrder == c2.TriggerOrder, ct);
            if (ct2 is null) continue;

            var earned = await _db.CommissionEarnings
                .AnyAsync(e => e.BeneficiaryMemberId == memberId
                            && e.CommissionTypeId == ct2.Id
                            && e.Status != CommissionEarningStatus.Cancelled, ct);
            if (earned && !force) continue;

            activeWindow = c2.TriggerOrder;
            windowStart  = c2.Start;
            windowEnd    = c2.End;
            break;
        }

        if (activeWindow == 0)
            return Ok(ApiResponse<object>.Ok(new { message = "No eligible FSB window found — all windows are closed, expired, or already earned." }));

        // 3. Get FSB CommissionType for this window
        var commType = await _db.CommissionTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive && t.IsPaidOnSignup && !t.IsSponsorBonus
                                   && t.TriggerOrder == activeWindow, ct);

        if (commType is null)
            return Ok(ApiResponse<object>.Ok(new { message = $"No active FSB CommissionType found for window {activeWindow}." }));

        // 4. Idempotency check — if force=true, cancel existing wrong record and recreate
        var existingEarning = await _db.CommissionEarnings
            .FirstOrDefaultAsync(e => e.BeneficiaryMemberId == memberId
                                   && e.CommissionTypeId == commType.Id
                                   && e.Status != CommissionEarningStatus.Cancelled, ct);

        if (existingEarning is not null && !force)
            return Ok(ApiResponse<object>.Ok(new { message = $"FSB Window {activeWindow} already recorded (${existingEarning.Amount}). Use ?force=true to correct it." }));

        if (existingEarning is not null && force)
        {
            existingEarning.Status         = CommissionEarningStatus.Cancelled;
            existingEarning.Notes          = $"[CORRECTED] {existingEarning.Notes}";
            existingEarning.LastUpdateDate = now;
            existingEarning.LastUpdateBy   = User.Identity?.Name ?? "admin-backfill";
        }

        // 5. Get the 2 Elite/Turbo (LevelId 3 or 4) sponsored members in this window
        int[] eligibleLevelIds = [3, 4];

        var eligibleSponsors = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == memberId
                     && m.EnrollDate >= windowStart && m.EnrollDate <= windowEnd)
            .Join(
                _db.MembershipSubscriptions.AsNoTracking()
                    .Where(s => s.SubscriptionStatus == MembershipStatus.Active
                             && eligibleLevelIds.Contains(s.MembershipLevelId)),
                m => m.MemberId,
                s => s.MemberId,
                (m, _) => new { m.MemberId, m.FirstName, m.LastName, m.EnrollDate })
            .OrderBy(x => x.EnrollDate)
            .Take(2)
            .ToListAsync(ct);

        if (eligibleSponsors.Count < 2)
            return Ok(ApiResponse<object>.Ok(new
            {
                message       = $"Not enough Elite/Turbo sponsors in window {activeWindow}. Found: {eligibleSponsors.Count}, required: 2.",
                eligibleCount = eligibleSponsors.Count
            }));

        // 6. Build Notes showing both member names for commission detail display
        var m1 = eligibleSponsors[0];
        var m2 = eligibleSponsors[1];
        var notes = $"{m1.FirstName} {m1.LastName} ({m1.MemberId}) — {m2.FirstName} {m2.LastName} ({m2.MemberId})";

        // Pay half now; the remaining half fires on rebilling
        var amount = (commType.ActiveAmount ?? 0m) / 2m;

        var sourceMemberId = m2.MemberId;
        var sourceOrderId = force
            ? $"BACKFILL-{Guid.NewGuid():N}"
            : await _db.Orders
                .AsNoTracking()
                .Where(o => o.MemberId == sourceMemberId && o.Status == Domain.Entities.Orders.OrderStatus.Completed)
                .OrderByDescending(o => o.CreationDate)
                .Select(o => o.Id)
                .FirstOrDefaultAsync(ct) ?? $"ADMIN-BACKFILL-{memberId}-W{activeWindow}";

        await _db.CommissionEarnings.AddAsync(new CommissionEarning
        {
            BeneficiaryMemberId = memberId,
            SourceMemberId      = sourceMemberId,
            SourceOrderId       = sourceOrderId,
            CommissionTypeId    = commType.Id,
            Amount              = amount,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = now,
            PaymentDate         = now.AddDays(commType.PaymentDelayDays),
            PeriodDate          = now.Date,
            Notes               = notes,
            CreatedBy           = User.Identity?.Name ?? "admin-backfill",
            CreationDate        = now,
            LastUpdateDate      = now
        }, ct);

        // 7. Advance next window dates
        if (activeWindow == 1)
        {
            countdown.FastStartBonus2Start = now;
            countdown.FastStartBonus2End   = now.AddDays(7);
            countdown.LastUpdateDate       = now;
            countdown.LastUpdateBy         = User.Identity?.Name ?? "admin-backfill";
        }
        else if (activeWindow == 2)
        {
            countdown.FastStartBonus3Start = now;
            countdown.FastStartBonus3End   = now.AddDays(7);
            countdown.LastUpdateDate       = now;
            countdown.LastUpdateBy         = User.Identity?.Name ?? "admin-backfill";
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            message      = $"FSB Window {activeWindow} commission recorded successfully.",
            window       = activeWindow,
            amount,
            eligibleCount = eligibleSponsors.Count,
            notes,
            nextW2Start  = activeWindow == 1 ? (DateTime?)countdown.FastStartBonus2Start : null,
            nextW2End    = activeWindow == 1 ? (DateTime?)countdown.FastStartBonus2End   : null,
            nextW3Start  = activeWindow == 2 ? (DateTime?)countdown.FastStartBonus3Start : null,
            nextW3End    = activeWindow == 2 ? (DateTime?)countdown.FastStartBonus3End   : null
        }));
    }

    // ─── Admin-only DTOs (week-selector enrollments view) ─────────────────

    public class BoostWeekOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class BoostEnrollmentResponseDto
    {
        public List<BoostWeekOptionDto>     AvailableWeeks      { get; set; } = new();
        public List<BoostEnrollmentItemDto> GoldEnrollments     { get; set; } = new();
        public List<BoostEnrollmentItemDto> PlatinumEnrollments { get; set; } = new();
    }

    public class BoostEnrollmentItemDto
    {
        public string    MemberId           { get; set; } = string.Empty;
        public string    OrderNo            { get; set; } = string.Empty;
        public string    MemberName         { get; set; } = string.Empty;
        public string    MembershipType     { get; set; } = string.Empty;
        public string    Email              { get; set; } = string.Empty;
        public string    LegName            { get; set; } = string.Empty;
        public DateTime  EnrollmentDate     { get; set; }
        public DateTime? NextRebillingDate  { get; set; }
        public bool      IsRebillingActive  { get; set; }
        public bool      IsRebillingPending { get; set; }
        public int       Points             { get; set; }
    }
}
