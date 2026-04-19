namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

/// <summary>
/// Aggregated status counts for the current member's entire enrollment downline.
/// </summary>
public class EnrollmentVisualizerStatsDto
{
    public int TotalMembers     { get; set; }
    public int TotalQualified   { get; set; }
    public int TotalUnqualified { get; set; }
    public int TotalCancelled   { get; set; }
}
