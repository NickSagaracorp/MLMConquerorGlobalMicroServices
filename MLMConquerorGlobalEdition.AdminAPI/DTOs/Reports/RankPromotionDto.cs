namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Reports;

public record RankPromotionDto(
    string   MemberId,
    string   FullName,
    string   Email,
    int      RankDefinitionId,
    string   RankName,
    int      RankSortOrder,
    DateTime AchievedAt,
    string?  PreviousRankName);
