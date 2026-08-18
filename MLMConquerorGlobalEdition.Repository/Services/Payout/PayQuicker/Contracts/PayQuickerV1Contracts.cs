using System.Text.Json.Serialization;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker.Contracts;

// DTOs de la API v1. Contrato distinto al de v2, no una variante: acá el monto viaja
// anidado en un objeto "monetary" y el usuario se identifica por
// userCompanyAssignedUniqueKey (que en nuestro dominio es el MemberId).

// ── Requests ───────────────────────────────────────────────────────────────

public sealed class V1InvitationRequest
{
    [JsonPropertyName("fundingAccountPublicId")]        public string? FundingAccountPublicId        { get; set; }
    [JsonPropertyName("userCompanyAssignedUniqueKey")]  public string? UserCompanyAssignedUniqueKey  { get; set; }
    [JsonPropertyName("userNotificationEmailAddress")]  public string? UserNotificationEmailAddress  { get; set; }
}

public sealed class V1PaymentBatchRequest
{
    [JsonPropertyName("payments")] public List<V1PaymentItem> Payments { get; set; } = [];
}

public sealed class V1PaymentItem
{
    [JsonPropertyName("fundingAccountPublicId")]       public string? FundingAccountPublicId       { get; set; }
    [JsonPropertyName("monetary")]                     public V1Monetary? Monetary                 { get; set; }
    [JsonPropertyName("userCompanyAssignedUniqueKey")] public string? UserCompanyAssignedUniqueKey { get; set; }
    [JsonPropertyName("userNotificationEmailAddress")] public string? UserNotificationEmailAddress { get; set; }

    /// <summary>Referencia del lado del cliente. Es lo más parecido a una clave de idempotencia que ofrece v1.</summary>
    [JsonPropertyName("accountingId")]                 public string? AccountingId                 { get; set; }
    [JsonPropertyName("recipientUserLanguageCode")]    public string  RecipientUserLanguageCode    { get; set; } = "en-us";
    [JsonPropertyName("issuePlasticCard")]             public bool    IssuePlasticCard             { get; set; }
}

/// <summary>v1 anida el importe: "monetary": { "amount": 5.02 }.</summary>
public sealed class V1Monetary
{
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
}

// ── Responses ──────────────────────────────────────────────────────────────

public sealed class V1InvitationResponse
{
    [JsonPropertyName("invitationKey")] public string? InvitationKey { get; set; }
    [JsonPropertyName("status")]        public string? Status        { get; set; }
}

public sealed class V1PaymentResponse
{
    [JsonPropertyName("transactionPublicId")] public string? TransactionPublicId { get; set; }
    [JsonPropertyName("accountingId")]        public string? AccountingId        { get; set; }
    [JsonPropertyName("status")]              public string? Status              { get; set; }
}

/// <summary>v1 devuelve un arreglo de saldos, uno por moneda.</summary>
public sealed class V1Balance
{
    [JsonPropertyName("amount")]          public decimal? Amount          { get; set; }
    /// <summary>Formato "Currency_USD", no "USD".</summary>
    [JsonPropertyName("currency")]        public string?  Currency        { get; set; }
    [JsonPropertyName("formattedAmount")] public string?  FormattedAmount { get; set; }
}
