using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;

/// <summary>
/// Single source of truth for commission payloads, shared by BizCenter (member's own
/// view) and Admin (member profile drill-down). Field names match the JSON contract
/// consumed by the SharedComponents Blazor components and existing BizCenter DTOs.
/// </summary>
public class CommissionSummaryView
{
    public decimal PendingTotal     { get; set; }
    public decimal PaidTotal        { get; set; }
    public decimal CurrentYearTotal { get; set; }
}

public class CommissionEarningView
{
    public string   Id                 { get; set; } = string.Empty;
    public string   CommissionTypeName { get; set; } = string.Empty;
    public string   CategoryName       { get; set; } = string.Empty;
    public string?  Description        { get; set; }
    public decimal  Amount             { get; set; }
    public string   Status             { get; set; } = string.Empty;
    public DateTime EarnedDate         { get; set; }
    public DateTime PaymentDate        { get; set; }
    public DateTime? PeriodDate        { get; set; }
}

public class CommissionHistoryYearView
{
    public int                              Year        { get; set; }
    public decimal                          TotalIncome { get; set; }
    public List<CommissionHistoryMonthView> Months      { get; set; } = new();
}

public class CommissionHistoryMonthView
{
    public int     MonthNo     { get; set; }
    public string  MonthName   { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
}

public class CommissionBreakdownView
{
    public string  CommissionTypeName { get; set; } = string.Empty;
    public string  Detail             { get; set; } = string.Empty;
    public decimal Amount             { get; set; }
}

public class CommissionMonthBreakdownView
{
    public string                       CommissionTypeName { get; set; } = string.Empty;
    public List<CommissionMonthItemView> Items             { get; set; } = new();
}

public class CommissionMonthItemView
{
    public DateTime EarnedDate  { get; set; }
    public DateTime PaymentDate { get; set; }
    public string   Detail      { get; set; } = string.Empty;
    public decimal  Amount      { get; set; }
    public string   Status      { get; set; } = string.Empty;
}

/// <summary>Used for Fast Start Bonus / Presidential Bonus summary stats.</summary>
public class CommissionBonusSummaryView
{
    public int                  Count              { get; set; }
    public decimal              TotalAmount        { get; set; }
    public List<FsbWindowView>? Windows            { get; set; }
    public bool                 IsExtendedMode     { get; set; }
    public bool                 IsDisqualifiedW2W3 { get; set; }
}

public class FsbWindowView
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

public class BoostBonusMemberSummaryView
{
    public int TotalMembers      { get; set; }
    public int ActiveRebilling   { get; set; }
    public int InactiveRebilling { get; set; }
}

public class BoostBonusWeekStatsView
{
    public string WeekLabel            { get; set; } = string.Empty;
    public int    GoldCount            { get; set; }
    public int    GoldTarget           { get; set; }
    public int    PlatinumCount        { get; set; }
    public int    PlatinumTarget       { get; set; }
    public int    GoldPoints           { get; set; }
    public int    GoldPointsTarget     { get; set; }
    public int    PlatinumPoints       { get; set; }
    public int    PlatinumPointsTarget { get; set; }
}

public class CarBonusAmbassadorView
{
    public string  MemberId        { get; set; } = string.Empty;
    public string  AmbassadorName  { get; set; } = string.Empty;
    public decimal TotalPoints     { get; set; }
    public int     EligiblePoints  { get; set; }
}

public class CarBonusBranchView
{
    public List<CarBonusBranchMemberView> Members { get; set; } = new();
}

public class CarBonusBranchMemberView
{
    public string    OrderNo         { get; set; } = string.Empty;
    public string    FullName        { get; set; } = string.Empty;
    public string    MembershipLevel { get; set; } = string.Empty;
    public DateTime? ExpirationDate  { get; set; }
    public int       Points          { get; set; }
}

public class CarBonusStatsView
{
    public decimal TotalPoints      { get; set; }
    public decimal EligiblePoints   { get; set; }
    public int     ProgressPercent  { get; set; }
    public int     TeamPointsTarget { get; set; }
    public string  MonthLabel       { get; set; } = string.Empty;
}
