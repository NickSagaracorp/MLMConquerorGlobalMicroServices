using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public class PaymentHistory : AuditChangesStringKey
{
    /// <summary>Longitud de PaymentHistories.OrderId: la de Orders.Id.</summary>
    public const int OrderIdMaxLength = 450;

    /// <summary>
    /// Longitud de PaymentHistories.MemberId. Más holgada que MemberProfiles.MemberId (20):
    /// sin FK, el webhook guarda lo que mande la pasarela, y 100 da margen sin admitir basura.
    /// </summary>
    public const int MemberIdMaxLength = 100;

    /// <summary>Longitud de PaymentHistories.GatewayTransactionId. Hoy miden 36 como mucho.</summary>
    public const int GatewayTransactionIdMaxLength = 200;

    /// <summary>
    /// Tope de GatewayName (el proveedor de la URL del webhook). La columna sigue en
    /// nvarchar(max) (acotarla pide otra migración); el tope lo aplica el webhook al entrar.
    /// </summary>
    public const int GatewayNameMaxLength = 100;

    public string OrderId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string GatewayName { get; set; } = string.Empty;
    public string? GatewayTransactionId { get; set; }
    public PaymentHistoryTransactionStatus TransactionStatus { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
