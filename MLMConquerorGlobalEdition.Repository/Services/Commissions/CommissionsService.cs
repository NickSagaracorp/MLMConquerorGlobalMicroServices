using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;

/// <inheritdoc />
public class CommissionsService : ICommissionsService
{
    // Category IDs — mirror BizCenter handler constants.
    private const int FastStartBonusCategoryId    = 2;
    private const int DualTeamResidualCategoryId  = 3;
    private const int PresidentialBonusCategoryId = 4;
    private const int BoostBonusCategoryId        = 4;
    private const int CarBonusCategoryId          = 4; // BizCenter handler uses 4 + name "Car"
    private const int CarBonusTypeId              = 85;

    // Comp plan: Elite=6 pts, Turbo=6 pts, VIP=1 pt per active subscription.
    private static readonly Dictionary<int, int> LevelPoints = new() { [2] = 1, [3] = 6, [4] = 6 };

    private readonly AppDbContext _db;

    public CommissionsService(AppDbContext db) => _db = db;

    // ─── Summary ───────────────────────────────────────────────────────────
    public async Task<CommissionSummaryView> GetSummaryAsync(
        string memberId, CancellationToken ct = default)
    {
        var currentYear = DateTime.UtcNow.Year;

        var earnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Select(c => new { c.Amount, c.Status, c.EarnedDate })
            .ToListAsync(ct);

        return new CommissionSummaryView
        {
            PendingTotal     = earnings.Where(c => c.Status == CommissionEarningStatus.Pending).Sum(c => c.Amount),
            PaidTotal        = earnings.Where(c => c.Status == CommissionEarningStatus.Paid).Sum(c => c.Amount),
            CurrentYearTotal = earnings.Where(c => c.Status == CommissionEarningStatus.Paid && c.EarnedDate.Year == currentYear).Sum(c => c.Amount)
        };
    }

    // ─── Commissions list ──────────────────────────────────────────────────
    public async Task<PagedResult<CommissionEarningView>> GetCommissionsAsync(
        string memberId, int page, int pageSize,
        string? status, DateTime? from, DateTime? to,
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
            .Where(c => from == null || c.EarnedDate >= from.Value)
            .Where(c => to   == null || c.EarnedDate <= to.Value)
            .Join(
                _db.CommissionTypes,
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { Earning = c, CommType = ct2 })
            .Join(
                _db.CommissionCategories,
                x   => x.CommType.CommissionCategoryId,
                cat => cat.Id,
                (x, cat) => new CommissionEarningView
                {
                    Id                 = x.Earning.Id,
                    CommissionTypeName = x.CommType.Name,
                    CategoryName       = cat.Name,
                    Amount             = x.Earning.Amount,
                    Status             = x.Earning.Status.ToString(),
                    EarnedDate         = x.Earning.EarnedDate,
                    PaymentDate        = x.Earning.PaymentDate,
                    PeriodDate         = x.Earning.PeriodDate
                });

        return await PageEarningsAsync(query, page, pageSize, ct);
    }

    // ─── History ───────────────────────────────────────────────────────────
    public async Task<List<CommissionHistoryYearView>> GetHistoryAsync(
        string memberId, CancellationToken ct = default)
    {
        var grouped = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId && c.Status == CommissionEarningStatus.Paid)
            .GroupBy(c => new { c.EarnedDate.Year, c.EarnedDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Sum(c => c.Amount)
            })
            .OrderByDescending(g => g.Year)
            .ThenByDescending(g => g.Month)
            .ToListAsync(ct);

