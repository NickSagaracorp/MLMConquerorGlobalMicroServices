namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement;

public class PlacementCandidateDto
{
    public string  MemberId           { get; set; } = string.Empty;
    public string  FullName           { get; set; } = string.Empty;
    public string? PhotoUrl           { get; set; }
    /// <summary>Number of floating descendants from a previous unplacement. 0 = never placed.</summary>
    public int     FloatingSubtreeSize { get; set; }
}
