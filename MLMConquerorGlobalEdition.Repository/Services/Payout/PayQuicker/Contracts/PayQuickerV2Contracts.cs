using System.Text.Json.Serialization;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker.Contracts;

// DTOs de la API v2. Todos llevan [JsonPropertyName] explícito: el contrato con PayQuicker
// no puede depender de cómo se llame una propiedad en C#, que es exactamente el problema
// que tiene la integración de MWRLife (un rename desde el IDE le rompe el payload en silencio).

// ── Requests ───────────────────────────────────────────────────────────────

/// <summary>POST /invitations — programa "hosted portal".</summary>
public sealed class V2InvitationRequest
{
    [JsonPropertyName("programToken")]  public string? ProgramToken  { get; set; }
    [JsonPropertyName("programUserId")] public string? ProgramUserId { get; set; }
    [JsonPropertyName("email")]         public string? Email         { get; set; }
    [JsonPropertyName("notifyUser")]    public bool    NotifyUser    { get; set; }
    [JsonPropertyName("issueCard")]     public bool    IssueCard     { get; set; }
    [JsonPropertyName("userType")]      public string  UserType      { get; set; } = "INDIVIDUAL";
    [JsonPropertyName("language")]      public string? Language      { get; set; }
    [JsonPropertyName("firstName")]     public string? FirstName     { get; set; }
    [JsonPropertyName("lastName")]      public string? LastName      { get; set; }
}

/// <summary>
/// POST /transfers con transferType = PAYMENT, variante Portal.
/// NO lleva currency: el schema PortalPaymentQuote del OpenAPI no define esa propiedad
/// (sólo el tipo TRANSFER la tiene). MWRLife la mandaba de más.
/// </summary>
public sealed class V2PaymentRequest
{
    [JsonPropertyName("transferType")]     public string  TransferType     { get; set; } = "PAYMENT";
    [JsonPropertyName("sourceToken")]      public string? SourceToken      { get; set; }
    [JsonPropertyName("programUserId")]    public string? ProgramUserId    { get; set; }
    [JsonPropertyName("email")]            public string? Email            { get; set; }
    [JsonPropertyName("amount")]           public string? Amount           { get; set; }
    [JsonPropertyName("clientPaymentRef")] public string? ClientPaymentRef { get; set; }
    [JsonPropertyName("purpose")]          public string? Purpose          { get; set; }
    [JsonPropertyName("acceptanceMode")]   public string? AcceptanceMode   { get; set; }
    [JsonPropertyName("memo")]             public string? Memo             { get; set; }
    [JsonPropertyName("note")]             public string? Note             { get; set; }
}

/// <summary>POST /balances/search.</summary>
public sealed class V2BalanceSearchRequest
{
    [JsonPropertyName("scope")]     public string? Scope     { get; set; }
    [JsonPropertyName("scopeType")] public string  ScopeType { get; set; } = "PROGRAM_USER_ID";
    [JsonPropertyName("filters")]   public object[] Filters  { get; set; } = [];
    [JsonPropertyName("sort")]      public object[] Sort     { get; set; } = [];
    [JsonPropertyName("page")]      public int      Page     { get; set; } = 1;
    /// <summary>Máximo 100 según el schema BalanceSearchRequest.</summary>
    [JsonPropertyName("pageSize")]  public int      PageSize { get; set; } = 50;
}

/// <summary>POST /transfers/search — para resolver el estado por clientPaymentRef.</summary>
public sealed class V2TransferSearchRequest
{
    [JsonPropertyName("filters")]  public List<V2SearchFilter> Filters { get; set; } = [];
    [JsonPropertyName("sort")]     public object[] Sort     { get; set; } = [];
    [JsonPropertyName("page")]     public int      Page     { get; set; } = 1;
    [JsonPropertyName("pageSize")] public int      PageSize { get; set; } = 10;
}

public sealed class V2SearchFilter
{
    [JsonPropertyName("field")]      public string? Field      { get; set; }
    [JsonPropertyName("comparison")] public string  Comparison { get; set; } = "EQUAL_TO";
    [JsonPropertyName("value")]      public string? Value      { get; set; }
}

// ── Responses ──────────────────────────────────────────────────────────────

/// <summary>
/// Respuesta de POST /invitations. Trae token (invt-…), key y url.
/// El que se persiste es `key`: es el que viaja en la URL de bienvenida
/// (…/Welcome?invitationId={key}) y el equivalente al invitationKey de v1.
/// </summary>
public sealed class V2InvitationResponse
{
    [JsonPropertyName("token")]              public string? Token              { get; set; }
    [JsonPropertyName("key")]                public string? Key                { get; set; }
    [JsonPropertyName("url")]                public string? Url                { get; set; }
    [JsonPropertyName("status")]             public string? Status             { get; set; }
    [JsonPropertyName("registrationStatus")] public string? RegistrationStatus { get; set; }
    [JsonPropertyName("programUserId")]      public string? ProgramUserId      { get; set; }
    [JsonPropertyName("email")]              public string? Email              { get; set; }
}

/// <summary>Respuesta de POST /transfers con transferType PAYMENT.</summary>
public sealed class V2TransferResponse
{
    [JsonPropertyName("token")]            public string? Token            { get; set; }
    [JsonPropertyName("amount")]           public string? Amount           { get; set; }
    [JsonPropertyName("currency")]         public string? Currency         { get; set; }
    [JsonPropertyName("quoteStatus")]      public string? QuoteStatus      { get; set; }
    [JsonPropertyName("receiptStatus")]    public string? ReceiptStatus    { get; set; }
    [JsonPropertyName("clientPaymentRef")] public string? ClientPaymentRef { get; set; }
    [JsonPropertyName("transferType")]     public string? TransferType     { get; set; }
}

public sealed class V2BalanceSearchResponse
{
    [JsonPropertyName("payload")] public List<V2Balance>? Payload { get; set; }
}

public sealed class V2Balance
{
    /// <summary>Llega como string para no perder precisión decimal.</summary>
    [JsonPropertyName("amount")]   public string? Amount   { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
}

public sealed class V2TransferSearchResponse
{
    [JsonPropertyName("payload")] public List<V2TransferResponse>? Payload { get; set; }
}
