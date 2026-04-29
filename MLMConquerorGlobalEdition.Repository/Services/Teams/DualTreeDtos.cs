namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Shared shape for the binary-tree visualizer used by both BizCenter and Admin.
/// Field names match the JSON contract consumed by the
/// <c>DualTeamBinaryTree</c> shared Blazor component.
/// </summary>
public class DualTreeNodeView
{
    public string MemberId       { get; set; } = string.Empty;
    public string FullName       { get; set; } = string.Empty;
    public string StatusCode     { get; set; } = string.Empty;
    /// <summary>Sum of LeftLegPoints + RightLegPoints — the node's own DualTeamPoints (downline only).</summary>
    public int    Points         { get; set; }
    public int    PersonalPoints { get; set; }
    public DualTreeChildView?  LeftChild  { get; set; }
    public DualTreeChildView?  RightChild { get; set; }
}

public class DualTreeChildView
{
    public string                 MemberId       { get; set; } = string.Empty;
    public string                 FullName       { get; set; } = string.Empty;
    public string                 StatusCode     { get; set; } = string.Empty;
    public int                    Points         { get; set; }
    public int                    PersonalPoints { get; set; }
    public bool                   HasLeft        { get; set; }
    public bool                   HasRight       { get; set; }
    public DualTreeGrandchildView? LeftChild     { get; set; }
    public DualTreeGrandchildView? RightChild    { get; set; }
}

public class DualTreeGrandchildView
{
    public string MemberId       { get; set; } = string.Empty;
    public string FullName       { get; set; } = string.Empty;
    public string StatusCode     { get; set; } = string.Empty;
    public int    Points         { get; set; }
    public int    PersonalPoints { get; set; }
    public bool   HasLeft        { get; set; }
    public bool   HasRight       { get; set; }
}
