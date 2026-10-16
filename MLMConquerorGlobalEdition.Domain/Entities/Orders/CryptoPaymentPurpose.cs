namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Qué se cobra en cripto. El alta la confirma SignupAPI (activa la membresía y genera comisiones);
/// la compra de tokens la confirma Billing (emite los códigos).
/// </summary>
public enum CryptoPaymentPurpose
{
    Signup        = 1,
    TokenPurchase = 2
}
