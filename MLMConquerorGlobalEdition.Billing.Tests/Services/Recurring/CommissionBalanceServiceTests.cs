using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

public class CommissionBalanceServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> BuildClock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static CommissionType BuildResidualType() => new()
    {
        Id               = 1,
        CommissionCategoryId = 1,
        Name             = "Daily Residual",
        ResidualBased    = true,
        IsPaidOnSignup   = false,
        IsActive         = true,
        CreatedBy        = "seed",
        CreationDate     = FixedNow,
        LastUpdateDate   = FixedNow
    };

    private static DailyResidualEarning BuildDailyResidual(
        string memberId, decimal amount, CommissionEarningStatus status = CommissionEarningStatus.Pending)
    {
        return new DailyResidualEarning
        {
            BeneficiaryMemberId = memberId,
            Amount              = amount,
            EarnedDate          = FixedNow.AddDays(-1),
            Status              = status,
            CreatedBy           = "test",
            CreationDate        = FixedNow.AddDays(-1)
        };
    }

    private static CommissionEarning BuildGeneralEarning(string memberId, decimal amount)
    {
        return new CommissionEarning
        {
            BeneficiaryMemberId = memberId,
            CommissionTypeId    = 1,
            Amount              = amount,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = FixedNow.AddDays(-2),
            PaymentDate         = FixedNow.AddDays(-2),
            IsManualEntry       = false,
            CreatedBy           = "test",
            CreationDate        = FixedNow.AddDays(-2),
            LastUpdateDate      = FixedNow.AddDays(-2)
        };
    }

    // ── GetAvailableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableAsync_WhenDailyResidualBelowMinimum_ExcludesItFromAvailable()
    {
        var db      = TestDbContextFactory.Create();
        var service = new CommissionBalanceService(db, BuildClock().Object);

        var memberId = "mem-1";
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 50m)); // below default 100 minimum
        db.CommissionEarnings.Add(BuildGeneralEarning(memberId, 30m));
        await db.SaveChangesAsync();

        var summary = await service.GetAvailableAsync(memberId);

        summary.DailyResidualPending.Should().Be(50m);
        summary.EligibleDailyResidual.Should().Be(0m);   // below minimum — not counted
        summary.GeneralPending.Should().Be(30m);
        summary.Available.Should().Be(30m);              // only general counts
        summary.ConsolidationMinimum.Should().Be(100m);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenDailyResidualMeetsMinimum_IncludesItInAvailable()
    {
        var db      = TestDbContextFactory.Create();
        var service = new CommissionBalanceService(db, BuildClock().Object);

        var memberId = "mem-2";
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 120m)); // above default 100
        db.CommissionEarnings.Add(BuildGeneralEarning(memberId, 40m));
        await db.SaveChangesAsync();

        var summary = await service.GetAvailableAsync(memberId);

        summary.EligibleDailyResidual.Should().Be(120m);
        summary.Available.Should().Be(160m); // 40 + 120
    }

    [Fact]
    public async Task GetAvailableAsync_WhenCustomMinimumInGlobalParameters_UsesIt()
    {
        var db      = TestDbContextFactory.Create();
        var service = new CommissionBalanceService(db, BuildClock().Object);

        var memberId = "mem-3";
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "DailyResidualConsolidationMinimum", Value = "50",
            CreatedBy = "test", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 60m)); // above custom 50
        await db.SaveChangesAsync();

        var summary = await service.GetAvailableAsync(memberId);

        summary.ConsolidationMinimum.Should().Be(50m);
        summary.EligibleDailyResidual.Should().Be(60m);
    }

    // ── ConsolidateDailyResidualAsync ──────────────────────────────────────────

    [Fact]
    public async Task ConsolidateDailyResidualAsync_WhenBelowMinimum_ReturnsNull()
    {
        var db      = TestDbContextFactory.Create();
        db.CommissionTypes.Add(BuildResidualType());
        await db.SaveChangesAsync();

        var service  = new CommissionBalanceService(db, BuildClock().Object);
        var memberId = "mem-4";
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 40m));
        await db.SaveChangesAsync();

        var result = await service.ConsolidateDailyResidualAsync(memberId, "actor");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ConsolidateDailyResidualAsync_WhenAboveMinimum_CreatesConsolidatedEarningAndMarksRowsPaid()
    {
        var db      = TestDbContextFactory.Create();
        db.CommissionTypes.Add(BuildResidualType());
        await db.SaveChangesAsync();

        var service  = new CommissionBalanceService(db, BuildClock().Object);
        var memberId = "mem-5";
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 60m));
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 70m));
        await db.SaveChangesAsync();

        var consolidatedId = await service.ConsolidateDailyResidualAsync(memberId, "actor");

        consolidatedId.Should().NotBeNullOrEmpty();

        var earning = db.CommissionEarnings.FirstOrDefault(e => e.Id == consolidatedId);
        earning.Should().NotBeNull();
        earning!.Amount.Should().Be(130m);
        earning.Status.Should().Be(CommissionEarningStatus.Pending);

        var pendingCount = db.DailyResidualEarnings
            .Count(e => e.BeneficiaryMemberId == memberId && e.Status == CommissionEarningStatus.Pending);
        pendingCount.Should().Be(0); // all marked Paid

        var paidCount = db.DailyResidualEarnings
            .Count(e => e.BeneficiaryMemberId == memberId && e.Status == CommissionEarningStatus.Paid);
        paidCount.Should().Be(2);
    }

    [Fact]
    public async Task ConsolidateDailyResidualAsync_WhenAboveMinimum_SetsPaymentTrackingFieldsOnPaidRows()
    {
        var db      = TestDbContextFactory.Create();
        db.CommissionTypes.Add(BuildResidualType());
        await db.SaveChangesAsync();

        var service  = new CommissionBalanceService(db, BuildClock().Object);
        var memberId = "mem-5b";
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 60m));
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 70m));
        await db.SaveChangesAsync();

        var consolidatedId = await service.ConsolidateDailyResidualAsync(memberId, "my-actor");

        var paidRows = db.DailyResidualEarnings
            .Where(e => e.BeneficiaryMemberId == memberId)
            .ToList();

        foreach (var row in paidRows)
        {
            row.PaymentDate.Should().Be(FixedNow, "PaymentDate must be set to IDateTimeProvider.Now");
            row.CommentedBy.Should().Be("my-actor", "CommentedBy must echo the actor argument");
            row.PaymentComment.Should().NotBeNullOrEmpty("PaymentComment must describe the consolidation event");
            row.PaymentComment.Should().Contain(consolidatedId!, "PaymentComment must reference the consolidated CommissionEarning Id");
        }
    }

    [Fact]
    public async Task ConsolidateDailyResidualAsync_WhenBelowMinimum_LeavesPaymentFieldsNull()
    {
        var db      = TestDbContextFactory.Create();
        db.CommissionTypes.Add(BuildResidualType());
        await db.SaveChangesAsync();

        var service  = new CommissionBalanceService(db, BuildClock().Object);
        var memberId = "mem-5c";
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 40m)); // below minimum
        await db.SaveChangesAsync();

        await service.ConsolidateDailyResidualAsync(memberId, "actor");

        var row = db.DailyResidualEarnings.First(e => e.BeneficiaryMemberId == memberId);
        row.PaymentDate.Should().BeNull();
        row.CommentedBy.Should().BeNull();
        row.PaymentComment.Should().BeNull();
    }

    [Fact]
    public async Task ConsolidateDailyResidualAsync_IsIdempotent_AlreadyPaidRowsIgnored()
    {
        var db      = TestDbContextFactory.Create();
        db.CommissionTypes.Add(BuildResidualType());
        await db.SaveChangesAsync();

        var service  = new CommissionBalanceService(db, BuildClock().Object);
        var memberId = "mem-6";
        // One Paid row (already consolidated), one new Pending row below minimum
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 150m, CommissionEarningStatus.Paid));
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 20m,  CommissionEarningStatus.Pending));
        await db.SaveChangesAsync();

        var result = await service.ConsolidateDailyResidualAsync(memberId, "actor");

        // Only 20m pending, below minimum of 100m → should return null
        result.Should().BeNull();
    }

    // ── FundWithCommissionBalanceAsync ─────────────────────────────────────────

    [Fact]
    public async Task FundWithCommissionBalance_WhenNoTokenTypeConfigured_ReturnsFailure()
    {
        var db      = TestDbContextFactory.Create();
        db.CommissionTypes.Add(BuildResidualType());
        await db.SaveChangesAsync();

        var service  = new CommissionBalanceService(db, BuildClock().Object);
        var plan     = new RecurringBillingPlan
        {
            Id = 99, Name = "NoPlanToken", CycleType = RecurringCycleType.Every30Days,
            RetryCadenceDays = "1", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            TokenTypeId = null, IsActive = true,
            CreatedBy = "test", CreationDate = FixedNow, LastUpdateDate = FixedNow
        };

        var result = await service.FundWithCommissionBalanceAsync(
            memberId: "mem-7", plan: plan, tokenTypeIdOverride: null,
            amountDue: 50m, orderId: "order-1", productId: "prod-1", actor: "actor");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_TOKEN_TYPE");
    }

    [Fact]
    public async Task FundWithCommissionBalance_WhenDailyResidualConsolidated_SetsPaymentTrackingWithTokenAndOrderContext()
    {
        // Arrange: seed minimum requirements for a successful fund call
        var db = TestDbContextFactory.Create();
        db.CommissionTypes.Add(BuildResidualType());

        // Seed a TokenType so the token issuance can succeed
        db.TokenTypes.Add(new TokenType
        {
            Id = 1, Name = "Elite Monthly", IsActive = true,
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });

        await db.SaveChangesAsync();

        var memberId = "mem-fund";
        db.DailyResidualEarnings.Add(BuildDailyResidual(memberId, 120m)); // above minimum
        await db.SaveChangesAsync();

        var service = new CommissionBalanceService(db, BuildClock().Object);
        var plan = new RecurringBillingPlan
        {
            Id = 1, Name = "Travel Advantage", CycleType = RecurringCycleType.Every30Days,
            RetryCadenceDays = "1", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            TokenTypeId = 1, IsActive = true,
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        };

        var result = await service.FundWithCommissionBalanceAsync(
            memberId: memberId, plan: plan, tokenTypeIdOverride: null,
            amountDue: 50m, orderId: "order-renewal-99", productId: "prod-1", actor: "membership-token-purchase");

        result.IsSuccess.Should().BeTrue();

        // The consolidated DailyResidualEarning rows should have the full token + order context in PaymentComment
        var paidRows = db.DailyResidualEarnings
            .Where(e => e.BeneficiaryMemberId == memberId && e.Status == CommissionEarningStatus.Paid)
            .ToList();

        paidRows.Should().NotBeEmpty();
        foreach (var row in paidRows)
        {
            row.PaymentDate.Should().Be(FixedNow, "PaymentDate must be set from IDateTimeProvider");
            row.CommentedBy.Should().Be("membership-token-purchase", "CommentedBy must echo the actor");
            row.PaymentComment.Should().Contain("order-renewal-99", "PaymentComment must include the order Id");
            row.PaymentComment.Should().Contain(result.Value!.ConsolidatedEarningId!, "PaymentComment must include the consolidated CommissionEarning Id");
            row.PaymentComment.Should().Contain(result.Value.TokenTransactionId.ToString(), "PaymentComment must include the TokenTransaction Id");
        }
    }
}
