namespace MLMConquerorGlobalEdition.SharedAPICenter.DTOs;

/// <summary>
/// Current rank information for a member, exposed to trusted external consumers.
/// All fields are nullable — a member may not yet have achieved any rank.
/// </summary>
public class ExternalMemberRankDto
{
    public string MemberId { get; set; } = string.Empty;
    public string? CurrentRankName { get; set; }
    public int? CurrentRankSortOrder { get; set; }
    public DateTime? AchievedAt { get; set; }
}
