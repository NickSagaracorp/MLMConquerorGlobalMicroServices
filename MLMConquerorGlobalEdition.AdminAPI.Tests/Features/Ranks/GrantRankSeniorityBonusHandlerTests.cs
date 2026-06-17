using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GrantRankSeniorityBonus;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Xunit;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Ranks;

public class GrantRankSeniorityBonusHandlerTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static GrantRankSeniorityBonusHandler BuildHandler(AppDbContext db)
    {
        var dt = new Mock<IDateTimeProvider>();
        dt.Setup(d => d.Now).Returns(Now);

        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns("admin-user");

        return new GrantRankSeniorityBonusHandler(db, dt.Object, user.Object);
    }

    /// <summary>Seeds a seniority CommissionType for the given rank in category 9.</summary>
    private static void SeedSeniorityType(AppDbContext db, int rankId, decimal amount, int typeId = 0)
    {
        if (typeId == 0) typeId = 100 + rankId;
        db.CommissionTypes.Add(new CommissionType
        {
            Id = typeId,
            CommissionCategoryId = RankSeniorityBonus.CategoryId,
            Name = $"Rank Seniority Bonus – Rank {rankId}",
            LifeTimeRank = rankId,
            Amount = amount,
            PaymentDelayDays = 0,
            IsActive = true,
            CreationDate = Now,
            CreatedBy = "seed"
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Grant_CreatesCommissionEarning_WithRankSeniorityType()
    {
        await using var db = InMemoryDbHelper.Create();
        // Rank 2 → type id 102 (100 + rankId), amount 250m
        SeedSeniorityType(db, 2, 250m, typeId: 102);

        var result = await BuildHandler(db).Handle(
            new GrantRankSeniorityBonusCommand("AMB-1", 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();

        var earning = await db.CommissionEarnings.SingleAsync();
        earning.BeneficiaryMemberId.Should().Be("AMB-1");
        earning.CommissionTypeId.Should().Be(102);
        earning.Amount.Should().Be(250m);
        earning.IsManualEntry.Should().BeTrue();
        earning.Status.Should().Be(CommissionEarningStatus.Pending);
        earning.CreatedBy.Should().Be("admin-user");
    }

    [Fact]
    public async Task Grant_WhenAlreadyGranted_Fails()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedSeniorityType(db, 2, 250m, typeId: 102);

        // Pre-seed a granted earning for this member + type
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-1",
            CommissionTypeId = 102,
            Amount = 250m,
            Status = CommissionEarningStatus.Paid,
            EarnedDate = Now.AddDays(-1),
            PaymentDate = Now.AddDays(-1),
            IsManualEntry = true,
            CreationDate = Now.AddDays(-1),
            CreatedBy = "admin-user",
            LastUpdateDate = Now.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var result = await BuildHandler(db).Handle(
            new GrantRankSeniorityBonusCommand("AMB-1", 2), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SENIORITY_ALREADY_GRANTED");
    }

    [Fact]
    public async Task Grant_WhenNoTypeForRank_Fails()
    {
        await using var db = InMemoryDbHelper.Create();
        // No seniority type seeded for rank 5

        var result = await BuildHandler(db).Handle(
            new GrantRankSeniorityBonusCommand("AMB-1", 5), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SENIORITY_TYPE_NOT_FOUND");
    }
}
