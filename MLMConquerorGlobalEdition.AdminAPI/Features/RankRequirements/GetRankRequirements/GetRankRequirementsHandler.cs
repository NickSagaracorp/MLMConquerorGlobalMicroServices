using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.GetRankRequirements;

public class GetRankRequirementsHandler
    : IRequestHandler<GetRankRequirementsQuery, Result<IEnumerable<RankRequirementDto>>>
{
    private readonly AppDbContext _db;

    public GetRankRequirementsHandler(AppDbContext db) => _db = db;

    public async Task<Result<IEnumerable<RankRequirementDto>>> Handle(
        GetRankRequirementsQuery request, CancellationToken cancellationToken)
    {
        var rankExists = await _db.RankDefinitions
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.RankId, cancellationToken);

        if (!rankExists)
            return Result<IEnumerable<RankRequirementDto>>.Failure(
                "RANK_NOT_FOUND", $"Rank definition '{request.RankId}' not found.");

        var items = await _db.RankRequirements
            .AsNoTracking()
            .Where(r => r.RankDefinitionId == request.RankId)
            .OrderBy(r => r.LevelNo)
            .Select(r => new RankRequirementDto
            {
                Id = r.Id,
                RankDefinitionId = r.RankDefinitionId,
                LevelNo = r.LevelNo,
                PersonalPoints = r.PersonalPoints,
                TeamPoints = r.TeamPoints,
                MaxTeamPointsPerBranch = r.MaxTeamPointsPerBranch,
                EnrollmentTeam = r.EnrollmentTeam,
                PlacementQualifiedTeamMembers = r.PlacementQualifiedTeamMembers,
                EnrollmentQualifiedTeamMembers = r.EnrollmentQualifiedTeamMembers,
                MaxEnrollmentTeamPointsPerBranch = r.MaxEnrollmentTeamPointsPerBranch,
                ExternalMembers = r.ExternalMembers,
                SponsoredMembers = r.SponsoredMembers,
                SalesVolume = r.SalesVolume,
                RankBonus = r.RankBonus,
                DailyBonus = r.DailyBonus,
                MonthlyBonus = r.MonthlyBonus,
                LifetimeHoldingDuration = r.LifetimeHoldingDuration,
                RankDescription = r.RankDescription,
                CurrentRankDescription = r.CurrentRankDescription,
                AchievementMessage = r.AchievementMessage,
                CertificateUrl = r.CertificateUrl
            })
            .ToListAsync(cancellationToken);

        return Result<IEnumerable<RankRequirementDto>>.Success(items);
    }
}
