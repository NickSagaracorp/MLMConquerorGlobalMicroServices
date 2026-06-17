using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GrantRankSeniorityBonus;

public class GrantRankSeniorityBonusHandler : IRequestHandler<GrantRankSeniorityBonusCommand, Result<string>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public GrantRankSeniorityBonusHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(GrantRankSeniorityBonusCommand c, CancellationToken ct)
    {
        var type = await _db.CommissionTypes
            .FirstOrDefaultAsync(t => t.CommissionCategoryId == RankSeniorityBonus.CategoryId
                                      && t.LifeTimeRank == c.RankDefinitionId, ct);
        if (type is null)
            return Result<string>.Failure(
                "SENIORITY_TYPE_NOT_FOUND",
                $"No seniority bonus commission type found for rank {c.RankDefinitionId}");

        var already = await _db.CommissionEarnings
            .AnyAsync(e => e.BeneficiaryMemberId == c.MemberId && e.CommissionTypeId == type.Id, ct);
        if (already)
            return Result<string>.Failure(
                "SENIORITY_ALREADY_GRANTED",
                "This rank's seniority bonus was already granted to the member");

        var now = _dateTime.Now;
        var earning = new CommissionEarning
        {
            BeneficiaryMemberId = c.MemberId,
            CommissionTypeId = type.Id,
            Amount = type.Amount ?? 0m,
            Status = CommissionEarningStatus.Pending,
            EarnedDate = now,
            PaymentDate = now.AddDays(type.PaymentDelayDays),
            IsManualEntry = true,
            Notes = $"Rank seniority bonus – rank {c.RankDefinitionId}",
            CreationDate = now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy = _currentUser.UserId
        };

        _db.CommissionEarnings.Add(earning);
        await _db.SaveChangesAsync(ct);

        return Result<string>.Success(earning.Id);
    }
}
