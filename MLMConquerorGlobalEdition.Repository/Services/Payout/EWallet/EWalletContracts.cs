using System.Text.Json.Serialization;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;

// Contratos de i-Payout (eWallet).
//
// La API NO es REST: hay UN endpoint y la operación se elige con el campo "fn" del payload,
// estilo RPC. La autenticación va en el cuerpo de cada request (MerchantGUID +
// MerchantPassword), no en headers.
//
// Todas las respuestas vienen envueltas: { "response": { "m_Code": 0, "m_Text": "..." }, ... }
// donde m_Code >= 0 es éxito (NO_ERROR = 0) y m_Code < 0 es error, con el detalle en m_Text.

/// <summary>Nombres de función de i-Payout, tomados de la integración de MWRLife.</summary>
public static class EWalletFunctions
{
    /// <summary>Alta básica de usuario.</summary>
    public const string CreateUser = "eWallet_CreateUser";

    /// <summary>Alta con datos KYC (DOB, documento, ocupación, moneda, idioma).</summary>
    public const string RegisterUser = "eWallet_RegisterUser";

    /// <summary>Confirma si un UserName ya existe en el gateway.</summary>
    public const string CheckIfUserNameExists = "eWallet_CheckIfUserNameExists";

    /// <summary>Saldo del usuario en una moneda.</summary>
    public const string GetCurrencyBalance = "eWallet_GetCurrencyBalance";

    /// <summary>Acredita fondos en una o varias cuentas — es el disburse.</summary>
    public const string Load = "eWallet_Load";
}

/// <summary>Envoltorio común de respuesta.</summary>
public class EWalletResponseEnvelope
{
    [JsonPropertyName("response")] public EWalletStatus? Response { get; set; }
}

public class EWalletStatus
{
    /// <summary>&gt;= 0 es éxito (0 = NO_ERROR). Negativo es error.</summary>
    [JsonPropertyName("m_Code")] public int     Code { get; set; }
    [JsonPropertyName("m_Text")] public string? Text { get; set; }
}

public sealed class EWalletBalanceResponse : EWalletResponseEnvelope
{
    [JsonPropertyName("Balance")] public decimal Balance { get; set; }
}

public sealed class EWalletCreateUserResponse : EWalletResponseEnvelope
{
    /// <summary>
    /// i-Payout devuelve acá el identificador de la cuenta creada. Es el valor que hay que
    /// persistir como AccountIdentifier del wallet: toda la operatoria posterior
    /// (balance, load) va contra ese UserName, NO contra el email.
    /// </summary>
    [JsonPropertyName("TransactionRefID")] public string? TransactionRefId { get; set; }
}

public sealed class EWalletLoadResponse : EWalletResponseEnvelope
{
    [JsonPropertyName("arrAccountsResponse")] public List<EWalletLoadItemResponse>? Accounts { get; set; }
}

public sealed class EWalletLoadItemResponse : EWalletStatus
{
    [JsonPropertyName("MerchantReferenceID")] public string? MerchantReferenceId { get; set; }
    [JsonPropertyName("TransactionRefID")]    public string? TransactionRefId    { get; set; }
}

/// <summary>Una cuenta a acreditar dentro de arrAccounts de eWallet_Load.</summary>
public sealed class EWalletLoadAccount
{
    [JsonPropertyName("UserName")]            public string? UserName            { get; set; }
    [JsonPropertyName("Amount")]              public decimal Amount              { get; set; }
    [JsonPropertyName("Comments")]            public string? Comments            { get; set; }

    /// <summary>
    /// Nuestra referencia. Combinado con AllowDuplicates = false es lo que da idempotencia:
    /// i-Payout rechaza una referencia ya usada en vez de acreditar dos veces.
    /// </summary>
    [JsonPropertyName("MerchantReferenceID")] public string? MerchantReferenceId { get; set; }
}
