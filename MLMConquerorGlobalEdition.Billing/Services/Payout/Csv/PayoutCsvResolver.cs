using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;

public class PayoutCsvResolver : IPayoutCsvResolver
{
    private readonly IEnumerable<IPayoutCsvFormatter> _formatters;
    private readonly IEnumerable<IPayoutResultCsvParser> _parsers;

    public PayoutCsvResolver(IEnumerable<IPayoutCsvFormatter> formatters, IEnumerable<IPayoutResultCsvParser> parsers)
    {
        _formatters = formatters;
        _parsers = parsers;
    }

    public Result<IPayoutCsvFormatter> ResolveFormatter(WalletType walletType)
    {
        var f = _formatters.FirstOrDefault(x => x.GatewayType == walletType);
        return f is null
            ? Result<IPayoutCsvFormatter>.Failure("PAYOUT_CSV_GATEWAY_NOT_SUPPORTED",
                $"No CSV formatter for {walletType}")
            : Result<IPayoutCsvFormatter>.Success(f);
    }

    public Result<IPayoutResultCsvParser> ResolveParser(WalletType walletType)
    {
        var p = _parsers.FirstOrDefault(x => x.GatewayType == walletType);
        return p is null
            ? Result<IPayoutResultCsvParser>.Failure("PAYOUT_CSV_GATEWAY_NOT_SUPPORTED",
                $"No CSV parser for {walletType}")
            : Result<IPayoutResultCsvParser>.Success(p);
    }
}