        return grouped
            .GroupBy(g => g.Year)
            .Select(yg => new CommissionHistoryYearView
            {
                Year        = yg.Key,
                TotalIncome = yg.Sum(m => m.Total),
                Months      = yg
                    .OrderByDescending(m => m.Month)
                    .Select(m => new CommissionHistoryMonthView
                    {
                        MonthNo     = m.Month,
                        MonthName   = new DateTime(m.Year, m.Month, 1).ToString("MMMM"),
                        TotalIncome = m.Total
                    })
                    .ToList()
            })
            .ToList();
    }

    // ─── Breakdown ─────────────────────────────────────────────────────────
    public async Task<List<CommissionBreakdownView>> GetBreakdownAsync(
        string memberId, DateTime paymentDate, DateTime? earnedDate,
        CancellationToken ct = default)
    {
        var targetDateUtc = paymentDate.Date;

        var raw = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.PaymentDate.Date == targetDateUtc
                     && (earnedDate == null || c.EarnedDate.Date == earnedDate.Value.Date))
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

        return raw.Select(x =>
        {
            string detail;
            if (!string.IsNullOrWhiteSpace(x.Notes))
            {
                // Notes stores human-readable detail (e.g. FSB "Member1 — Member2" format)
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
            return new CommissionBreakdownView
            {
                CommissionTypeName = x.TypeName,
                Detail             = detail,
                Amount             = x.Amount
            };
        }).ToList();
    }

    // ─── Month breakdown ───────────────────────────────────────────────────
    public async Task<List<CommissionMonthBreakdownView>> GetMonthBreakdownAsync(
        string memberId, int year, int month,
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

        return raw
            .GroupBy(x => x.TypeName)
            .Select(g => new CommissionMonthBreakdownView
            {
                CommissionTypeName = g.Key,
                Items = g.Select(i => new CommissionMonthItemView
                {
                    EarnedDate  = i.EarnedDate,
                    PaymentDate = i.PaymentDate,
                    Detail      = i.Description,
                    Amount      = i.Amount,
                    Status      = i.Status
                }).ToList()
            })
            .ToList();
    }

    // ─── Dual residual ─────────────────────────────────────────────────────
    public Task<PagedResult<CommissionEarningView>> GetDualResidualAsync(
        string memberId, int page, int pageSize, CancellationToken ct = default)
        => GetCategoryPagedAsync(memberId, page, pageSize,
            categoryId: DualTeamResidualCategoryId, nameContains: null,
            includeDescription: false, ct);

    // ─── Fast Start Bonus list ─────────────────────────────────────────────
    public Task<PagedResult<CommissionEarningView>> GetFastStartBonusAsync(
        string memberId, int page, int pageSize, CancellationToken ct = default)
        => GetCategoryPagedAsync(memberId, page, pageSize,
            categoryId: FastStartBonusCategoryId, nameContains: null,
            includeDescription: true, ct);

    // ─── Fast Start Bonus summary (windows) ────────────────────────────────
    public async Task<CommissionBonusSummaryView> GetFastStartBonusSummaryAsync(
        string memberId, CancellationToken ct = default)
    {
        var now = DateTime.Now;

        // 1. Eligible sponsored member IDs (only active Elite / Turbo memberships count)
        var eligibleMemberIds = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => s.SubscriptionStatus == MembershipStatus.Active)
            .Join(
                _db.MembershipLevels.Where(l => l.Name.Contains("Elite") || l.Name.Contains("Turbo")),
                s => s.MembershipLevelId,
                l => l.Id,
                (s, _) => s.MemberId)
            .ToHashSetAsync(ct);

        // Enroll dates of directly-sponsored members with eligible membership
        var sponsoredEnrollments = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == memberId && eligibleMemberIds.Contains(m.MemberId))
            .Select(m => m.EnrollDate)
            .ToListAsync(ct);

        // 2. FSB earnings (all non-cancelled) — carry EarnedDate to compute dynamic windows
        var fsbEarnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId && c.Status != CommissionEarningStatus.Cancelled)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == FastStartBonusCategoryId),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { ct2.TriggerOrder, c.EarnedDate, c.Amount })
            .ToListAsync(ct);

        var totalCount  = fsbEarnings.Count;
        var totalAmount = fsbEarnings.Sum(x => x.Amount);

        // Earliest earn date & total amount per TriggerOrder
        var earnByOrder = fsbEarnings
            .GroupBy(x => x.TriggerOrder)
            .ToDictionary(g => g.Key, g => (
                EarnedDate: g.Min(x => x.EarnedDate),
                Amount:     g.Sum(x => x.Amount)));

        // 3. Countdown record (keyed by UserId, not MemberId string)
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
            return new CommissionBonusSummaryView
            {
                Count       = totalCount,
                TotalAmount = totalAmount
            };

        // 4. FSB1 normal earned = TriggerOrder 1 commission earned before W1 normal end
        var w1NormalEnd = countdown.FastStartBonus1End;
        var w1ExtEnd    = countdown.FastStartBonus1ExtendedEnd;

        earnByOrder.TryGetValue(1, out var fsb1Earn);
        var fsb1NormalEarned   = fsb1Earn != default && fsb1Earn.EarnedDate <= w1NormalEnd;
        var fsb1ExtendedEarned = fsb1Earn != default && fsb1Earn.EarnedDate > w1NormalEnd && fsb1Earn.EarnedDate <= w1ExtEnd;

        earnByOrder.TryGetValue(2, out var fsb2Earn);
        earnByOrder.TryGetValue(3, out var fsb3Earn);

        // 5. Dynamic W2/W3 dates — FSB2 starts the moment FSB1 was earned (if earned early)
        var w2Start = fsb1NormalEarned
            ? fsb1Earn.EarnedDate
            : countdown.FastStartBonus2Start;
        var w2End   = fsb1NormalEarned
            ? fsb1Earn.EarnedDate.AddDays(7)
            : countdown.FastStartBonus2End;

        var w3Start = fsb2Earn != default
            ? fsb2Earn.EarnedDate
            : countdown.FastStartBonus3Start;
        var w3End   = fsb2Earn != default
            ? fsb2Earn.EarnedDate.AddDays(7)
            : countdown.FastStartBonus3End;

        // 6. Mode flags
        var isExtendedMode     = now > w1NormalEnd && !fsb1NormalEarned;
        var isDisqualifiedW2W3 = fsb1ExtendedEarned;

        // 7. Build windows
        FsbWindowView BuildWindow(int num, bool isPromo, DateTime start, DateTime end, decimal amount, bool hidden)
        {
            var isCompleted    = amount > 0;  // earned = completed regardless of whether window has closed
            var isExpired      = !isCompleted && now > end;
            var isActive       = !isCompleted && !isExpired && now >= start;
            var sponsoredCount = sponsoredEnrollments.Count(d => d >= start && d <= end);
            return new FsbWindowView
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

        var w1NormalStart = countdown.FastStartBonus1Start;
        var w1ExtStart    = countdown.FastStartBonus1ExtendedStart;

        var windows = new List<FsbWindowView>
        {
            // Normal W1
            BuildWindow(1, false, w1NormalStart, w1NormalEnd,
                fsb1NormalEarned ? fsb1Earn.Amount : 0m,
                hidden: false),

            // Normal W2 — hidden when in extended mode or disqualified
            BuildWindow(2, false, w2Start, w2End,
                fsb2Earn != default ? fsb2Earn.Amount : 0m,
                hidden: isExtendedMode || isDisqualifiedW2W3),

            // Normal W3 — hidden when in extended mode or disqualified
            BuildWindow(3, false, w3Start, w3End,
                fsb3Earn != default ? fsb3Earn.Amount : 0m,
                hidden: isExtendedMode || isDisqualifiedW2W3),

            // Extended W1 (Promo)
            BuildWindow(1, true, w1ExtStart, w1ExtEnd,
                fsb1ExtendedEarned ? fsb1Earn.Amount : 0m,
                hidden: false),
        };

        return new CommissionBonusSummaryView
        {
            Count              = totalCount,
            TotalAmount        = totalAmount,
            Windows            = windows,
            IsExtendedMode     = isExtendedMode,
            IsDisqualifiedW2W3 = isDisqualifiedW2W3
        };
    }

    // ─── Presidential bonus list ──────────────────────────────────────────
    public Task<PagedResult<CommissionEarningView>> GetPresidentialBonusAsync(
        string memberId, int page, int pageSize, CancellationToken ct = default)
        => GetCategoryPagedAsync(memberId, page, pageSize,
            categoryId: PresidentialBonusCategoryId, nameContains: "Presidential",
            includeDescription: false, ct);

    // ─── Presidential bonus summary ────────────────────────────────────────
    public async Task<CommissionBonusSummaryView> GetPresidentialBonusSummaryAsync(
        string memberId, CancellationToken ct = default)
    {
        var summary = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == PresidentialBonusCategoryId
                                            && t.Name.Contains("Presidential")),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, _) => c.Amount)
            .GroupBy(_ => 1)
            .Select(g => new CommissionBonusSummaryView
            {
                Count       = g.Count(),
                TotalAmount = g.Sum()
            })
            .FirstOrDefaultAsync(ct);

        return summary ?? new CommissionBonusSummaryView();
    }

    // ─── Boost bonus list ─────────────────────────────────────────────────
    public Task<PagedResult<CommissionEarningView>> GetBoostBonusAsync(
        string memberId, int page, int pageSize, CancellationToken ct = default)
        => GetCategoryPagedAsync(memberId, page, pageSize,
            categoryId: BoostBonusCategoryId, nameContains: "Boost",
            includeDescription: false, ct);

    // ─── Boost bonus member summary ────────────────────────────────────────
    public async Task<BoostBonusMemberSummaryView> GetBoostBonusMemberSummaryAsync(
        string memberId, CancellationToken ct = default)
    {
        // Downline member IDs from enrollment tree (direct and indirect)
        var downlineMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains($"/{memberId}/") && g.MemberId != memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (downlineMemberIds.Count == 0)
            return new BoostBonusMemberSummaryView();

        // Latest subscription per downline member
        var subscriptions = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => downlineMemberIds.Contains(s.MemberId) && !s.IsDeleted)
            .GroupBy(s => s.MemberId)
            .Select(g => g.OrderByDescending(s => s.CreationDate).First())
            .ToListAsync(ct);

        var total    = subscriptions.Count;
        var active   = subscriptions.Count(s => s.SubscriptionStatus == MembershipStatus.Active);
        var inactive = total - active;

        return new BoostBonusMemberSummaryView
        {
            TotalMembers      = total,
            ActiveRebilling   = active,
            InactiveRebilling = inactive
        };
    }

    // ─── Boost bonus week stats ───────────────────────────────────────────
    public async Task<BoostBonusWeekStatsView> GetBoostBonusWeekStatsAsync(
        string memberId, CancellationToken ct = default)
    {
        var today      = DateTime.UtcNow;
        var dayOfWeek  = (int)today.DayOfWeek;
        var daysToMon  = dayOfWeek == 0 ? -6 : -(dayOfWeek - 1);
        var weekStart  = today.AddDays(daysToMon).Date;
        var weekEnd    = weekStart.AddDays(7);

        // Resolve boost thresholds from CommissionTypes
        var boostTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && !t.IsPaidOnSignup && !t.ResidualBased
                     && !t.IsSponsorBonus && t.CommissionCategoryId == BoostBonusCategoryId)
            .OrderBy(t => t.LifeTimeRank)
            .ToListAsync(ct);

        var goldType     = boostTypes.FirstOrDefault(t => t.Name.Contains("Gold",     StringComparison.OrdinalIgnoreCase));
        var platinumType = boostTypes.FirstOrDefault(t => t.Name.Contains("Platinum", StringComparison.OrdinalIgnoreCase));

        int goldTarget     = goldType?.NewMembers     ?? 6;
        int platinumTarget = platinumType?.NewMembers ?? 12;

        // New Elite/Turbo Active enrollments in downline this week
        int[] eligibleLevelIds = [3, 4];
        var hierarchyFilter    = $"/{memberId}/";

        var downlineEnrollments = await (
            from mp  in _db.MemberProfiles.AsNoTracking()
            join g   in _db.GenealogyTree.AsNoTracking()           on mp.MemberId equals g.MemberId
            join sub in _db.MembershipSubscriptions.AsNoTracking() on mp.MemberId equals sub.MemberId
            where mp.EnrollDate >= weekStart
               && mp.EnrollDate <  weekEnd
               && sub.SubscriptionStatus == MembershipStatus.Active
               && eligibleLevelIds.Contains(sub.MembershipLevelId)
               && g.HierarchyPath.Contains(hierarchyFilter)
            select new { mp.MemberId, g.HierarchyPath }
        ).Distinct().ToListAsync(ct);

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

        // Qualification points per enrolled member from completed orders
        var enrolledIds = downlineEnrollments.Select(e => e.MemberId).Distinct().ToList();

        Dictionary<string, int> pointsPerMember;
        if (enrolledIds.Count > 0)
        {
            var memberPointsData = await (
                from o  in _db.Orders.AsNoTracking()
                join od in _db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
                join p  in _db.Products.AsNoTracking()     on od.ProductId equals p.Id
                where enrolledIds.Contains(o.MemberId)
                   && o.Status == OrderStatus.Completed
                select new { o.MemberId, Points = od.Quantity * p.QualificationPoins }
            ).ToListAsync(ct);

            pointsPerMember = memberPointsData
                .GroupBy(x => x.MemberId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Points));
        }
        else
        {
            pointsPerMember = new Dictionary<string, int>();
        }

        var minProductPoints = enrolledIds.Count > 0
            ? await _db.Products.AsNoTracking()
                .Where(p => p.IsActive && !p.IsDeleted && p.QualificationPoins >= 6)
                .MinAsync(p => (int?)p.QualificationPoins, ct) ?? 6
            : 6;

        int CappedPoints(int threshold)
        {
            if (threshold == 0)
                return downlineEnrollments.DistinctBy(e => e.MemberId)
                    .Sum(e => pointsPerMember.TryGetValue(e.MemberId, out var pts) ? pts : 0);
            var capPerLeg = (int)Math.Floor(threshold * 0.5);
            return downlineEnrollments
                .GroupBy(e => GetLeg(e.HierarchyPath, hierarchyFilter))
                .Sum(g => g.DistinctBy(e => e.MemberId)
                           .Take(capPerLeg)
                           .Sum(e => pointsPerMember.TryGetValue(e.MemberId, out var p2) ? p2 : 0));
        }

        return new BoostBonusWeekStatsView
        {
            WeekLabel            = $"Week of {weekStart:MMM d} - {weekStart.AddDays(6):MMM d}",
            GoldCount            = CappedCount(goldTarget),
            PlatinumCount        = CappedCount(platinumTarget),
            GoldTarget           = goldTarget,
            PlatinumTarget       = platinumTarget,
            GoldPoints           = CappedPoints(goldTarget),
            GoldPointsTarget     = goldTarget * minProductPoints,
            PlatinumPoints       = CappedPoints(platinumTarget),
            PlatinumPointsTarget = platinumTarget * minProductPoints
        };
    }

    // ─── Car bonus list ───────────────────────────────────────────────────
    public async Task<PagedResult<CommissionEarningView>> GetCarBonusAsync(
        string memberId, int page, int pageSize,
        DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        var query = _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Where(c => from == null || c.EarnedDate >= from.Value)
            .Where(c => to   == null || c.EarnedDate <= to.Value)
            .Join(
                _db.CommissionTypes.Where(t => t.CommissionCategoryId == CarBonusCategoryId
                                            && t.Name.Contains("Car")),
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { Earning = c, CommType = ct2 })
            .Join(
                _db.CommissionCategories,
                x   => x.CommType.CommissionCategoryId,
                cat => cat.Id,
                (x, cat) => new CommissionEarningView
                {
                    Id                 = x.Earning.Id,
                    CommissionTypeName = x.CommType.Name,
                    CategoryName       = cat.Name,
                    Amount             = x.Earning.Amount,
                    Status             = x.Earning.Status.ToString(),
                    EarnedDate         = x.Earning.EarnedDate,
                    PaymentDate        = x.Earning.PaymentDate,
                    PeriodDate         = x.Earning.PeriodDate
                });

        return await PageEarningsAsync(query, page, pageSize, ct);
    }

    // ─── Car bonus stats ──────────────────────────────────────────────────
    public async Task<CarBonusStatsView> GetCarBonusStatsAsync(
        string memberId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;

        // Resolve Car Bonus threshold from CommissionType config
        var carBonusType = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.Id == CarBonusTypeId && t.IsActive)
            .FirstOrDefaultAsync(ct);

        int teamPointsTarget = carBonusType?.TeamPoints ?? 1000;

        // Get all active downline subscription levels (one row per member, latest subscription)
        var hierarchyFilter = $"/{memberId}/";

        var downlineMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(hierarchyFilter) && g.MemberId != memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        int totalPoints = 0;
        if (downlineMemberIds.Count > 0)
        {
            var activeSubs = await _db.MembershipSubscriptions
                .AsNoTracking()
                .Where(s => downlineMemberIds.Contains(s.MemberId)
                         && s.SubscriptionStatus == MembershipStatus.Active
                         && !s.IsDeleted)
                .Select(s => new { s.MemberId, s.MembershipLevelId, s.CreationDate })
                .ToListAsync(ct);

            // Pick highest-level subscription per member and sum points
            totalPoints = activeSubs
                .GroupBy(s => s.MemberId)
                .Sum(g =>
                {
                    var levelId = g.OrderByDescending(s => s.CreationDate).First().MembershipLevelId;
                    return LevelPoints.TryGetValue(levelId, out var pts) ? pts : 0;
                });
        }

        var eligiblePoints  = totalPoints;
        var progressPercent = teamPointsTarget > 0
            ? (int)Math.Min(100, Math.Floor(eligiblePoints * 100.0 / teamPointsTarget))
            : 0;

        return new CarBonusStatsView
        {
            TotalPoints      = totalPoints,
            EligiblePoints   = eligiblePoints,
            ProgressPercent  = progressPercent,
            TeamPointsTarget = teamPointsTarget,
            MonthLabel       = today.ToString("MMMM yyyy")
        };
    }

    // ─── Car bonus ambassadors ────────────────────────────────────────────
    public async Task<List<CarBonusAmbassadorView>> GetCarBonusAmbassadorsAsync(
        string memberId, DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        _ = from; _ = to; // BizCenter handler does not filter by date — params kept for API parity

        var hierarchyFilter = $"/{memberId}/";

        // Get ALL downline member IDs regardless of enrollment date
        var downlineMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(hierarchyFilter) && g.MemberId != memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (downlineMemberIds.Count == 0)
            return new List<CarBonusAmbassadorView>();

        // Get member profiles
        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(mp => downlineMemberIds.Contains(mp.MemberId))
            .Select(mp => new { mp.MemberId, mp.FirstName, mp.LastName })
            .ToListAsync(ct);

        // Get active subscriptions
        var activeSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => downlineMemberIds.Contains(s.MemberId)
                     && s.SubscriptionStatus == MembershipStatus.Active
                     && !s.IsDeleted)
            .Select(s => new { s.MemberId, s.MembershipLevelId, s.CreationDate })
            .ToListAsync(ct);

        var subsByMember = activeSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.CreationDate).First().MembershipLevelId);

        return profiles
            .Select(mp =>
            {
                var levelId = subsByMember.TryGetValue(mp.MemberId, out var lid) ? lid : 0;
                var pts     = LevelPoints.TryGetValue(levelId, out var p) ? p : 0;
                return new CarBonusAmbassadorView
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
    }

    // ─── Car bonus branch ─────────────────────────────────────────────────
    public async Task<CarBonusBranchView> GetCarBonusBranchAsync(
        string branchMemberId, CancellationToken ct = default)
    {
        var hierarchyFilter = $"/{branchMemberId}/";

        // Include the branch ambassador themselves + all their downline
        var memberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.MemberId == branchMemberId
                     || g.HierarchyPath.Contains(hierarchyFilter))
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (memberIds.Count == 0)
            return new CarBonusBranchView();

        // Active subscriptions
        var activeSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => memberIds.Contains(s.MemberId)
                     && s.SubscriptionStatus == MembershipStatus.Active
                     && !s.IsDeleted)
            .Select(s => new
            {
                s.MemberId,
                s.MembershipLevelId,
                s.EndDate,
                s.LastOrderId,
                s.CreationDate
            })
            .ToListAsync(ct);

        var subsByMember = activeSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreationDate).First());

        // Membership level names
        var levelIds = subsByMember.Values.Select(s => s.MembershipLevelId).Distinct().ToList();
        var levels   = await _db.MembershipLevels
            .AsNoTracking()
            .Where(l => levelIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Name })
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        // Order numbers via LastOrderId
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

        // Member profiles
        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(mp => memberIds.Contains(mp.MemberId))
            .Select(mp => new { mp.MemberId, mp.FirstName, mp.LastName })
            .ToListAsync(ct);

        var members = profiles
            .Select(mp =>
            {
                var sub        = subsByMember.TryGetValue(mp.MemberId, out var s) ? s : null;
                var levelId    = sub?.MembershipLevelId ?? 0;
                var pts        = LevelPoints.TryGetValue(levelId, out var p) ? p : 0;
                var levelName  = levelId > 0 && levels.TryGetValue(levelId, out var ln) ? ln : "—";
                var orderNo    = sub?.LastOrderId != null && orderNos.TryGetValue(sub.LastOrderId, out var on) ? on : "—";
                var expDate    = sub?.EndDate;

                return new CarBonusBranchMemberView
                {
                    OrderNo         = orderNo,
                    FullName        = $"{mp.FirstName} {mp.LastName}".Trim(),
                    MembershipLevel = levelName,
                    ExpirationDate  = expDate,
                    Points          = pts
                };
            })
            .OrderByDescending(x => x.Points)
            .ThenBy(x => x.FullName)
            .ToList();

        return new CarBonusBranchView { Members = members };
    }

    // ─────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns earnings for a member filtered by category (and optional type-name substring),
    /// joined to category for the response, paged.
    /// </summary>
    private async Task<PagedResult<CommissionEarningView>> GetCategoryPagedAsync(
        string memberId, int page, int pageSize,
        int categoryId, string? nameContains, bool includeDescription,
        CancellationToken ct)
    {
        var typeFilter = nameContains is null
            ? _db.CommissionTypes.Where(t => t.CommissionCategoryId == categoryId)
            : _db.CommissionTypes.Where(t => t.CommissionCategoryId == categoryId && t.Name.Contains(nameContains));

        var earnings = _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId)
            .Join(
                typeFilter,
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new { Earning = c, CommType = ct2 });

        IQueryable<CommissionEarningView> query;
        if (includeDescription)
        {
            query = earnings.Join(
                _db.CommissionCategories,
                x   => x.CommType.CommissionCategoryId,
                cat => cat.Id,
                (x, cat) => new CommissionEarningView
                {
                    Id                 = x.Earning.Id,
                    CommissionTypeName = x.CommType.Name,
                    CategoryName       = cat.Name,
                    Description        = x.Earning.Notes,
                    Amount             = x.Earning.Amount,
                    Status             = x.Earning.Status.ToString(),
                    EarnedDate         = x.Earning.EarnedDate,
                    PaymentDate        = x.Earning.PaymentDate,
                    PeriodDate         = x.Earning.PeriodDate
                });
        }
        else
        {
            query = earnings.Join(
                _db.CommissionCategories,
                x   => x.CommType.CommissionCategoryId,
                cat => cat.Id,
                (x, cat) => new CommissionEarningView
                {
                    Id                 = x.Earning.Id,
                    CommissionTypeName = x.CommType.Name,
                    CategoryName       = cat.Name,
                    Amount             = x.Earning.Amount,
                    Status             = x.Earning.Status.ToString(),
                    EarnedDate         = x.Earning.EarnedDate,
                    PaymentDate        = x.Earning.PaymentDate,
                    PeriodDate         = x.Earning.PeriodDate
                });
        }

        return await PageEarningsAsync(query, page, pageSize, ct);
    }

    private static async Task<PagedResult<CommissionEarningView>> PageEarningsAsync(
        IQueryable<CommissionEarningView> query, int page, int pageSize, CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.EarnedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<CommissionEarningView>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };
    }
}
