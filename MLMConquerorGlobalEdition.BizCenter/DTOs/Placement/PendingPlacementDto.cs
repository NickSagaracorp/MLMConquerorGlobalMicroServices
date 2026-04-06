namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;

public class PendingPlacementDto
{
    public string MemberId              { get; set; } = string.Empty;
    public string FullName              { get; set; } = string.Empty;
    public string MemberCode            { get; set; } = string.Empty;
    public string? PhotoUrl             { get; set; }
    public DateTime EnrolledAt          { get; set; }
    public int DaysRemainingInWindow    { get; set; }   // 0..30, negative = expired
    public double WindowPercentUsed     { get; set; }   // 0..100
    public int PlacementOpportunitiesUsed { get; set; } // 0 | 1 | 2
    public bool IsAlreadyPlaced         { get; set; }   // Currently in a node
    public DateTime? PlacedAt           { get; set; }
    public bool CanCorrect              { get; set; }   // Within 72h correction window
    public double CorrectionHoursRemaining { get; set; }
    public string? CurrentTreeSide      { get; set; }   // "Left" | "Right"
    public string? CurrentParentMemberId { get; set; }
    public string? CurrentParentFullName { get; set; }
    public bool IsWindowExpired         { get; set; }
    public bool IsBlocked               { get; set; }   // Used both opportunities
    public string PlacementStatus       { get; set; } = string.Empty; // Unplaced | Placed | AutoPlaced | Blocked | Expired
}
