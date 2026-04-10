using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.DTOs;

namespace MLMConquerorGlobalEdition.RankEngine.Mappings;

public static class RankEngineMappingExtensions
{
    public static RankDefinitionResponse ToResponse(this RankDefinition entity) => new()
    {
        Id                     = entity.Id,
        Name                   = entity.Name,
        Description            = entity.Description,
        SortOrder              = entity.SortOrder,
        Status                 = entity.Status.ToString(),
        CertificateTemplateUrl = entity.CertificateTemplateUrl,
        Requirements           = entity.Requirements.Select(r => r.ToResponse()).ToList()
    };

    public static RankRequirementResponse ToResponse(this RankRequirement entity) => new()
    {
        Id                               = entity.Id,
        LevelNo                          = entity.LevelNo,
        PersonalPoints                   = entity.PersonalPoints,
        TeamPoints                       = entity.TeamPoints,
        MaxTeamPointsPerBranch           = entity.MaxTeamPointsPerBranch,
        EnrollmentTeam                   = entity.EnrollmentTeam,
        PlacementQualifiedTeamMembers    = entity.PlacementQualifiedTeamMembers,
        EnrollmentQualifiedTeamMembers   = entity.EnrollmentQualifiedTeamMembers,
        MaxEnrollmentTeamPointsPerBranch = entity.MaxEnrollmentTeamPointsPerBranch,
        ExternalMembers                  = entity.ExternalMembers,
        SponsoredMembers                 = entity.SponsoredMembers,
        SalesVolume                      = entity.SalesVolume,
        RankBonus                        = entity.RankBonus,
        DailyBonus                       = entity.DailyBonus,
        MonthlyBonus                     = entity.MonthlyBonus,
        LifetimeHoldingDuration          = entity.LifetimeHoldingDuration,
        RankDescription                  = entity.RankDescription,
        CurrentRankDescription           = entity.CurrentRankDescription,
        AchievementMessage               = entity.AchievementMessage,
        CertificateUrl                   = entity.CertificateUrl
    };
}
