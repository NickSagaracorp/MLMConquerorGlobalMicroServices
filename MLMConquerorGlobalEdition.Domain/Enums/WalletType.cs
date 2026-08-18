namespace MLMConquerorGlobalEdition.Domain.Enums;

public enum WalletType
{
    /// <summary>
    /// RETIRED (2026-08). Dwolla ya no se ofrece: su fila de PaymentGatewayInfo está
    /// IsActive = false y no se pueden dar de alta wallets nuevas. El valor se CONSERVA
    /// a propósito para que WalletHistories y WalletApiLogs históricos sigan diciendo
    /// "Dwolla" — reusarlo para otro gateway corrompería la auditoría.
    /// Las wallets vivas se migraron a <see cref="Volet"/> (ambos son email-based).
    /// </summary>
    Dwolla = 1,
    Propay = 2,
    WireTransferCanada = 3,

    /// <summary>
    /// I-Payout. OJO: el AccountIdentifier es el UserName/UserID que asigna el gateway
    /// tras registrar la cuenta, NO un email.
    /// </summary>
    eWallet = 4,
    Paypal = 5,

    /// <summary>
    /// Volet, antes AdvCash (la empresa se renombró). Email-based.
    /// Este es el único valor canónico: el antiguo <c>Advancash = 10</c> se fusionó acá.
    /// Coincide con el gateway id 6 de MWRLife.
    /// </summary>
    Volet = 6,
    Yestransfer = 7,
    Crypto = 9,

    /// <summary>
    /// PayQuicker. Programa "hosted portal": el destinatario se identifica por
    /// programUserId (nuestro MemberId) + email, no por un token de cuenta.
    /// </summary>
    PayQuicker = 11
}
