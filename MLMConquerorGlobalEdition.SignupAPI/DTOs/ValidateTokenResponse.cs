namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

/// <summary>Response for real-time token validation. The endpoint always returns HTTP 200 with this body
/// to prevent attackers from distinguishing 'not found' from 'already used' or 'wrong sponsor'.</summary>
public class ValidateTokenResponse
{
    /// <summary>True only when every validation rule passes for the supplied product selection.</summary>
    public bool Valid { get; set; }

    /// <summary>Generic when the failure leaks security info (not yours / used / expired). Specific only for
    /// product mismatch since the user already knows what they selected.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Returned only on success. Lets the join page show a chip like "Token covers VIP, Subscription".</summary>
    public List<string>? AllowedProductIds { get; set; }

    /// <summary>Product names parallel to <see cref="AllowedProductIds"/> (same order). Hydrated by the handler.</summary>
    public List<string>? AllowedProductNames { get; set; }

    /// <summary>Returned on success — lets the join page hide product cards that aren't covered by the token.</summary>
    public int? TokenTypeId { get; set; }
}
