namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;

public class CeoDashboardDto
{
    // ── MEMBERS ──────────────────────────────────────────────────────────────
    public int TotalMembers { get; set; }
    public int TotalAmbassadors { get; set; }
    public int TotalExternalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int InactiveMembers { get; set; }
    public int SuspendedMembers { get; set; }
    public int TerminatedMembers { get; set; }
    public int NewMembersToday { get; set; }
    public int NewMembersThisWeek { get; set; }
    public int NewMembersThisMonth { get; set; }
    public int NewMembersLastMonth { get; set; }

    // ── FINANCIAL ────────────────────────────────────────────────────────────
    public decimal RevenueThisMonth { get; set; }
    public decimal RevenueLastMonth { get; set; }
    public decimal RevenueThisYear { get; set; }
    public int OrdersThisMonth { get; set; }
    public decimal AvgOrderValueThisMonth { get; set; }

    // ── COMMISSIONS ──────────────────────────────────────────────────────────
    public decimal CommissionsPaidAllTime { get; set; }
    public decimal CommissionsPaidThisMonth { get; set; }
    public decimal CommissionsPending { get; set; }

    // ── SUBSCRIPTIONS ────────────────────────────────────────────────────────
    public int ActiveSubscriptions { get; set; }
    public int ExpiringSubscriptions30Days { get; set; }
    public int CancelledSubscriptionsThisMonth { get; set; }

    // ── SUPPORT ──────────────────────────────────────────────────────────────
    public int TicketsOpen { get; set; }
    public int TicketsInProgress { get; set; }
    public int TicketsCritical { get; set; }
    public int TicketsResolvedThisMonth { get; set; }

    // ── TOKENS ───────────────────────────────────────────────────────────────
    public int TokenCodesTotal { get; set; }
    public int TokenCodesUsed { get; set; }
    public int TokenCodesAvailable { get; set; }

    // ── PAYMENTS ─────────────────────────────────────────────────────────────
    public int PaymentsPending { get; set; }
    public int PaymentsFailed { get; set; }

    // ── LISTS ─────────────────────────────────────────────────────────────────
    public List<RecentMemberItem> RecentMembers { get; set; } = new();
    public List<MembershipBreakdownItem> MembershipBreakdown { get; set; } = new();
    public List<AmbassadorsByLevelItem> AmbassadorsByLevel { get; set; } = new();
    public List<MonthlySignupItem> MonthlySignups { get; set; } = new();
    public List<MembersByCountryItem> MembersByCountry { get; set; } = new();
    public BillingRunDto? LastBillingRun { get; set; }
}

public record RecentMemberItem(
    string MemberId,
    string FullName,
    string MemberType,
    string Status,
    DateTime EnrollDate,
    string? SponsorId);

public record MembershipBreakdownItem(
    string LevelName,
    int ActiveCount,
    bool IsFree);

public record AmbassadorsByLevelItem(
    string LevelName,
    int Count,
    bool IsFree);

public record MonthlySignupItem(
    int Month,
    string MembershipLevel,
    string Status,
    int Count);

public record MembersByCountryItem(
    string CountryName,
    int ActiveCount);

public class BillingRunDto
{
    public DateTime RunDate { get; set; }
    public int TotalDue { get; set; }
    public int TotalSuccessful { get; set; }
    public int TotalFailed { get; set; }
    public int TotalPending { get; set; }
    public decimal RevenueCollected { get; set; }
    public decimal RevenueMissed { get; set; }
    public decimal SuccessRate { get; set; }
    public List<BillingRunLevelItem> ByLevel { get; set; } = new();
}

public record BillingRunLevelItem(
    string LevelName,
    decimal RenewalPrice,
    int DueCount,
    int SuccessCount,
    int FailedCount,
    int PendingCount,
    decimal RevenueCollected,
    decimal RevenueMissed);
