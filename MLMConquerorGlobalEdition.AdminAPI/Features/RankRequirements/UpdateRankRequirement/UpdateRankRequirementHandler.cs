using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.UpdateRankRequirement;

public class UpdateRankRequirementHandler
    : IRequestHandler<UpdateRankRequirementCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateRankRequirementHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<bool>> Handle(
        UpdateRankRequirementCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.RankRequirements
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.RankDefinitionId == request.RankId, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("RANK_REQUIREMENT_NOT_FOUND", "Rank requirement not found.");

        var req = request.Request;
        entity.LevelNo = req.LevelNo;
        entity.PersonalPoints = req.PersonalPoints;
        entity.TeamPoints = req.TeamPoints;
        entity.MaxTeamPointsPerBranch = req.MaxTeamPointsPerBranch;
        entity.EnrollmentTeam = req.EnrollmentTeam;
        entity.PlacementQualifiedTeamMembers = req.PlacementQualifiedTeamMembers;
        entity.EnrollmentQualifiedTeamMembers = req.EnrollmentQualifiedTeamMembers;
        entity.MaxEnrollmentTeamPointsPerBranch = req.MaxEnrollmentTeamPointsPerBranch;
        entity.ExternalMembers = req.ExternalMembers;
        entity.SponsoredMembers = req.SponsoredMembers;
        entity.SalesVolume = req.SalesVolume;
        entity.RankBonus = req.RankBonus;
        entity.DailyBonus = req.DailyBonus;
        entity.MonthlyBonus = req.MonthlyBonus;
        entity.LifetimeHoldingDuration = req.LifetimeHoldingDuration;
        entity.RankDescription = req.RankDescription;
        entity.CurrentRankDescription = req.CurrentRankDescription;
        entity.AchievementMessage = req.AchievementMessage;
        entity.CertificateUrl = req.CertificateUrl;
        entity.LastUpdateDate = _dateTime.Now;
        entity.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
