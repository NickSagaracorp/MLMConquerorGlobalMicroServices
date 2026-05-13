using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.CommissionEngine.Jobs;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class DailyResidualConsolidationJobTests
{
    private static readonly DateTime FixedNow = new(2026, 2, 17, 4, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> BuildClock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static CommissionType BuildResidualType() => new()
    {
        Id                   = 1,
        CommissionCategoryId = 1,
        Name                 = "Daily Residual",
        ResidualBased        = true,
        IsPaidOnSignup       = false,
        IsActive             = true,
        CreatedBy            = "seed",
        CreationDate         = FixedNow,
        LastUpdateDate       = FixedNow
    };

    private static DailyResidualEarning BuildEarning(
        string memberId, decimal amount,
        CommissionEarningStatus status = CommissionEarningStatus.Pending) => new()
    {
        BeneficiaryMemberId = memberId,
        Amount              = amount,
        EarnedDate          = FixedNow.AddDays(-1),
        Status              = status,
        CreatedBy           = "test",
        CreationDate        = FixedNow.AddDays(-1)
    };

    private DailyResidualConsolidationJob BuildJob(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        IDateTimeProvider? clock = null)
        => new(db, clock ?? BuildClock().Object, NullLogger<DailyResidualConsolidationJob>.Instance);

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenMemberHasRowsAboveMinimum_ConsolidatesAndMarksPaid()
    {
        var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BuildResidualType());
        db.DailyResidualEarnings.Add(BuildEarning("mem-1", 60m));
        db.DailyResidualEarnings.Add(BuildEarning("mem-1", 80m));
        await db.SaveChangesAsync();

        await BuildJob(db).ExecuteAsync();

        // One consolidated CommissionEarning should have been created
        var earning = db.CommissionEarnings.FirstOrDefault(e => e.BeneficiaryMemberId == "mem-1");
        earning.Should().NotBeNull();
        earning!.Amount.Should().Be(140m);
        earning.Status.Should().Be(CommissionEarningStatus.Pending);

        // All DailyResidualEarning rows should be Paid
        var paidCount = db.DailyResidualEarnings
            .Count(e => e.BeneficiaryMemberId == "mem-1" && e.Status == CommissionEarningStatus.Paid);
        paidCount.Should().Be(2);

        // Rows point to consolidated earning
        var allLinked = db.DailyResidualEarnings
            .Where(e => e.BeneficiaryMemberId == "mem-1")
            .All(e => e.ConsolidatedIntoCommissionEarningId == earning.Id);
        allLinked.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMemberHasRowsAboveMinimum_SetsPaymentTrackingFields()
    {
        var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BuildResidualType());
        db.DailyResidualEarnings.Add(BuildEarning("mem-pt", 60m));
        db.DailyResidualEarnings.Add(BuildEarning("mem-pt", 80m));
        await db.SaveChangesAsync();

        await BuildJob(db).ExecuteAsync();

        var earning = db.CommissionEarnings.First(e => e.BeneficiaryMemberId == "mem-pt");
        var paidRows = db.DailyResidualEarnings
            .Where(e => e.BeneficiaryMemberId == "mem-pt")
            .ToList();

        foreach (var row in paidRows)
        {
            row.PaymentDate.Should().Be(FixedNow, "PaymentDate must be set to IDateTimeProvider.Now");
            row.CommentedBy.Should().Be("weekly-consolidation", "CommentedBy must identify the weekly job actor");
            row.PaymentComment.Should().Contain(earning.Id, "PaymentComment must reference the consolidated CommissionEarning Id");
            row.PaymentComment.Should().Contain("weekly daily-residual consolidation job");
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenMemberHasRowsBelowMinimum_LeavesRowsPending()
    {
        var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BuildResidualType());
        db.DailyResidualEarnings.Add(BuildEarning("mem-2", 30m)); // below default 100
        db.DailyResidualEarnings.Add(BuildEarning("mem-2", 40m)); // sum = 70, below 100
        await db.SaveChangesAsync();

        await BuildJob(db).ExecuteAsync();

        var earningCount = db.CommissionEarnings.Count(e => e.BeneficiaryMemberId == "mem-2");
        earningCount.Should().Be(0); // nothing consolidated

        var pendingCount = db.DailyResidualEarnings
            .Count(e => e.BeneficiaryMemberId == "mem-2" && e.Status == CommissionEarningStatus.Pending);
        pendingCount.Should().Be(2); // still pending

        // Skipped rows must NOT have payment tracking fields set
        var skipped = db.DailyResidualEarnings
            .Where(e => e.BeneficiaryMemberId == "mem-2")
            .ToList();

        skipped.All(r => r.PaymentDate is null).Should().BeTrue();
        skipped.All(r => r.CommentedBy is null).Should().BeTrue();
        skipped.All(r => r.PaymentComment is null).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_UsesCustomMinimumFromGlobalParameters()
    {
        var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BuildResidualType());
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "DailyResidualConsolidationMinimum", Value = "50",
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        db.DailyResidualEarnings.Add(BuildEarning("mem-3", 60m)); // above custom minimum of 50
        await db.SaveChangesAsync();

        await BuildJob(db).ExecuteAsync();

        var earning = db.CommissionEarnings.FirstOrDefault(e => e.BeneficiaryMemberId == "mem-3");
        earning.Should().NotBeNull();
        earning!.Amount.Should().Be(60m);
    }

    [Fact]
    public async Task ExecuteAsync_IsIdempotent_SkipsAlreadyPaidRows()
    {
        var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BuildResidualType());
        // Already-Paid rows from a previous job run
        db.DailyResidualEarnings.Add(BuildEarning("mem-4", 150m, CommissionEarningStatus.Paid));
        // New pending row below minimum
        db.DailyResidualEarnings.Add(BuildEarning("mem-4", 20m, CommissionEarningStatus.Pending));
        await db.SaveChangesAsync();

        await BuildJob(db).ExecuteAsync();

        // Only pending rows count; 20m < 100m so nothing new should be created
        var earningCount = db.CommissionEarnings.Count(e => e.BeneficiaryMemberId == "mem-4");
        earningCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleMembers_ConsolidatesEachIndependently()
    {
        var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BuildResidualType());

        // mem-5: above minimum
        db.DailyResidualEarnings.Add(BuildEarning("mem-5", 120m));

        // mem-6: below minimum
        db.DailyResidualEarnings.Add(BuildEarning("mem-6", 40m));

        await db.SaveChangesAsync();

        await BuildJob(db).ExecuteAsync();

        // mem-5 consolidated
        db.CommissionEarnings.Count(e => e.BeneficiaryMemberId == "mem-5").Should().Be(1);

        // mem-6 not consolidated
        db.CommissionEarnings.Count(e => e.BeneficiaryMemberId == "mem-6").Should().Be(0);
        db.DailyResidualEarnings
            .First(e => e.BeneficiaryMemberId == "mem-6").Status
            .Should().Be(CommissionEarningStatus.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_UsesIDateTimeProviderForPaymentDate_NotHardcodedClock()
    {
        // Arrange: wire a specific clock time and verify it propagates to PaymentDate
        var specificTime = new DateTime(2026, 3, 10, 4, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(d => d.Now).Returns(specificTime);

        var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BuildResidualType());
        db.DailyResidualEarnings.Add(BuildEarning("mem-dt", 150m));
        await db.SaveChangesAsync();

        await BuildJob(db, clock.Object).ExecuteAsync();

        var row = db.DailyResidualEarnings.First(e => e.BeneficiaryMemberId == "mem-dt");
        row.PaymentDate.Should().Be(specificTime);
    }
}
