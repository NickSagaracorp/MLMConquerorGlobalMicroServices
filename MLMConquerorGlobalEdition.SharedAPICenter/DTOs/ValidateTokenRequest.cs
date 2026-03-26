namespace MLMConquerorGlobalEdition.SharedAPICenter.DTOs;

/// <summary>
/// Request body for the POST /api/v1/external/member/validate-token endpoint.
/// Checks whether a member holds sufficient balance of the specified token type.
/// </summary>
public class ValidateTokenRequest
{
    public string MemberId { get; set; } = string.Empty;
    public int TokenTypeId { get; set; }

    /// <summary>Minimum balance required. Defaults to 1.</summary>
    public int RequiredQuantity { get; set; } = 1;
}
