namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

/// <summary>Lo que aporta quien confirma a mano que un cobro en cripto entró.</summary>
public class ConfirmCryptoPaymentRequest
{
    /// <summary>Hash o identificador de la transferencia en la cadena. Obligatorio.</summary>
    public string CryptoTransactionId { get; set; } = string.Empty;

    /// <summary>Nota opcional: red usada, importe recibido, incidencias.</summary>
    public string? Notes { get; set; }
}
