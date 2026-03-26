using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Signups.Services;
using MLMConquerorGlobalEdition.Signups.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Signups.Tests.Services;

public class SponsorBonusServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static async Task SeedProductAndOrderDetail(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        string orderId,
        string productId,
        int membershipLevelId)
    {
        await db.Products.AddAsync(new Product
        {
            Id = productId,
            Name = "Test Product",
            Description = "Test",
            ImageUrl = string.Empty,
            MonthlyFee = 99m,
            SetupFee = 0m,
            MembershipLevelId = membershipLevelId,
            QualificationPoins = 6,
            IsActive = true,
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId = orderId,
            ProductId = productId,
            Quantity = 1,
            UnitPrice = 99m,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCommissionType(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        int id,
        int levelNo,
        decimal? fixedAmount = null,
        decimal percentage = 0m,
        int paymentDelayDays = 7,
        int reverseId = 0)
    {
        await db.CommissionTypes.AddAsync(new CommissionType
        {
            Id = id,
            Name = $"Sponsor Bonus L{levelNo}",
            IsActive = true,
            IsSponsorBonus = true,
            LevelNo = levelNo,
            FixedAmount = fixedAmount,
            Percentage = percentage,
            PaymentDelayDays = paymentDelayDays,
            ReverseId = reverseId,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
    }

    // ─── ComputeAsync Tests ───────────────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_WhenSponsorMemberIdIsNull_DoesNotCreateEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        var service = new SponsorBonusService(db);

        await service.ComputeAsync(null, "MBR-001", "order-001", 99m, "seed", FixedNow, CancellationToken.None);

        db.CommissionEarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_WhenSponsorMemberIdIsEmpty_DoesNotCreateEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        var service = new SponsorBonusService(db);

        await service.ComputeAsync(string.Empty, "MBR-001", "order-001", 99m, "seed", FixedNow, CancellationToken.None);

        db.CommissionEarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_WhenMembershipLevelIsLifestyleAmbassador_DoesNotCreateEarning()
    {
        // MembershipLevelId = 1 (Lifestyle Ambassador) — no bonus
        await using var db = InMemoryDbHelper.Create();
        await SeedProductAndOrderDetail(db, "order-001", "prod-001", membershipLevelId: 1);
        await SeedCommissionType(db, 1, levelNo: 1, fixedAmount: 20m);

        var service = new SponsorBonusService(db);
        await service.ComputeAsync("AMB-SPONSOR", "MBR-001", "order-001", 99m, "seed", FixedNow, CancellationToken.None);

        db.CommissionEarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_WhenNoMatchingCommissionType_DoesNotCreateEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedProductAndOrderDetail(db, "order-001", "prod-001", membershipLevelId: 2);
        // No commission type seeded

        var service = new SponsorBonusService(db);
        await service.ComputeAsync("AMB-SPONSOR", "MBR-001", "order-001", 99m, "seed", FixedNow, CancellationToken.None);

        db.CommissionEarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_WhenIdempotentCall_DoesNotCreateDuplicate()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedProductAndOrderDetail(db, "order-001", "prod-001", membershipLevelId: 2);
        await SeedCommissionType(db, 10, levelNo: 2, fixedAmount: 40m);

        // Pre-existing earning for same order + type + beneficiary
        await db.CommissionEarnings.AddAsync(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-SPONSOR",
            SourceMemberId = "MBR-001",
            SourceOrderId = "order-001",
            CommissionTypeId = 10,
            Amount = 40m,
            Status = CommissionEarningStatus.Pending,
            EarnedDate = FixedNow,
            PaymentDate = FixedNow.AddDays(7),
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var service = new SponsorBonusService(db);
        await service.ComputeAsync("AMB-SPONSOR", "MBR-001", "order-001", 99m, "seed", FixedNow, CancellationToken.None);

        db.CommissionEarnings.Count().Should().Be(1);
    }

    [Fact]
    public async Task ComputeAsync_WhenUsingFixedAmount_CreatesEarningWithFixedAmount()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedProductAndOrderDetail(db, "order-001", "prod-001", membershipLevelId: 2);
        await SeedCommissionType(db, 10, levelNo: 2, fixedAmount: 40m, paymentDelayDays: 7);

        var service = new SponsorBonusService(db);
        await service.ComputeAsync("AMB-SPONSOR", "MBR-001", "order-001", 99m, "seed", FixedNow, CancellationToken.None);
        await db.SaveChangesAsync();

        var earning = db.CommissionEarnings.Single();
        earning.BeneficiaryMemberId.Should().Be("AMB-SPONSOR");
        earning.SourceMemberId.Should().Be("MBR-001");
        earning.Amount.Should().Be(40m);
        earning.Status.Should().Be(CommissionEarningStatus.Pending);
        earning.PaymentDate.Should().Be(FixedNow.AddDays(7));
        earning.PeriodDate.Should().Be(FixedNow.Date);
    }

    [Fact]
    public async Task ComputeAsync_WhenUsingPercentage_CreatesEarningWithCalculatedAmount()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedProductAndOrderDetail(db, "order-001", "prod-001", membershipLevelId: 2);
        await SeedCommissionType(db, 10, levelNo: 2, fixedAmount: null, percentage: 20m); // 20%

        var service = new SponsorBonusService(db);
        // order total = 100m, 20% = 20m
        await service.ComputeAsync("AMB-SPONSOR", "MBR-001", "order-001", 100m, "seed", FixedNow, CancellationToken.None);
        await db.SaveChangesAsync();

        var earning = db.CommissionEarnings.Single();
        earning.Amount.Should().Be(20m);
    }

    // ─── TryReverseAsync Tests ────────────────────────────────────────────────

    [Fact]
    public async Task TryReverseAsync_WhenNoSponsorBonusTypesConfigured_DoesNothing()
    {
        await using var db = InMemoryDbHelper.Create();
        var service = new SponsorBonusService(db);

        await service.TryReverseAsync("MBR-001", "order-001", null, FixedNow, "actor", CancellationToken.None);

        db.CommissionEarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task TryReverseAsync_WhenNoMatchingEarning_DoesNothing()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 10, levelNo: 2, fixedAmount: 40m);

        var service = new SponsorBonusService(db);
        await service.TryReverseAsync("MBR-001", "order-999", null, FixedNow, "actor", CancellationToken.None);

        db.CommissionEarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task TryReverseAsync_WhenPendingEarning_CancelsItInPlace()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 10, levelNo: 2, fixedAmount: 40m);

        var earning = new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-SPONSOR",
            SourceMemberId = "MBR-001",
            SourceOrderId = "order-001",
            CommissionTypeId = 10,
            Amount = 40m,
            Status = CommissionEarningStatus.Pending,
            EarnedDate = FixedNow,
            PaymentDate = FixedNow.AddDays(7),
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        };
        await db.CommissionEarnings.AddAsync(earning);
        await db.SaveChangesAsync();

        var service = new SponsorBonusService(db);
        await service.TryReverseAsync("MBR-001", "order-001", "Chargeback", FixedNow, "actor", CancellationToken.None);

        var updated = db.CommissionEarnings.Single();
        updated.Status.Should().Be(CommissionEarningStatus.Cancelled);
        updated.Notes.Should().Be("Chargeback");
    }

    [Fact]
    public async Task TryReverseAsync_WhenPaidEarningAndReverseTypeExists_CreatesNegativeEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        // Sponsor bonus type (ID=10) with reverse type (ID=11)
        await SeedCommissionType(db, 10, levelNo: 2, fixedAmount: 40m, reverseId: 11);
        await db.CommissionTypes.AddAsync(new CommissionType
        {
            Id = 11,
            Name = "Sponsor Bonus Reversal",
            IsActive = true,
            IsSponsorBonus = false,
            LevelNo = 2,
            PaymentDelayDays = 0,
            CreatedBy = "seed"
        });

        var originalEarning = new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-SPONSOR",
            SourceMemberId = "MBR-001",
            SourceOrderId = "order-001",
            CommissionTypeId = 10,
            Amount = 40m,
            Status = CommissionEarningStatus.Paid, // already paid
            EarnedDate = FixedNow.AddDays(-10),
            PaymentDate = FixedNow.AddDays(-3),
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        };
        await db.CommissionEarnings.AddAsync(originalEarning);
        await db.SaveChangesAsync();

        var service = new SponsorBonusService(db);
        await service.TryReverseAsync("MBR-001", "order-001", "Cancelled", FixedNow, "actor", CancellationToken.None);
        await db.SaveChangesAsync();

        db.CommissionEarnings.Count().Should().Be(2);

        var reversal = db.CommissionEarnings.First(e => e.CommissionTypeId == 11);
        reversal.Amount.Should().Be(-40m);
        reversal.Status.Should().Be(CommissionEarningStatus.Pending);
        reversal.BeneficiaryMemberId.Should().Be("AMB-SPONSOR");
        reversal.Notes.Should().Contain("Reversal of sponsor bonus");
    }

    [Fact]
    public async Task TryReverseAsync_WhenPaidEarningAndReversalAlreadyExists_IsIdempotent()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 10, levelNo: 2, fixedAmount: 40m, reverseId: 11);
        await db.CommissionTypes.AddAsync(new CommissionType
        {
            Id = 11, Name = "Reversal", IsActive = true, IsSponsorBonus = false,
            LevelNo = 2, PaymentDelayDays = 0, CreatedBy = "seed"
        });

        // Original paid earning
        var original = new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-SPONSOR",
            SourceMemberId = "MBR-001",
            SourceOrderId = "order-001",
            CommissionTypeId = 10,
            Amount = 40m,
            Status = CommissionEarningStatus.Paid,
            EarnedDate = FixedNow,
            PaymentDate = FixedNow,
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        };
        // Already-existing reversal
        var existingReversal = new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-SPONSOR",
            SourceMemberId = "MBR-001",
            SourceOrderId = "order-001",
            CommissionTypeId = 11,
            Amount = -40m,
            Status = CommissionEarningStatus.Pending,
            EarnedDate = FixedNow,
            PaymentDate = FixedNow,
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        };
        await db.CommissionEarnings.AddRangeAsync(original, existingReversal);
        await db.SaveChangesAsync();

        var service = new SponsorBonusService(db);
        await service.TryReverseAsync("MBR-001", "order-001", null, FixedNow, "actor", CancellationToken.None);

        // Should still only have 2 earnings (no new reversal created)
        db.CommissionEarnings.Count().Should().Be(2);
    }

    [Fact]
    public async Task TryReverseAsync_WhenAlreadyCancelledEarning_DoesNothing()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 10, levelNo: 2, fixedAmount: 40m);

        // Earning already cancelled
        await db.CommissionEarnings.AddAsync(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-SPONSOR",
            SourceMemberId = "MBR-001",
            SourceOrderId = "order-001",
            CommissionTypeId = 10,
            Amount = 40m,
            Status = CommissionEarningStatus.Cancelled,
            EarnedDate = FixedNow,
            PaymentDate = FixedNow,
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var service = new SponsorBonusService(db);
        await service.TryReverseAsync("MBR-001", "order-001", null, FixedNow, "actor", CancellationToken.None);

        // No new earnings created
        db.CommissionEarnings.Count().Should().Be(1);
        db.CommissionEarnings.Single().Status.Should().Be(CommissionEarningStatus.Cancelled);
    }
}
