namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

public class DualTreeNodeDto
{
    public string          MemberId   { get; set; } = string.Empty;
    public string          FullName   { get; set; } = string.Empty;
    public string          StatusCode { get; set; } = string.Empty;
    public int             Points     { get; set; }
    public DualTreeChildDto? LeftChild  { get; set; }
    public DualTreeChildDto? RightChild { get; set; }
}

public class DualTreeChildDto
{
    public string MemberId   { get; set; } = string.Empty;
    public string FullName   { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public int    Points     { get; set; }
    public bool   HasLeft    { get; set; }
    public bool   HasRight   { get; set; }
}

public class DualTreeStatsDto
{
    public decimal LeftLegPoints  { get; set; }
    public decimal RightLegPoints { get; set; }
}
