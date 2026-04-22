using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Commission data for a specific member — used by Admin Member Profile commissions tab.
/// Routes: /api/v1/admin/members/{memberId}/commissions/*
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/commissions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberCommissionsController : ControllerBase
{
    // Category IDs — mirror BizCenter handler constants
    private const int FastStartBonusCategoryId    = 2;
    private const int DualTeamResidualCategoryId  = 3;
    private const int BoostPresidentialCategoryId = 4;
    private const int CarBonusCategoryId          = 5;

    private readonly AppDbContext _db;

    public AdminMemberCommissionsController(AppDbContext db) => _db = db;

    // ─────────────────────────────────────────────────────────────────────────
    // Summary & Overview
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>GET /summary — pending, paid, and current-year totals for a member.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(string memberId, CancellationToken ct = default)
    {
        var currentYear = DateTime.UtcNow.Year;

        var earnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Select(c => new { c.Amount, c.Status, c.EarnedDate })
            .ToListAsync(ct);

        var dto = new CommissionSummaryDto
        {
            PendingTotal     = earnings.Where(c => c.Status == CommissionEarningStatus.Pending).Sum(c => c.Amount),
            PaidTotal        = earnings.Where(c => c.Status == CommissionEarningStatus.Paid).Sum(c => c.Amount),
            CurrentYearTotal = earnings.Where(c => c.Status == CommissionEarningStatus.Paid && c.EarnedDate.Year == currentYear).Sum(c => c.Amount)
        };

        return Ok(ApiResponse<CommissionSummaryDto>.Ok(dto));
    }

    /// <summary>GET / — paged list with optional status/date filters.</summary>
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
        CommissionEarningStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<CommissionEarningStatus>(status, ignoreCase: true, out var parsed))
        {
            statusFilter = parsed;
        }

        var query = _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Where(c => statusFilter == null || c.Status == statusFilter.Value)
            .Join(
                _db.CommissionTypes,
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { Earning = c, CommType = ct2 })
            .Join(
                _db.CommissionCategories,
                x   => x.CommType.CommissionCategoryId,
                cat => cat.Id,
                (x, cat) => new CommissionEarningItemDto
                {
                    Id                 = x.Earning.Id,
                    CommissionTypeName = x.CommType.Name,
                    CategoryName       = cat.Name,
                    Amount             = x.Earning.Amount,
                    Status             = x.Earning.Status.ToString(),
                    EarnedDate         = x.Earning.EarnedDate,
                    PaymentDate        = x.Earning.PaymentDate
                });

        if (DateTime.TryParse(from, out var fromDate))
            query = query.Where(x => x.EarnedDate >= fromDate);

        if (DateTime.TryParse(to, out var toDate))
            query = query.Where(x => x.EarnedDate <= toDate.AddDays(1));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.EarnedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = new PagedResult<CommissionEarningItemDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };

        return Ok(ApiResponse<PagedResult<CommissionEarningItemDto>>.Ok(result));
    }

    /// <summary>GET /history — year/month grouped totals (paid only).</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(string memberId, CancellationToken ct = default)
    {
        // Load to memory first — GroupBy projection in EF can be tricky across providers
        var raw = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId && c.Status == CommissionEarningStatus.Paid)
            .Select(c => new { c.EarnedDate.Year, c.EarnedDate.Month, c.Amount })
            .ToListAsync(ct);

        var years = raw
            .GroupBy(c => c.Year)
            .OrderByDescending(yg => yg.Key)
            .Select(yg => new CommissionHistoryYearDto
            {
                Year        = yg.Key,
                TotalIncome = yg.Sum(m => m.Amount),
                Months      = yg
                    .GroupBy(m => m.Month)
                    .OrderByDescending(mg => mg.Key)
                    .Select(mg => new CommissionHistoryMonthDto
                    {
                        MonthNo     = mg.Key,
                        MonthName   = new DateTime(yg.Key, mg.Key, 1).ToString("MMMM"),
                        TotalIncome = mg.Sum(x => x.Amount)
                    })
                    .ToList()
            })
            .ToList();

        return Ok(ApiResponse<List<CommissionHistoryYearDto>>.Ok(years));
    }

    /// <summary>GET /breakdown?paymentDate=&amp;earnedDate= — type breakdown. earnedDate required for pending (narrows by batch), omit for paid.</summary>
    [HttpGet("breakdown")]
    public async Task<IActionResult> GetBreakdown(
        string memberId,
        [FromQuery] DateTime  paymentDate,
        [FromQuery] DateTime? earnedDate = null,
        CancellationToken ct = default)
    {
        var targetPaymentDate = paymentDate.Date;

        var baseQuery = _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.PaymentDate.Date == targetPaymentDate
                     && c.Status != CommissionEarningStatus.Cancelled);

        if (earnedDate.HasValue)
            baseQuery = baseQuery.Where(c => c.EarnedDate.Date == earnedDate.Value.Date);

        var raw = await baseQuery
            .Join(
                _db.CommissionTypes,
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new
                {
                    c.SourceMemberId,
                    c.SourceOrderId,
                    c.Notes,
                    c.Amount,
                    TypeName = ct2.Name,
                    TypeDesc = ct2.Description
                })
            .OrderBy(d => d.TypeName)
            .ToListAsync(ct);

        var sourceOrderIds = raw
            .Where(x => x.SourceOrderId != null)
            .Select(x => x.SourceOrderId!)
            .Distinct()
            .ToList();

        var orderNumbers = sourceOrderIds.Count > 0
            ? await _db.Orders
                .AsNoTracking()
                .Where(o => sourceOrderIds.Contains(o.Id))
                .Select(o => new { o.Id, Display = o.OrderNo ?? o.Id })
                .ToDictionaryAsync(o => o.Id, o => o.Display, ct)
            : new Dictionary<string, string>();

        var sourceIds = raw
            .Where(x => x.SourceMemberId != null)
            .Select(x => x.SourceMemberId!)
            .Distinct()
            .ToList();

        var memberNames = sourceIds.Count > 0
            ? await _db.MemberProfiles
                .AsNoTracking()
                .Where(mp => sourceIds.Contains(mp.MemberId))
                .Select(mp => new { mp.MemberId, FullName = mp.FirstName + " " + mp.LastName })
                .ToDictionaryAsync(mp => mp.MemberId, mp => mp.FullName, ct)
            : new Dictionary<string, string>();

        var items = raw.Select(x =>
        {
            string detail;
            if (!string.IsNullOrWhiteSpace(x.Notes))
            {
                detail = x.Notes;
            }
            else if (x.SourceOrderId != null)
            {
                var orderRef = orderNumbers.TryGetValue(x.SourceOrderId, out var no) ? no : x.SourceOrderId;
                var name = x.SourceMemberId != null && memberNames.TryGetValue(x.SourceMemberId, out var fullName)
                    ? $"{fullName} ({x.SourceMemberId})"
                    : x.SourceMemberId ?? string.Empty;
                detail = $"Order #{orderRef} — {name}";
            }
            else
            {
                detail = x.TypeDesc ?? string.Empty;
            }
            return new CommissionBreakdownItemDto
            {
                CommissionTypeName = x.TypeName,
                Detail             = detail,
                Amount             = x.Amount
            };
        }).ToList();

        return Ok(ApiResponse<List<CommissionBreakdownItemDto>>.Ok(items));
    }

    /// <summary>GET /month-breakdown?year=&amp;month= — monthly detail per commission type.</summary>
    [HttpGet("month-breakdown")]
    public async Task<IActionResult> GetMonthBreakdown(
        string memberId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        var raw = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.EarnedDate.Year  == year
                     && c.EarnedDate.Month == month)
            .Join(
                _db.CommissionTypes,
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new
                {
                    TypeName    = ct2.Name,
                    Description = ct2.Description ?? string.Empty,
                    c.EarnedDate,
                    c.PaymentDate,
                    c.Amount,
                    Status      = c.Status.ToString()
                })
            .OrderBy(x => x.TypeName)
            .ThenByDescending(x => x.EarnedDate)
            .ToListAsync(ct);

        // Group in memory — avoids EF GroupBy projection limitations
        var grouped = raw
            .GroupBy(x => x.TypeName)
            .Select(g => new CommissionMonthGroupDto
            {
                CommissionTypeName = g.Key,
                Items = g.Select(i => new CommissionMonthRowDto
                {
                    EarnedDate  = i.EarnedDate,
                    PaymentDate = i.PaymentDate,
                    Detail      = i.Description,
                    Amount      = i.Amount,
                    Status      = i.Status
                }).ToList()
            })
            .ToList();

        return Ok(ApiResponse<List<CommissionMonthGroupDto>>.Ok(grouped));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Dual Residual (already existed — kept for backward compat)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("dual-residual")]
    public async Task<IActionResult> GetDualResidual(
        string memberId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 10,
        [FromQuery] string? search   = null,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        CancellationToken ct = default)
    {
        var query = _db.CommissionEarnings.AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == DualTeamResidualCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { Earning = c, CommType = ct2 });

        if (DateTime.TryParse(from, out var fromDate))
            query = query.Where(x => x.Earning.EarnedDate >= fromDate);

        if (DateTime.TryParse(to, out var toDate))
            query = query.Where(x => x.Earning.EarnedDate <= toDate.AddDays(1));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.CommType.Name.Contains(search));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.Earning.EarnedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new DualResidualDto
            {
                EarnedDate     = x.Earning.EarnedDate,
                Amount         = x.Earning.Amount,
                Status         = x.Earning.Status.ToString(),
                EligiblePoints = 0
            })
            .ToListAsync(ct);

        var result = new PagedResult<DualResidualDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };

        return Ok(ApiResponse<PagedResult<DualResidualDto>>.Ok(result));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fast Start Bonus
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>GET /fast-start-bonus/summary</summary>
    [HttpGet("fast-start-bonus/summary")]
    public async Task<IActionResult> GetFastStartBonusSummary(string memberId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // 1. Eligible sponsored members — only active Elite/Turbo memberships count
        var eligibleMemberIds = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => s.SubscriptionStatus == MembershipStatus.Active)
            .Join(
                _db.MembershipLevels.Where(l => l.Name.Contains("Elite") || l.Name.Contains("Turbo")),
                s => s.MembershipLevelId,
                l => l.Id,
                (s, _) => s.MemberId)
            .ToHashSetAsync(ct);

        var sponsoredEnrollments = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == memberId && eligibleMemberIds.Contains(m.MemberId))
            .Select(m => m.EnrollDate)
            .ToListAsync(ct);

        // 2. FSB earnings — carry EarnedDate for dynamic window recalculation
        var fsbEarnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId && c.Status != CommissionEarningStatus.Cancelled)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == FastStartBonusCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { ct2.TriggerOrder, c.EarnedDate, c.Amount })
            .ToListAsync(ct);

        var earnByOrder = fsbEarnings
            .GroupBy(x => x.TriggerOrder)
            .ToDictionary(g => g.Key, g => (
                EarnedDate: g.Min(x => x.EarnedDate),
                Amount:     g.Sum(x => x.Amount)));

        // 3. Countdown record
        var memberUserId = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => m.UserId)
            .FirstOrDefaultAsync(ct);

        var countdown = memberUserId != default
            ? await _db.CommissionCountDowns
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MemberId == memberUserId, ct)
            : null;

        if (countdown is null)
            return Ok(ApiResponse<FsbSummaryDto>.Ok(new FsbSummaryDto
            {
                Count       = fsbEarnings.Count,
                TotalAmount = fsbEarnings.Sum(x => x.Amount)
            }));

        // 4. Determine FSB1 normal vs extended earned
        var w1NormalEnd = countdown.FastStartBonus1End;
        var w1ExtEnd    = countdown.FastStartBonus1ExtendedEnd;

        earnByOrder.TryGetValue(1, out var fsb1Earn);
        var fsb1NormalEarned   = fsb1Earn != default && fsb1Earn.EarnedDate <= w1NormalEnd;
        var fsb1ExtendedEarned = fsb1Earn != default && fsb1Earn.EarnedDate > w1NormalEnd && fsb1Earn.EarnedDate <= w1ExtEnd;

        earnByOrder.TryGetValue(2, out var fsb2Earn);
        earnByOrder.TryGetValue(3, out var fsb3Earn);

        // 5. Dynamic W2/W3 dates — start the moment the previous FSB was earned
        var w2Start = fsb1NormalEarned ? fsb1Earn.EarnedDate         : countdown.FastStartBonus2Start;
        var w2End   = fsb1NormalEarned ? fsb1Earn.EarnedDate.AddDays(7) : countdown.FastStartBonus2End;
        var w3Start = fsb2Earn != default ? fsb2Earn.EarnedDate         : countdown.FastStartBonus3Start;
        var w3End   = fsb2Earn != default ? fsb2Earn.EarnedDate.AddDays(7) : countdown.FastStartBonus3End;

        // 6. Mode flags
        var isExtendedMode     = now > w1NormalEnd && !fsb1NormalEarned;
        var isDisqualifiedW2W3 = fsb1ExtendedEarned;

        // 7. Build windows
        FsbWindowDto Build(int num, bool isPromo, DateTime start, DateTime end, decimal amount, bool hidden)
        {
            var isCompleted    = amount > 0; // earned = completed regardless of whether window has closed
            var isExpired      = !isCompleted && now > end;
            var isActive       = !isCompleted && !isExpired && now >= start;
            var sponsoredCount = sponsoredEnrollments.Count(d => d >= start && d <= end);
            return new FsbWindowDto
            {
                WindowNumber   = num,
                IsPromo        = isPromo,
                Amount         = amount,
                IsCompleted    = isCompleted,
                IsActive       = isActive,
                StartDate      = start,
                EndDate        = end,
                SponsoredCount = sponsoredCount,
                IsHidden       = hidden
            };
        }

        var windows = new List<FsbWindowDto>
        {
            Build(1, false, countdown.FastStartBonus1Start, w1NormalEnd,
                fsb1NormalEarned ? fsb1Earn.Amount : 0m, hidden: false),

            Build(2, false, w2Start, w2End,
                fsb2Earn != default ? fsb2Earn.Amount : 0m,
                hidden: isExtendedMode || isDisqualifiedW2W3),

            Build(3, false, w3Start, w3End,
                fsb3Earn != default ? fsb3Earn.Amount : 0m,
                hidden: isExtendedMode || isDisqualifiedW2W3),

            Build(1, true, countdown.FastStartBonus1ExtendedStart, w1ExtEnd,
                fsb1ExtendedEarned ? fsb1Earn.Amount : 0m, hidden: false),
        };

        return Ok(ApiResponse<FsbSummaryDto>.Ok(new FsbSummaryDto
        {
            Count              = fsbEarnings.Count,
            TotalAmount        = fsbEarnings.Sum(x => x.Amount),
            Windows            = windows,
            IsExtendedMode     = isExtendedMode,
            IsDisqualifiedW2W3 = isDisqualifiedW2W3
        }));
    }

    /// <summary>GET /fast-start-bonus — paged earnings.</summary>
    [HttpGet("fast-start-bonus")]
    public async Task<IActionResult> GetFastStartBonus(
        string memberId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        CancellationToken ct = default)
    {
        var query = _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == FastStartBonusCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new EarningItemDto
                {
                    CommissionTypeName = ct2.Name,
                    Description        = c.Notes,
                    Amount             = c.Amount,
                    Status             = c.Status.ToString(),
                    EarnedDate         = c.EarnedDate,
                    PaymentDate        = c.PaymentDate
                });

        if (DateTime.TryParse(from, out var fromDate))
            query = query.Where(x => x.EarnedDate >= fromDate);

        if (DateTime.TryParse(to, out var toDate))
            query = query.Where(x => x.EarnedDate <= toDate.AddDays(1));

        var totalCount = await query.CountAsync(ct);
        var items      = await query.OrderByDescending(c => c.EarnedDate)
                                    .Skip((page - 1) * pageSize).Take(pageSize)
                                    .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<EarningItemDto>>.Ok(new PagedResult<EarningItemDto>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
        }));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Presidential Bonus
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>GET /presidential-bonus/summary</summary>
    [HttpGet("presidential-bonus/summary")]
    public async Task<IActionResult> GetPresidentialBonusSummary(string memberId, CancellationToken ct = default)
    {
        var summary = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == BoostPresidentialCategoryId
                                            && t.Name.Contains("Presidential")),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, _) => c.Amount)
            .GroupBy(_ => 1)
            .Select(g => new BonusSummaryDto { Count = g.Count(), TotalAmount = g.Sum() })
            .FirstOrDefaultAsync(ct);

        return Ok(ApiResponse<BonusSummaryDto>.Ok(summary ?? new BonusSummaryDto()));
    }

    /// <summary>GET /presidential-bonus — paged earnings.</summary>
    [HttpGet("presidential-bonus")]
    public async Task<IActionResult> GetPresidentialBonus(
        string memberId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        CancellationToken ct = default)
    {
        var query = _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == BoostPresidentialCategoryId
                                            && t.Name.Contains("Presidential")),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new EarningItemDto
                {
                    CommissionTypeName = ct2.Name,
                    Amount             = c.Amount,
                    Status             = c.Status.ToString(),
                    EarnedDate         = c.EarnedDate,
                    PaymentDate        = c.PaymentDate
                });

        if (DateTime.TryParse(from, out var fromDate))
            query = query.Where(x => x.EarnedDate >= fromDate);

        if (DateTime.TryParse(to, out var toDate))
            query = query.Where(x => x.EarnedDate <= toDate.AddDays(1));

        var totalCount = await query.CountAsync(ct);
        var items      = await query.OrderByDescending(c => c.EarnedDate)
                                    .Skip((page - 1) * pageSize).Take(pageSize)
                                    .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<EarningItemDto>>.Ok(new PagedResult<EarningItemDto>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
        }));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Boost Bonus
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>GET /boost-bonus/week-stats — Gold/Platinum enrollment progress for current ISO week.</summary>
    [HttpGet("boost-bonus/week-stats")]
    public async Task<IActionResult> GetBoostBonusWeekStats(string memberId, CancellationToken ct = default)
    {
        var today     = DateTime.UtcNow;
        var daysToMon = (int)today.DayOfWeek == 0 ? -6 : -((int)today.DayOfWeek - 1);
        var weekStart = today.AddDays(daysToMon).Date;
        var weekEnd   = weekStart.AddDays(7);

        // Load boost types to get correct NewMembers thresholds (Gold=6, Platinum=12 per seed)
        var boostTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && !t.IsPaidOnSignup && !t.ResidualBased
                     && !t.IsSponsorBonus && t.TriggerOrder > 0)
            .OrderBy(t => t.LifeTimeRank)
            .ToListAsync(ct);

        var goldType     = boostTypes.FirstOrDefault(t => t.Name.Contains("Gold",     StringComparison.OrdinalIgnoreCase));
        var platinumType = boostTypes.FirstOrDefault(t => t.Name.Contains("Platinum", StringComparison.OrdinalIgnoreCase));

        int goldTarget     = goldType?.NewMembers     ?? 6;
        int platinumTarget = platinumType?.NewMembers ?? 12;

        // New Elite/Turbo Active enrollments in member's downline this week (raw, per leg).
        // Uses MembershipSubscription (set in Phase 1) and filters on Active status (Phase 3 done)
        // so the progress bar reflects signups that actually count toward the bonus threshold.
        int[] eligibleLevelIds = [3, 4];
        var hierarchyFilter    = $"/{memberId}/";

        var downlineEnrollments = await (
            from mp  in _db.MemberProfiles.AsNoTracking()
            join g   in _db.GenealogyTree.AsNoTracking()           on mp.MemberId           equals g.MemberId
            join sub in _db.MembershipSubscriptions.AsNoTracking() on mp.MemberId           equals sub.MemberId
            where mp.EnrollDate >= weekStart
               && mp.EnrollDate <  weekEnd
               && sub.SubscriptionStatus == MembershipStatus.Active
               && eligibleLevelIds.Contains(sub.MembershipLevelId)
               && g.HierarchyPath.Contains(hierarchyFilter)
            select new { mp.MemberId, g.HierarchyPath }
        ).Distinct().ToListAsync(ct);

        // Determine leg (direct child of member) for each enrollment, then apply 50% cap
        static string GetLeg(string path, string filter)
        {
            var idx = path.IndexOf(filter, StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            var after = path[(idx + filter.Length)..];
            var slash = after.IndexOf('/');
            return slash > 0 ? after[..slash] : after.TrimEnd('/');
        }

        int CappedCount(int threshold)
        {
            if (threshold == 0) return downlineEnrollments.Count;
            var capPerLeg = (int)Math.Floor(threshold * 0.5);
            return downlineEnrollments
                .Select(e => GetLeg(e.HierarchyPath, hierarchyFilter))
                .GroupBy(leg => leg)
                .Sum(g => Math.Min(g.Count(), capPerLeg));
        }

        // Resolve qualification points per enrolled member from completed orders
        var enrolledIds = downlineEnrollments.Select(e => e.MemberId).Distinct().ToList();
        var memberPointsData = enrolledIds.Count > 0
            ? await (
                from o  in _db.Orders.AsNoTracking()
                join od in _db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
                join p  in _db.Products.AsNoTracking()     on od.ProductId equals p.Id
                where enrolledIds.Contains(o.MemberId)
                   && o.Status == Domain.Entities.Orders.OrderStatus.Completed
                select new { o.MemberId, Points = od.Quantity * p.QualificationPoins }
              ).ToListAsync(ct)
            : new();

        var pointsPerMember = memberPointsData
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Points));

        var minProductPoints = enrolledIds.Count > 0
            ? await _db.Products.AsNoTracking()
                .Where(p => p.IsActive && !p.IsDeleted && p.QualificationPoins >= 6)
                .MinAsync(p => (int?)p.QualificationPoins, ct) ?? 6
            : 6;

        int CappedPoints(int threshold)
        {
            if (threshold == 0)
                return downlineEnrollments.DistinctBy(e => e.MemberId)
                    .Sum(e => pointsPerMember.TryGetValue(e.MemberId, out var p) ? p : 0);
            var capPerLeg = (int)Math.Floor(threshold * 0.5);
            return downlineEnrollments
                .GroupBy(e => GetLeg(e.HierarchyPath, hierarchyFilter))
                .Sum(g => g.DistinctBy(e => e.MemberId)
                           .Take(capPerLeg)
                           .Sum(e => pointsPerMember.TryGetValue(e.MemberId, out var p) ? p : 0));
        }

        // Earned amounts this week (for the amount totals)
        var amounts = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.EarnedDate >= weekStart
                     && c.EarnedDate <  weekEnd)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == BoostPresidentialCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { ct2.Name, c.Amount })
            .ToListAsync(ct);

        var dto = new BoostWeekStatsDto
        {
            WeekLabel          = $"Week of {weekStart:MMM d} - {weekStart.AddDays(6):MMM d}",
            GoldCount          = CappedCount(goldTarget),
            PlatinumCount      = CappedCount(platinumTarget),
            GoldTarget         = goldTarget,
            PlatinumTarget     = platinumTarget,
            GoldPoints         = CappedPoints(goldTarget),
            GoldPointsTarget   = goldTarget * minProductPoints,
            PlatinumPoints     = CappedPoints(platinumTarget),
            PlatinumPointsTarget = platinumTarget * minProductPoints,
            GoldAmount         = amounts.Where(x => x.Name.Contains("Gold",     StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount),
            PlatinumAmount     = amounts.Where(x => x.Name.Contains("Platinum", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount)
        };

        return Ok(ApiResponse<BoostWeekStatsDto>.Ok(dto));
    }

    /// <summary>GET /boost-bonus/enrollments — new Elite/Turbo members in downline for a given ISO week.</summary>
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

        int[] eligibleLevelIds = [3, 4];    // Elite=3, Turbo=4
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

        // Deduplicate per member: prefer Active subscription, then most recent enrollment date.
        var deduped = rawEnrollments
            .GroupBy(m => m.MemberId)
            .Select(g => g.OrderByDescending(m => m.SubscriptionStatus == MembershipStatus.Active ? 1 : 0)
                          .ThenByDescending(m => m.EnrollDate)
                          .First())
            .OrderBy(m => m.EnrollDate)
            .ToList();

        // Resolve OrderNo and qualification points per enrolled member.
        var enrolledMemberIds = deduped.Select(m => m.MemberId).ToList();

        // OrderNo: any completed order — does NOT require products to be present
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

        // Points: requires order details + products join
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
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Points));

        // Derive leg MemberId: first path segment after /{memberId}/ in hierarchy path.
        static string GetLegMemberId(string path, string filter)
        {
            var idx = path.IndexOf(filter, StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            var after = path[(idx + filter.Length)..];
            var slash = after.IndexOf('/');
            return slash > 0 ? after[..slash] : after.TrimEnd('/');
        }

        // Resolve leg MemberIds → full names.
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

    /// <summary>GET /boost-bonus/summary — total members in downline (count).</summary>
    [HttpGet("boost-bonus/summary")]
    public async Task<IActionResult> GetBoostBonusSummary(string memberId, CancellationToken ct = default)
    {
        var downlineCount = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains($"/{memberId}/") && g.MemberId != memberId)
            .CountAsync(ct);

        var dto = new BoostMemberSummaryDto
        {
            TotalMembers      = downlineCount,
            ActiveRebilling   = 0,
            InactiveRebilling = 0
        };

        return Ok(ApiResponse<BoostMemberSummaryDto>.Ok(dto));
    }

    /// <summary>GET /boost-bonus — paged earnings.</summary>
    [HttpGet("boost-bonus")]
    public async Task<IActionResult> GetBoostBonus(
        string memberId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        CancellationToken ct = default)
    {
        var query = _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == BoostPresidentialCategoryId
                                            && t.Name.Contains("Boost")),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new EarningItemDto
                {
                    CommissionTypeName = ct2.Name,
                    Amount             = c.Amount,
                    Status             = c.Status.ToString(),
                    EarnedDate         = c.EarnedDate,
                    PaymentDate        = c.PaymentDate
                });

        if (DateTime.TryParse(from, out var fromDate))
            query = query.Where(x => x.EarnedDate >= fromDate);

        if (DateTime.TryParse(to, out var toDate))
            query = query.Where(x => x.EarnedDate <= toDate.AddDays(1));

        var totalCount = await query.CountAsync(ct);
        var items      = await query.OrderByDescending(c => c.EarnedDate)
                                    .Skip((page - 1) * pageSize).Take(pageSize)
                                    .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<EarningItemDto>>.Ok(new PagedResult<EarningItemDto>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
        }));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Car Bonus
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>GET /car-bonus — paged earnings.</summary>
    [HttpGet("car-bonus")]
    public async Task<IActionResult> GetCarBonus(
        string memberId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        CancellationToken ct = default)
    {
        var query = _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == CarBonusCategoryId
                                            && t.Name.Contains("Car")),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new EarningItemDto
                {
                    CommissionTypeName = ct2.Name,
                    Amount             = c.Amount,
                    Status             = c.Status.ToString(),
                    EarnedDate         = c.EarnedDate,
                    PaymentDate        = c.PaymentDate
                });

        if (DateTime.TryParse(from, out var fromDate))
            query = query.Where(x => x.EarnedDate >= fromDate);

        if (DateTime.TryParse(to, out var toDate))
            query = query.Where(x => x.EarnedDate <= toDate.AddDays(1));

        var totalCount = await query.CountAsync(ct);
        var items      = await query.OrderByDescending(c => c.EarnedDate)
                                    .Skip((page - 1) * pageSize).Take(pageSize)
                                    .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<EarningItemDto>>.Ok(new PagedResult<EarningItemDto>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
        }));
    }

    /// <summary>GET /car-bonus/stats — current-month progress for a member.</summary>
    [HttpGet("car-bonus/stats")]
    public async Task<IActionResult> GetCarBonusStats(string memberId, CancellationToken ct = default)
    {
        var today  = DateTime.UtcNow;
        var label  = today.ToString("MMMM");

        var carBonusType = await _db.CommissionTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name.Contains("Car") && t.IsActive, ct);

        var teamPointsTarget = carBonusType?.TeamPoints ?? 1000;
        var personalTarget   = carBonusType?.PersonalPoints ?? 6;

        var hierarchyFilter = $"/{memberId}/";

        var downlineIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(hierarchyFilter) && g.MemberId != memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        var activeSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => downlineIds.Contains(s.MemberId)
                     && s.SubscriptionStatus == MembershipStatus.Active
                     && !s.IsDeleted)
            .Select(s => new { s.MemberId, s.MembershipLevelId, s.CreationDate })
            .ToListAsync(ct);

        var levelPoints = new Dictionary<int, int> { [2] = 1, [3] = 6, [4] = 6 };

        var subsByMember = activeSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreationDate).First().MembershipLevelId);

        var totalPoints   = subsByMember.Values.Sum(lid => levelPoints.TryGetValue(lid, out var p) ? p : 0);
        var eligiblePoints = Math.Min(totalPoints, (int)teamPointsTarget);
        var pct           = teamPointsTarget > 0 ? (int)Math.Min(100, Math.Round(totalPoints / (double)teamPointsTarget * 100)) : 0;

        return Ok(ApiResponse<CarBonusStatsAdminDto>.Ok(new CarBonusStatsAdminDto
        {
            TotalPoints      = totalPoints,
            EligiblePoints   = eligiblePoints,
            ProgressPercent  = pct,
            TeamPointsTarget = (int)teamPointsTarget,
            MonthLabel       = label
        }));
    }

    /// <summary>GET /car-bonus/ambassadors — downline ambassador breakdown for a member.</summary>
    [HttpGet("car-bonus/ambassadors")]
    public async Task<IActionResult> GetCarBonusAmbassadors(string memberId, CancellationToken ct = default)
    {
        var hierarchyFilter = $"/{memberId}/";
        var levelPoints     = new Dictionary<int, int> { [2] = 1, [3] = 6, [4] = 6 };

        var downlineIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(hierarchyFilter) && g.MemberId != memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (downlineIds.Count == 0)
            return Ok(ApiResponse<List<CarBonusAmbassadorAdminDto>>.Ok(new List<CarBonusAmbassadorAdminDto>()));

        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(mp => downlineIds.Contains(mp.MemberId))
            .Select(mp => new { mp.MemberId, mp.FirstName, mp.LastName })
            .ToListAsync(ct);

        var activeSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => downlineIds.Contains(s.MemberId)
                     && s.SubscriptionStatus == MembershipStatus.Active
                     && !s.IsDeleted)
            .Select(s => new { s.MemberId, s.MembershipLevelId, s.CreationDate })
            .ToListAsync(ct);

        var subsByMember = activeSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreationDate).First().MembershipLevelId);

        var result = profiles
            .Select(mp =>
            {
                var levelId = subsByMember.TryGetValue(mp.MemberId, out var lid) ? lid : 0;
                var pts     = levelPoints.TryGetValue(levelId, out var p) ? p : 0;
                return new CarBonusAmbassadorAdminDto
                {
                    MemberId       = mp.MemberId,
                    AmbassadorName = $"{mp.FirstName} {mp.LastName}".Trim(),
                    TotalPoints    = pts,
                    EligiblePoints = pts
                };
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenBy(x => x.AmbassadorName)
            .ToList();

        return Ok(ApiResponse<List<CarBonusAmbassadorAdminDto>>.Ok(result));
    }

    /// <summary>GET /car-bonus/ambassadors/{branchMemberId}/branch — branch member breakdown.</summary>
    [HttpGet("car-bonus/ambassadors/{branchMemberId}/branch")]
    public async Task<IActionResult> GetCarBonusBranch(
        string branchMemberId,
        CancellationToken ct = default)
    {
        var levelPoints     = new Dictionary<int, int> { [2] = 1, [3] = 6, [4] = 6 };
        var hierarchyFilter = $"/{branchMemberId}/";

        // Include the branch ambassador themselves + all their downline
        var memberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.MemberId == branchMemberId
                     || g.HierarchyPath.Contains(hierarchyFilter))
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (memberIds.Count == 0)
            return Ok(ApiResponse<CarBonusBranchAdminDto>.Ok(new CarBonusBranchAdminDto()));

        var activeSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => memberIds.Contains(s.MemberId)
                     && s.SubscriptionStatus == MembershipStatus.Active
                     && !s.IsDeleted)
            .Select(s => new { s.MemberId, s.MembershipLevelId, s.EndDate, s.LastOrderId, s.CreationDate })
            .ToListAsync(ct);

        var subsByMember = activeSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreationDate).First());

        var levelIds = subsByMember.Values.Select(s => s.MembershipLevelId).Distinct().ToList();
        var levels   = await _db.MembershipLevels
            .AsNoTracking()
            .Where(l => levelIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Name })
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        var orderIds = subsByMember.Values
            .Where(s => s.LastOrderId != null)
            .Select(s => s.LastOrderId!)
            .Distinct()
            .ToList();

        var orderNos = await _db.Orders
            .AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.OrderNo })
            .ToDictionaryAsync(o => o.Id, o => o.OrderNo ?? string.Empty, ct);

        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(mp => memberIds.Contains(mp.MemberId))
            .Select(mp => new { mp.MemberId, mp.FirstName, mp.LastName })
            .ToListAsync(ct);

        var result = profiles
            .Select(mp =>
            {
                var sub       = subsByMember.TryGetValue(mp.MemberId, out var s) ? s : null;
                var levelId   = sub?.MembershipLevelId ?? 0;
                var pts       = levelPoints.TryGetValue(levelId, out var p) ? p : 0;
                var levelName = levelId > 0 && levels.TryGetValue(levelId, out var ln) ? ln : "—";
                var orderNo   = sub?.LastOrderId != null && orderNos.TryGetValue(sub.LastOrderId, out var on) ? on : "—";
                return new CarBonusBranchMemberAdminDto
                {
                    OrderNo         = orderNo,
                    FullName        = $"{mp.FirstName} {mp.LastName}".Trim(),
                    MembershipLevel = levelName,
                    ExpirationDate  = sub?.EndDate,
                    Points          = pts
                };
            })
            .OrderByDescending(x => x.Points)
            .ThenBy(x => x.FullName)
            .ToList();

        return Ok(ApiResponse<CarBonusBranchAdminDto>.Ok(new CarBonusBranchAdminDto { Members = result }));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fast Start Bonus — Admin Backfill
    // ─────────────────────────────────────────────────────────────────────────

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
        // W1 and W2 can overlap in time, so we cannot rely on an if/else date chain.
        // ?window=N overrides auto-detection (admin bypass for testing).
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
            existingEarning.Status        = CommissionEarningStatus.Cancelled;
            existingEarning.Notes         = $"[CORRECTED] {existingEarning.Notes}";
            existingEarning.LastUpdateDate = now;
            existingEarning.LastUpdateBy  = User.Identity?.Name ?? "admin-backfill";
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
                message        = $"Not enough Elite/Turbo sponsors in window {activeWindow}. Found: {eligibleSponsors.Count}, required: 2.",
                eligibleCount  = eligibleSponsors.Count
            }));

        // 6. Build Notes showing both member names for commission detail display
        var m1 = eligibleSponsors[0];
        var m2 = eligibleSponsors[1];
        var notes = $"{m1.FirstName} {m1.LastName} ({m1.MemberId}) — {m2.FirstName} {m2.LastName} ({m2.MemberId})";

        // Pay half now; the remaining half fires on rebilling
        var amount = (commType.ActiveAmount ?? 0m) / 2m;

        var sourceMemberId = m2.MemberId;
        // When force=true we may be re-inserting after cancelling the original — use a unique synthetic ID
        // to avoid the (SourceOrderId, CommissionTypeId) unique index violation.
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

    // ─────────────────────────────────────────────────────────────────────────
    // Nested DTOs
    // ─────────────────────────────────────────────────────────────────────────

    public class CommissionSummaryDto
    {
        public decimal PendingTotal     { get; set; }
        public decimal PaidTotal        { get; set; }
        public decimal CurrentYearTotal { get; set; }
    }

    public class CommissionEarningItemDto
    {
        public string   Id                 { get; set; } = string.Empty;
        public string   CommissionTypeName { get; set; } = string.Empty;
        public string   CategoryName       { get; set; } = string.Empty;
        public decimal  Amount             { get; set; }
        public string   Status             { get; set; } = string.Empty;
        public DateTime EarnedDate         { get; set; }
        public DateTime PaymentDate        { get; set; }
    }

    public class CommissionHistoryYearDto
    {
        public int                            Year        { get; set; }
        public decimal                        TotalIncome { get; set; }
        public List<CommissionHistoryMonthDto> Months     { get; set; } = new();
    }

    public class CommissionHistoryMonthDto
    {
        public int     MonthNo     { get; set; }
        public string  MonthName   { get; set; } = string.Empty;
        public decimal TotalIncome { get; set; }
    }

    public class CommissionBreakdownItemDto
    {
        public string  CommissionTypeName { get; set; } = string.Empty;
        public string  Detail             { get; set; } = string.Empty;
        public decimal Amount             { get; set; }
    }

    public class CommissionMonthGroupDto
    {
        public string                     CommissionTypeName { get; set; } = string.Empty;
        public List<CommissionMonthRowDto> Items             { get; set; } = new();
    }

    public class CommissionMonthRowDto
    {
        public DateTime EarnedDate  { get; set; }
        public DateTime PaymentDate { get; set; }
        public string   Detail      { get; set; } = string.Empty;
        public decimal  Amount      { get; set; }
        public string   Status      { get; set; } = string.Empty;
    }

    public class BonusSummaryDto
    {
        public int     Count       { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class FsbSummaryDto
    {
        public int                 Count              { get; set; }
        public decimal             TotalAmount        { get; set; }
        public List<FsbWindowDto>? Windows            { get; set; }
        public bool                IsExtendedMode     { get; set; }
        public bool                IsDisqualifiedW2W3 { get; set; }
    }

    public class FsbWindowDto
    {
        public int       WindowNumber   { get; set; }
        public bool      IsPromo        { get; set; }
        public decimal   Amount         { get; set; }
        public bool      IsCompleted    { get; set; }
        public bool      IsActive       { get; set; }
        public DateTime? StartDate      { get; set; }
        public DateTime? EndDate        { get; set; }
        public int       SponsoredCount { get; set; }
        public bool      IsHidden       { get; set; }
    }

    public class EarningItemDto
    {
        public string   CommissionTypeName { get; set; } = string.Empty;
        public string?  Description        { get; set; }
        public decimal  Amount             { get; set; }
        public string   Status             { get; set; } = string.Empty;
        public DateTime EarnedDate         { get; set; }
        public DateTime PaymentDate        { get; set; }
    }

    public class BoostWeekStatsDto
    {
        public string  WeekLabel            { get; set; } = string.Empty;
        public int     GoldCount            { get; set; }
        public int     GoldTarget           { get; set; }
        public int     PlatinumCount        { get; set; }
        public int     PlatinumTarget       { get; set; }
        public int     GoldPoints           { get; set; }
        public int     GoldPointsTarget     { get; set; }
        public int     PlatinumPoints       { get; set; }
        public int     PlatinumPointsTarget { get; set; }
        public decimal GoldAmount           { get; set; }
        public decimal PlatinumAmount       { get; set; }
    }

    public class BoostMemberSummaryDto
    {
        public int TotalMembers      { get; set; }
        public int ActiveRebilling   { get; set; }
        public int InactiveRebilling { get; set; }
    }

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

    public class DualResidualDto
    {
        public DateTime EarnedDate     { get; set; }
        public decimal  Amount         { get; set; }
        public string   Status         { get; set; } = string.Empty;
        public int      EligiblePoints { get; set; }
    }

    public class CarBonusStatsAdminDto
    {
        public int    TotalPoints      { get; set; }
        public int    EligiblePoints   { get; set; }
        public int    ProgressPercent  { get; set; }
        public int    TeamPointsTarget { get; set; }
        public string MonthLabel       { get; set; } = string.Empty;
    }

    public class CarBonusAmbassadorAdminDto
    {
        public string MemberId       { get; set; } = string.Empty;
        public string AmbassadorName { get; set; } = string.Empty;
        public int    TotalPoints    { get; set; }
        public int    EligiblePoints { get; set; }
    }

    public class CarBonusBranchAdminDto
    {
        public List<CarBonusBranchMemberAdminDto> Members { get; set; } = new();
    }

    public class CarBonusBranchMemberAdminDto
    {
        public string    OrderNo         { get; set; } = string.Empty;
        public string    FullName        { get; set; } = string.Empty;
        public string    MembershipLevel { get; set; } = string.Empty;
        public DateTime? ExpirationDate  { get; set; }
        public int       Points          { get; set; }
    }
}
