namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

/// <summary>Resultado de confirmar un cobro en cripto.</summary>
public class ConfirmCryptoPaymentResponse
{
    public string OrderId  { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;

    /// <summary>Estado del miembro tras la confirmación. Debe ser Active.</summary>
    public string MemberStatus { get; set; } = string.Empty;

    public string   CryptoTransactionId { get; set; } = string.Empty;
    public string   ConfirmedByEmail    { get; set; } = string.Empty;
    public DateTime ConfirmedAt         { get; set; }
}
