using FluentAssertions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutStats;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using Xunit;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Payouts;

public class GetPayoutStatsHandlerTests
{
    private static readonly DateTime ProcessDate = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static void SeedWallet(AppDbContext db, string memberId, WalletType type)
        => db.Wallets.Add(new MemberProfilesWallet
        {
            MemberId = memberId, WalletType = type, Status = WalletStatus.Approved,
            AccountIdentifier = "acct", IsPreferred = true, IsDeleted = false,
            CreationDate = ProcessDate, CreatedBy = "seed", LastUpdateDate = ProcessDate
        });

    private static void SeedSetting(AppDbContext db, WalletType type, decimal min)
        => db.PaymentGateways.Add(new PaymentGatewayInfo
        {
            WalletType = type, MinimumPayoutAmount = min, IsActive = true,
            DisplayName = type.ToString(), Description = "d", Currency = "USD",
            AdminFee = 0m, AdminFeeKind = AdminFeeKind.Fixed,
            CreationDate = ProcessDate, CreatedBy = "seed"
        });

    private static void SeedEarning(AppDbContext db, string memberId, decimal amt, int typeId = 0)
        => db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = memberId, Amount = amt, Status = CommissionEarningStatus.Pending,
            PaymentDate = ProcessDate.AddDays(-1), CommissionTypeId = typeId, IsDeleted = false,
            CreationDate = ProcessDate, CreatedBy = "seed"
        });

    [Fact]
    public async Task PendingTotalPerGateway_SumsEligibleCandidates()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedWallet(db, "AMB-1", WalletType.eWallet); SeedWallet(db, "AMB-2", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        SeedEarning(db, "AMB-1", 30m); SeedEarning(db, "AMB-2", 40m);
        await db.SaveChangesAsync();

        var result = await new GetPayoutStatsHandler(db).Handle(new GetPayoutStatsQuery(ProcessDate), CancellationToken.None);

        var ew = result.Value!.Gateways.Single(g => g.WalletType == WalletType.eWallet);
        ew.PendingTotal.Should().Be(70m);
        ew.PendingCount.Should().Be(2);
    }

    [Fact]
    public async Task PendingTotal_RespectsCommissionTypeFilter()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        SeedEarning(db, "AMB-1", 30m, typeId: 5);
        SeedEarning(db, "AMB-1", 40m, typeId: 7);
        await db.SaveChangesAsync();

        // Unfiltered → 70 across both types.
        var all = await new GetPayoutStatsHandler(db).Handle(new GetPayoutStatsQuery(ProcessDate), CancellationToken.None);
        all.Value!.Gateways.Single(g => g.WalletType == WalletType.eWallet).PendingTotal.Should().Be(70m);

        // Filtered to type 7 → only 40 counts toward the gateway's pending.
        var t7 = await new GetPayoutStatsHandler(db).Handle(
            new GetPayoutStatsQuery(ProcessDate, CommissionTypeId: 7), CancellationToken.None);
        t7.Value!.Gateways.Single(g => g.WalletType == WalletType.eWallet).PendingTotal.Should().Be(40m);
    }

    [Fact]
    public async Task PaidTotal_ReflectsSuccessfulAttemptsCompletedOnProcessDay()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        db.PayoutAttempts.Add(new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "acct",
            AmountUsd = 100m, ProcessDateUtc = ProcessDate, Outcome = PayoutOutcome.Success,
            AttemptedAtUtc = ProcessDate, CompletedAtUtc = ProcessDate, EarningsCount = 1,
            CreationDate = ProcessDate, CreatedBy = "seed"
        });
        // a success on a different day should NOT count
        db.PayoutAttempts.Add(new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "acct",
            AmountUsd = 55m, ProcessDateUtc = ProcessDate, Outcome = PayoutOutcome.Success,
            AttemptedAtUtc = ProcessDate.AddDays(-3), CompletedAtUtc = ProcessDate.AddDays(-3), EarningsCount = 1,
            CreationDate = ProcessDate, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var result = await new GetPayoutStatsHandler(db).Handle(new GetPayoutStatsQuery(ProcessDate), CancellationToken.None);

        result.Value!.Gateways.Single(g => g.WalletType == WalletType.eWallet).PaidTotal.Should().Be(100m);
    }

    [Fact]
    public async Task FailedAttempts_DoNotCountAsPaid()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedWallet(db, "AMB-1", WalletType.eWallet);
        SeedSetting(db, WalletType.eWallet, 20m);
        db.PayoutAttempts.Add(new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "acct",
            AmountUsd = 100m, ProcessDateUtc = ProcessDate, Outcome = PayoutOutcome.Failed,
            AttemptedAtUtc = ProcessDate, CompletedAtUtc = ProcessDate, EarningsCount = 1,
            CreationDate = ProcessDate, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var result = await new GetPayoutStatsHandler(db).Handle(new GetPayoutStatsQuery(ProcessDate), CancellationToken.None);

        var ew = result.Value!.Gateways.SingleOrDefault(g => g.WalletType == WalletType.eWallet);
        (ew?.PaidTotal ?? 0m).Should().Be(0m);
    }
}
