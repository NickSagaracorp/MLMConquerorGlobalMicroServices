namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

/// <summary>Used for Fast Start Bonus summary stats including countdown windows.</summary>
public class CommissionBonusSummaryDto
{
    public int                 Count              { get; set; }
    public decimal             TotalAmount        { get; set; }
    public List<FsbWindowDto>? Windows            { get; set; }
    /// <summary>True when FSB1 normal (7-day) window expired without earning it — only Extended W1 is shown.</summary>
    public bool                IsExtendedMode     { get; set; }
    /// <summary>True when FSB1 Extended was earned — ambassador is disqualified from FSB2/FSB3.</summary>
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
    /// <summary>Should not be rendered (e.g. W2/W3 when in extended mode).</summary>
    public bool      IsHidden       { get; set; }
}
