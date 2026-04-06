namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;

public class PlacementHistoryDto
{
    public long     Id                   { get; set; }
    public string   MemberId             { get; set; } = string.Empty;
    public string   MemberFullName       { get; set; } = string.Empty;
    public string   MemberCode           { get; set; } = string.Empty;
    public string   Action               { get; set; } = string.Empty; // Placed | Removed | AutoPlaced
    public string?  TargetParentMemberId { get; set; }
    public string?  TargetParentFullName { get; set; }
    public string?  Side                 { get; set; }
    public DateTime ExecutedAt           { get; set; }
    public string   ExecutedByType       { get; set; } = string.Empty; // ambassador | admin | system
}
