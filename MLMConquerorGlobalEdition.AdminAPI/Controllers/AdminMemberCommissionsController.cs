using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                     && c.PaymentDate.Date == targetPaymentDate);

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
            if (x.SourceOrderId != null)
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
        var summary = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == FastStartBonusCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, _) => c.Amount)
            .GroupBy(_ => 1)
            .Select(g => new BonusSummaryDto { Count = g.Count(), TotalAmount = g.Sum() })
            .FirstOrDefaultAsync(ct);

        return Ok(ApiResponse<BonusSummaryDto>.Ok(summary ?? new BonusSummaryDto()));
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

    /// <summary>GET /boost-bonus/week-stats — Gold/Platinum counts for current ISO week.</summary>
    [HttpGet("boost-bonus/week-stats")]
    public async Task<IActionResult> GetBoostBonusWeekStats(string memberId, CancellationToken ct = default)
    {
        var today    = DateTime.UtcNow;
        var dayOfWeek = (int)today.DayOfWeek;
        var daysToMon = dayOfWeek == 0 ? -6 : -(dayOfWeek - 1);
        var weekStart = today.AddDays(daysToMon).Date;
        var weekEnd   = weekStart.AddDays(6).Date;

        var names = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.EarnedDate >= weekStart
                     && c.EarnedDate <= weekEnd.AddDays(1))
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == BoostPresidentialCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (_, ct2) => ct2.Name)
            .ToListAsync(ct);

        // Also pull amounts for Gold/Platinum totals
        var amounts = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.EarnedDate >= weekStart
                     && c.EarnedDate <= weekEnd.AddDays(1))
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == BoostPresidentialCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { ct2.Name, c.Amount })
            .ToListAsync(ct);

        var dto = new BoostWeekStatsDto
        {
            WeekLabel      = $"Week of {weekStart:MMM d} - {weekEnd:MMM d}",
            GoldCount      = names.Count(n => n.Contains("Gold",     StringComparison.OrdinalIgnoreCase)),
            PlatinumCount  = names.Count(n => n.Contains("Platinum", StringComparison.OrdinalIgnoreCase)),
            GoldTarget     = 36,
            PlatinumTarget = 72,
            GoldAmount     = amounts.Where(x => x.Name.Contains("Gold",     StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount),
            PlatinumAmount = amounts.Where(x => x.Name.Contains("Platinum", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount)
        };

        return Ok(ApiResponse<BoostWeekStatsDto>.Ok(dto));
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

    public class EarningItemDto
    {
        public string   CommissionTypeName { get; set; } = string.Empty;
        public decimal  Amount             { get; set; }
        public string   Status             { get; set; } = string.Empty;
        public DateTime EarnedDate         { get; set; }
        public DateTime PaymentDate        { get; set; }
    }

    public class BoostWeekStatsDto
    {
        public string  WeekLabel      { get; set; } = string.Empty;
        public int     GoldCount      { get; set; }
        public int     GoldTarget     { get; set; }
        public int     PlatinumCount  { get; set; }
        public int     PlatinumTarget { get; set; }
        public decimal GoldAmount     { get; set; }
        public decimal PlatinumAmount { get; set; }
    }

    public class BoostMemberSummaryDto
    {
        public int TotalMembers      { get; set; }
        public int ActiveRebilling   { get; set; }
        public int InactiveRebilling { get; set; }
    }

    public class DualResidualDto
    {
        public DateTime EarnedDate     { get; set; }
        public decimal  Amount         { get; set; }
        public string   Status         { get; set; } = string.Empty;
        public int      EligiblePoints { get; set; }
    }
}
