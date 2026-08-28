namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>Redención del código SMS que confirma el teléfono recién dado de alta.</summary>
public class VerifyPhoneRequest
{
    public string ChallengeToken { get; set; } = string.Empty;
    public string Code           { get; set; } = string.Empty;
}
