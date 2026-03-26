using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.CreateRankRequirement;

public class CreateRankRequirementHandler
    : IRequestHandler<CreateRankRequirementCommand, Result<int>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateRankRequirementHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<int>> Handle(
        CreateRankRequirementCommand request, CancellationToken cancellationToken)
    {
        var rankExists = await _db.RankDefinitions
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.RankId, cancellationToken);

        if (!rankExists)
            return Result<int>.Failure("RANK_NOT_FOUND", $"Rank definition '{request.RankId}' not found.");

        var req = request.Request;
        var now = _dateTime.Now;

        var entity = new RankRequirement
        {
            RankDefinitionId = request.RankId,
            LevelNo = req.LevelNo,
            PersonalPoints = req.PersonalPoints,
            TeamPoints = req.TeamPoints,
            MaxTeamPointsPerBranch = req.MaxTeamPointsPerBranch,
            EnrollmentTeam = req.EnrollmentTeam,
            PlacementQualifiedTeamMembers = req.PlacementQualifiedTeamMembers,
            EnrollmentQualifiedTeamMembers = req.EnrollmentQualifiedTeamMembers,
            MaxEnrollmentTeamPointsPerBranch = req.MaxEnrollmentTeamPointsPerBranch,
            ExternalMembers = req.ExternalMembers,
            SponsoredMembers = req.SponsoredMembers,
            SalesVolume = req.SalesVolume,
            RankBonus = req.RankBonus,
            DailyBonus = req.DailyBonus,
            MonthlyBonus = req.MonthlyBonus,
            LifetimeHoldingDuration = req.LifetimeHoldingDuration,
            RankDescription = req.RankDescription,
            CurrentRankDescription = req.CurrentRankDescription,
            AchievementMessage = req.AchievementMessage,
            CertificateUrl = req.CertificateUrl,
            CreationDate = now,
            CreatedBy = _currentUser.UserId
        };

        await _db.RankRequirements.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
