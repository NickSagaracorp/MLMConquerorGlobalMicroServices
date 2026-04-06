namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;

public class PlacementTreeNodeDto
{
    public string  MemberId        { get; set; } = string.Empty;
    public string  FullName        { get; set; } = string.Empty;
    public string  MemberCode      { get; set; } = string.Empty;
    public string? PhotoUrl        { get; set; }

    public string? LeftChildId     { get; set; }
    public string? LeftChildName   { get; set; }
    public string? RightChildId    { get; set; }
    public string? RightChildName  { get; set; }

    public bool    IsLeftAvailable  { get; set; }
    public bool    IsRightAvailable { get; set; }

    public string? ParentMemberId  { get; set; }
    public int     Depth           { get; set; }
}

public class AvailableNodesResponse
{
    public string SponsorMemberId   { get; set; } = string.Empty;
    public string SponsorFullName   { get; set; } = string.Empty;
    public List<PlacementTreeNodeDto> Nodes { get; set; } = new();
}
