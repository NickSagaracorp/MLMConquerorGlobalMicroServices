using FluentAssertions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPendingPayouts;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using Xunit;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Payouts;

public class GetPendingPayoutsHandlerTests
{
    private static readonly DateTime ProcessDate = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static void SeedMember(AppDbContext db, string memberId, string first, string last)
        => db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = memberId, FirstName = first, LastName = last,
            CreationDate = ProcessDate, CreatedBy = "seed", LastUpdateDate = ProcessDate
        });

    private static void SeedWallet(AppDbContext db, string memberId, WalletType type)
        => db.Wallets.Add(new MemberProfilesWallet
        {
            MemberId = memberId, WalletType = type, Status = WalletStatus.Approved,
            AccountIdentifier = "acct", IsPreferred = true, IsDeleted = false,
            CreationDate = ProcessDate, CreatedBy = "seed", LastUpdateDate = ProcessDate
        });

    private static void SeedSetting(AppDbContext db, WalletType type, decimal min, bool active = true)
        => db.PaymentGateways.Add(new PaymentGatewayInfo
        {
            WalletType = type, MinimumPayoutAmount = min, IsActive = active,
            DisplayName = type.ToString(), Description = "d", Currency = "USD",
            AdminFee = 0m, AdminFeeKind = AdminFeeKind.Fixed,
            CreationDate = ProcessDate, CreatedBy = "seed"
        });

    private static CommissionEarning SeedEarning(AppDbContext db, string memberId, decimal amt, DateTime due, int typeId = 0)
    {
        var e = new CommissionEarning
        {
            BeneficiaryMemberId = memberId, Amount = amt, Status = CommissionEarningStatus.Pending,
            PaymentDate = due, CommissionTypeId = typeId, IsDeleted = false,
            CreationDate = ProcessDate, CreatedBy = "seed"
        };
        db.CommissionEarnings.Add(e);
        return e;
    }

    [Fact]
    public async Task Threshold_AtOrAboveMinimum_Eligible()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        SeedEarning(db, "AMB-1", 20m, ProcessDate.AddDays(-1));
        await db.SaveChangesAsync();

        var result = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().PendingAmount.Should().Be(20m);
        result.Value.Items.Single().FullName.Should().Be("Ana Diaz");
    }

    [Fact]
    public async Task Threshold_BelowMinimum_NotEligible()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        SeedEarning(db, "AMB-1", 19.99m, ProcessDate.AddDays(-1));
        await db.SaveChangesAsync();

        var result = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task AmountCountsOnlyEarningsDueOnOrBeforeProcessDate()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        SeedEarning(db, "AMB-1", 30m, ProcessDate.AddDays(-1)); // due
        SeedEarning(db, "AMB-1", 99m, ProcessDate.AddDays(5));  // not due
        await db.SaveChangesAsync();

        var result = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate), CancellationToken.None);

        result.Value!.Items.Single().PendingAmount.Should().Be(30m);
    }

    [Fact]
    public async Task ReservedEarnings_AreExcluded()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        var e = SeedEarning(db, "AMB-1", 50m, ProcessDate.AddDays(-1));
        await db.SaveChangesAsync();

        var attempt = new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "acct",
            AmountUsd = 50m, ProcessDateUtc = ProcessDate, Outcome = PayoutOutcome.Pending,
            AttemptedAtUtc = ProcessDate, EarningsCount = 1, CreationDate = ProcessDate, CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(attempt);
        await db.SaveChangesAsync();
        db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
        {
            PayoutAttemptId = attempt.Id, CommissionEarningId = e.Id, Amount = 50m,
            CreationDate = ProcessDate, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var result = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(0); // earning reserved by a non-failed attempt
    }

    [Fact]
    public async Task FiltersByWalletType()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedMember(db, "AMB-1", "Ana", "Diaz");   SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedMember(db, "AMB-2", "Bob", "Smith");  SeedWallet(db, "AMB-2", WalletType.Volet);
        SeedSetting(db, WalletType.eWallet, 20m);  SeedSetting(db, WalletType.Volet, 20m);
        SeedEarning(db, "AMB-1", 25m, ProcessDate.AddDays(-1));
        SeedEarning(db, "AMB-2", 25m, ProcessDate.AddDays(-1));
        await db.SaveChangesAsync();

        var result = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate, WalletType: WalletType.Volet), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().MemberId.Should().Be("AMB-2");
        result.Value.Items.Single().WalletType.Should().Be(WalletType.Volet);
    }

    [Fact]
    public async Task FiltersByCommissionTypeId()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        SeedEarning(db, "AMB-1", 25m,  ProcessDate.AddDays(-1), typeId: 5);
        SeedEarning(db, "AMB-1", 100m, ProcessDate.AddDays(-1), typeId: 7);
        await db.SaveChangesAsync();

        // Unfiltered: both commission types summed into the member's pending total.
        var all = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate), CancellationToken.None);
        all.Value!.Items.Single().PendingAmount.Should().Be(125m);

        // Filtered to type 5 — only that type's earnings count.
        var t5 = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate, CommissionTypeId: 5), CancellationToken.None);
        t5.Value!.Items.Single().PendingAmount.Should().Be(25m);

        // Filtered to type 7.
        var t7 = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate, CommissionTypeId: 7), CancellationToken.None);
        t7.Value!.Items.Single().PendingAmount.Should().Be(100m);
    }

    [Fact]
    public async Task CommissionTypeFilter_DropsMemberWhenThatTypeAloneIsBelowThreshold()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        SeedEarning(db, "AMB-1", 5m,   ProcessDate.AddDays(-1), typeId: 5);  // type 5 alone < threshold
        SeedEarning(db, "AMB-1", 100m, ProcessDate.AddDays(-1), typeId: 7);
        await db.SaveChangesAsync();

        var t5 = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate, CommissionTypeId: 5), CancellationToken.None);

        t5.Value!.TotalCount.Should().Be(0); // 5 < 20 once the filter isolates type 5
    }

    [Fact]
    public async Task NoActiveSettingForWalletType_NotEligible()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m, active: false); // inactive
        SeedEarning(db, "AMB-1", 50m, ProcessDate.AddDays(-1));
        await db.SaveChangesAsync();

        var result = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task LastAttemptOutcomeAndError_AreSurfaced()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        SeedEarning(db, "AMB-1", 50m, ProcessDate.AddDays(-1));
        // a prior FAILED attempt (does not reserve the earning, since Outcome == Failed)
        db.PayoutAttempts.Add(new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "acct",
            AmountUsd = 50m, ProcessDateUtc = ProcessDate, Outcome = PayoutOutcome.Failed,
            GatewayErrorMessage = "Gateway declined", AttemptedAtUtc = ProcessDate.AddHours(-1),
            EarningsCount = 1, CreationDate = ProcessDate, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var result = await new GetPendingPayoutsHandler(db).Handle(
            new GetPendingPayoutsQuery(ProcessDate), CancellationToken.None);

        var row = result.Value!.Items.Single();
        row.LastAttemptOutcome.Should().Be(PayoutOutcome.Failed);
        row.LastAttemptError.Should().Be("Gateway declined");
    }
}
