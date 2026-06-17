using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutGatewaySettings;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.UpdatePayoutGatewaySetting;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Payouts;

public class PayoutGatewaySettingsHandlerTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static UpdatePayoutGatewaySettingHandler UpdateHandler(AppDbContext db)
    {
        var dt = new Mock<IDateTimeProvider>(); dt.Setup(d => d.Now).Returns(Now);
        var user = new Mock<ICurrentUserService>(); user.Setup(u => u.UserId).Returns("admin-1");
        return new UpdatePayoutGatewaySettingHandler(db, dt.Object, user.Object);
    }

    private static void SeedGateway(AppDbContext db, WalletType type, decimal min, bool active = true)
        => db.PaymentGateways.Add(new PaymentGatewayInfo
        {
            WalletType = type, MinimumPayoutAmount = min, IsActive = active,
            DisplayName = type.ToString(), Description = "d", Currency = "USD",
            AdminFee = 1.95m, AdminFeeKind = AdminFeeKind.Fixed,
            CreationDate = Now, CreatedBy = "seed"
        });

    private static UpdatePayoutGatewaySettingCommand Cmd(
        WalletType type, decimal min, bool active, decimal fee = 1.95m, string display = "Gateway")
        => new(type, display, fee, AdminFeeKind.Fixed, null, "USD", min, active);

    [Fact]
    public async Task Update_ExistingGateway_UpdatesInPlace()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedGateway(db, WalletType.eWallet, 20m);
        await db.SaveChangesAsync();

        var result = await UpdateHandler(db).Handle(
            Cmd(WalletType.eWallet, 35m, active: false, fee: 2.50m, display: "eWallet (I-Payout)"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.PaymentGateways.CountAsync()).Should().Be(1); // no duplicate row
        var saved = await db.PaymentGateways.SingleAsync(g => g.WalletType == WalletType.eWallet);
        saved.MinimumPayoutAmount.Should().Be(35m);
        saved.IsActive.Should().BeFalse();
        saved.AdminFee.Should().Be(2.50m);
        saved.DisplayName.Should().Be("eWallet (I-Payout)");
    }

    [Fact]
    public async Task Update_UnknownGateway_FailsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var result = await UpdateHandler(db).Handle(
            Cmd(WalletType.eWallet, 20m, active: true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("GATEWAY_NOT_FOUND");
    }

    [Fact]
    public async Task Get_ReturnsAllGateways()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedGateway(db, WalletType.eWallet, 20m);
        SeedGateway(db, WalletType.Volet, 50m, active: false);
        await db.SaveChangesAsync();

        var result = await new GetPayoutGatewaySettingsHandler(db).Handle(
            new GetPayoutGatewaySettingsQuery(), CancellationToken.None);

        result.Value!.Should().HaveCount(2);
        result.Value!.Single(g => g.WalletType == WalletType.Volet).IsActive.Should().BeFalse();
    }
}
