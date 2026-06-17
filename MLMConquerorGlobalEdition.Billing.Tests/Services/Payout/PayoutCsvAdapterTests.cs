using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;
using MLMConquerorGlobalEdition.Domain.Enums;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

public class PayoutCsvAdapterTests
{
    [Fact]
    public void EWallet_FormatExport_HasHeaderAndRows()
    {
        var csv = new EWalletPayoutCsvAdapter().FormatExport(new[]
        {
            new PayoutCsvRow(101, "AMB-1", "ana@x.com", 50m),
            new PayoutCsvRow(102, "AMB-2", "bob@x.com", 25.5m)
        });
        var lines = csv.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        lines[0].Should().Contain("Reference"); // header
        lines.Should().HaveCount(3); // header + 2 rows
        lines[1].Should().Contain("101").And.Contain("ana@x.com").And.Contain("50.00");
    }

    [Fact]
    public void EWallet_ParseResults_RoundTripsReferenceAndOutcome()
    {
        var content = "Reference,Status,TransactionId,ErrorCode,ErrorMessage\n" +
                      "101,SUCCESS,txn-aaa,,\n" +
                      "102,FAILED,,E12,Account closed\n";
        var rows = new EWalletPayoutCsvAdapter().ParseResults(content);
        rows.Should().HaveCount(2);
        rows[0].PayoutAttemptId.Should().Be(101);
        rows[0].Success.Should().BeTrue();
        rows[0].GatewayTransactionId.Should().Be("txn-aaa");
        rows[1].Success.Should().BeFalse();
        rows[1].ErrorCode.Should().Be("E12");
        rows[1].ErrorMessage.Should().Be("Account closed");
    }

    [Fact]
    public void Resolver_ReturnsAdapterByWalletType_AndFailsForUnsupported()
    {
        var resolver = new PayoutCsvResolver(
            new IPayoutCsvFormatter[] { new EWalletPayoutCsvAdapter(), new VoletPayoutCsvAdapter() },
            new IPayoutResultCsvParser[] { new EWalletPayoutCsvAdapter(), new VoletPayoutCsvAdapter() });

        resolver.ResolveFormatter(WalletType.Volet).Value!.GatewayType.Should().Be(WalletType.Volet);
        resolver.ResolveParser(WalletType.eWallet).Value!.GatewayType.Should().Be(WalletType.eWallet);
        resolver.ResolveFormatter(WalletType.Crypto).IsSuccess.Should().BeFalse();
    }
}
