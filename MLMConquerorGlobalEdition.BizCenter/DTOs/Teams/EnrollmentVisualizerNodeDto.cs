namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

/// <summary>
/// Represents a single node in the enrollment tree visualizer.
/// </summary>
public class EnrollmentVisualizerNodeDto
{
    public string MemberId    { get; set; } = string.Empty;
    public string FullName    { get; set; } = string.Empty;
    /// <summary>Q = Qualified (Active), U = Unqualified (Inactive/Suspended), C = Cancelled (Terminated/Pending)</summary>
    public string StatusCode  { get; set; } = "Q";
    public int    Points      { get; set; }
    public bool   HasChildren { get; set; }
}
