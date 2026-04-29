namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

public class DualTreeNodeDto
{
    public string             MemberId       { get; set; } = string.Empty;
    public string             FullName       { get; set; } = string.Empty;
    public string             StatusCode     { get; set; } = string.Empty;
    /// <summary>Sum of LeftLeg + RightLeg for this node — i.e. their DualTeamPoints (downline only).</summary>
    public int                Points         { get; set; }
    /// <summary>This member's own PersonalPoints — used for the upline contribution = Points + PersonalPoints.</summary>
    public int                PersonalPoints { get; set; }
    public DualTreeChildDto?  LeftChild      { get; set; }
    public DualTreeChildDto?  RightChild     { get; set; }
}

public class DualTreeChildDto
{
    public string                  MemberId       { get; set; } = string.Empty;
    public string                  FullName       { get; set; } = string.Empty;
    public string                  StatusCode     { get; set; } = string.Empty;
    public int                     Points         { get; set; }
    public int                     PersonalPoints { get; set; }
    public bool                    HasLeft        { get; set; }
    public bool                    HasRight       { get; set; }
    public DualTreeGrandchildDto?  LeftChild      { get; set; }
    public DualTreeGrandchildDto?  RightChild     { get; set; }
}

public class DualTreeGrandchildDto
{
    public string MemberId       { get; set; } = string.Empty;
    public string FullName       { get; set; } = string.Empty;
    public string StatusCode     { get; set; } = string.Empty;
    public int    Points         { get; set; }
    public int    PersonalPoints { get; set; }
    public bool   HasLeft        { get; set; }
    public bool   HasRight       { get; set; }
}

public class DualTreeStatsDto
{
    public decimal LeftLegPoints  { get; set; }
    public decimal RightLegPoints { get; set; }
}
