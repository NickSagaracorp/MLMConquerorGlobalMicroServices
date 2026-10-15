namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>El reto a redimir en <c>step-up/verify</c>. Channel va como texto: la UI no referencia Domain.</summary>
public class StepUpChallengeResponse
{
    public string   ChallengeToken { get; set; } = string.Empty;
    public string   Channel        { get; set; } = string.Empty;
    public string   MaskedTarget   { get; set; } = string.Empty;
    public DateTime ExpiresAt      { get; set; }
}
