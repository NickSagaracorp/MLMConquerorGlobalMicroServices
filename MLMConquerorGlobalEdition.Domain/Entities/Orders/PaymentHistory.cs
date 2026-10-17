using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public class PaymentHistory : AuditChangesStringKey
{
    /// <summary>Longitud de PaymentHistories.OrderId: la de Orders.Id (Plan M, M1 y M7).</summary>
    public const int OrderIdMaxLength = 450;

    /// <summary>
    /// Longitud de PaymentHistories.MemberId (Plan M, M7). Más holgada que MemberProfiles.MemberId (20):
    /// sin FK, el webhook guarda lo que mande la pasarela, y 100 da margen sin admitir basura.
    /// </summary>
    public const int MemberIdMaxLength = 100;

    /// <summary>Longitud de PaymentHistories.GatewayTransactionId (Plan M, M7). Hoy miden 36 como mucho.</summary>
    public const int GatewayTransactionIdMaxLength = 200;

    public string OrderId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string GatewayName { get; set; } = string.Empty;
    public string? GatewayTransactionId { get; set; }
    public PaymentHistoryTransactionStatus TransactionStatus { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
