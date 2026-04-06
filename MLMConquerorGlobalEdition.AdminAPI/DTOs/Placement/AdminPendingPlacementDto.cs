namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement;

public class AdminPendingPlacementDto
{
    public string   MemberId                    { get; set; } = string.Empty;
    public string   FullName                    { get; set; } = string.Empty;
    public string   MemberCode                  { get; set; } = string.Empty;
    public string?  PhotoUrl                    { get; set; }
    public DateTime EnrolledAt                  { get; set; }
    public int      DaysRemainingInWindow       { get; set; }
    public double   WindowPercentUsed           { get; set; }
    public int      PlacementOpportunitiesUsed  { get; set; }
    public bool     IsAlreadyPlaced             { get; set; }
    public DateTime? PlacedAt                   { get; set; }
    public bool     CanCorrect                  { get; set; }
    public double   CorrectionHoursRemaining    { get; set; }
    public string?  CurrentTreeSide             { get; set; }
    public string?  CurrentParentMemberId       { get; set; }
    public string?  CurrentParentFullName       { get; set; }
    public bool     IsWindowExpired             { get; set; }
    public bool     IsBlocked                   { get; set; }
    public string   PlacementStatus             { get; set; } = string.Empty;

    // Admin-only: sponsor context
    public string?  SponsorMemberId             { get; set; }
    public string?  SponsorFullName             { get; set; }
}
