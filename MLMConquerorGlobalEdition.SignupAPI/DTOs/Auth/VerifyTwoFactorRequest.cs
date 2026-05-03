namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

public class VerifyTwoFactorRequest
{
    public string ChallengeToken { get; set; } = string.Empty;
    public string Code           { get; set; } = string.Empty;
}
