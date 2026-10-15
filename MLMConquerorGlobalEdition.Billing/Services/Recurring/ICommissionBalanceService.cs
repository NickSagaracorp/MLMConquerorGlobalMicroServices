using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

/// <summary>
/// Calculates and applies the commission-balance-first funding rule.
/// Reads DailyResidualConsolidationMinimum from GlobalParameters.
/// </summary>
public interface ICommissionBalanceService
{
    /// <summary>Returns the member's available commission balance breakdown.</summary>
    Task<CommissionBalanceSummary> GetAvailableAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// Consolidates all pending DailyResidualEarning rows for the member
    /// (if sum >= DailyResidualConsolidationMinimum) into a single CommissionEarning credit row.
    /// Returns the consolidated CommissionEarning Id, or null if below the minimum.
    /// </summary>
    Task<string?> ConsolidateDailyResidualAsync(string memberId, string actor, CancellationToken ct = default);

    /// <summary>
    /// Funds a recurring bill entirely from the member's commission balance:
    /// 1. Optionally consolidates daily residual.
    /// 2. Creates a negative CommissionEarning debit.
    /// 3. Issues a TokenTransaction (Quantity 1) of the correct token type.
    /// 4. Bumps the TokenBalance.
    /// 5. Marks the provided Order as Paid and creates a PaymentHistory row.
    /// Returns the IDs of the created rows for the RecurringBillingAttempt log.
    /// </summary>
    Task<Result<CommissionFundResult>> FundWithCommissionBalanceAsync(
        string memberId,
        RecurringBillingPlan plan,
        int? tokenTypeIdOverride,
        decimal amountDue,
        string orderId,
        string productId,
        string actor,
        CancellationToken ct = default);

    /// <summary>
    /// Saldo con el que el miembro puede pagar AHORA: comisiones pendientes ya pagables
    /// (PaymentDate &lt;= Now) no reservadas por un pago en curso, más el residual diario si alcanza
    /// el mínimo de consolidación. Más estricto que <see cref="GetAvailableAsync"/>.
    /// </summary>
    Task<decimal> GetSpendableAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// Paga una compra de tokens con comisiones: consolida el residual si toca, escribe el débito
    /// negativo ligado al pedido y el PaymentHistory. No emite ni toca el pedido: eso lo hace quien
    /// compra en la misma transacción. Devuelve el id del PaymentHistory.
    /// </summary>
    Task<Result<string>> FundTokenPurchaseAsync(
        string memberId, decimal amount, string orderId, string actor, CancellationToken ct = default);
}

public class CommissionBalanceSummary
{
    public decimal GeneralPending { get; init; }
    public decimal DailyResidualPending { get; init; }
    public decimal EligibleDailyResidual { get; init; }
    public decimal Available { get; init; }
    public decimal ConsolidationMinimum { get; init; }
}

public class CommissionFundResult
{
    public string CommissionDebitEarningId { get; init; } = string.Empty;
    public long TokenTransactionId { get; init; }
    public string PaymentHistoryId { get; init; } = string.Empty;
    public string? ConsolidatedEarningId { get; init; }
}
