namespace MLMConquerorGlobalEdition.SharedAPICenter.DTOs;

/// <summary>
/// Response returned by the POST /api/v1/external/member/validate-token endpoint.
/// </summary>
public class ValidateTokenResponse
{
    /// <summary>
    /// True when AvailableBalance &gt;= RequiredQuantity and the token type exists.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>Current token balance held by the member for the requested type.</summary>
    public int AvailableBalance { get; set; }

    /// <summary>Human-readable name of the token type (e.g. "Guest Pass").</summary>
    public string? TokenTypeName { get; set; }
}
