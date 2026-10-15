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

    /// <summary>
    /// Cobra de la cuenta del usuario con AutoChargeAccount = "true". Es el débito del legado
    /// (EWallet_AddDeductionFromBalance). PROCESSING y PENDING devuelven también m_Code 0.
    /// </summary>
    public const string AddCheckoutItems = "eWallet_AddCheckoutItems";
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

/// <summary>Un ítem de arrItems de eWallet_AddCheckoutItems, con los valores del legado.</summary>
public sealed class EWalletCheckoutItem
{
    [JsonPropertyName("Amount")]              public decimal Amount              { get; set; }
    [JsonPropertyName("CurrencyCode")]        public string  CurrencyCode        { get; set; } = "USD";
    [JsonPropertyName("ItemDescription")]     public string  ItemDescription     { get; set; } = string.Empty;
    [JsonPropertyName("MerchantReferenceID")] public string  MerchantReferenceId { get; set; } = string.Empty;
    [JsonPropertyName("IsSubscription")]      public string  IsSubscription      { get; set; } = "false";
    [JsonPropertyName("SubscriptionAmount")]  public decimal SubscriptionAmount  { get; set; }
    [JsonPropertyName("UserReturnURL")]       public string  UserReturnUrl       { get; set; } = string.Empty;
    [JsonPropertyName("MustComplete")]        public string  MustComplete        { get; set; } = "false";
}

public sealed class EWalletCheckoutResponse : EWalletResponseEnvelope
{
    [JsonPropertyName("arrItemsResponse")] public List<EWalletStatus>? Items { get; set; }
}

public enum EWalletDeductionStatus
{
    /// <summary>Envoltorio e ítems con m_Code &gt;= 0. Puede estar en PROCESSING/PENDING: hay que verificar el saldo.</summary>
    Accepted,

    /// <summary>i-Payout dijo que no (código negativo) o no se llegó a enviar (sin credencial).</summary>
    Rejected,

    /// <summary>No hubo respuesta clara: el dinero pudo salir o no. No se reintenta.</summary>
    Unknown
}

public sealed record EWalletDeduction(EWalletDeductionStatus Status, string? Code, string? Text);
