namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.RankRequirements;

public class RankDashboardDto
{
    public IEnumerable<RankMemberCountDto> Ranks { get; set; } = new List<RankMemberCountDto>();
    public int TotalRankedMembers { get; set; }
}

public class RankMemberCountDto
{
    public int RankId { get; set; }
    public string RankName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public decimal Percentage { get; set; }
}
