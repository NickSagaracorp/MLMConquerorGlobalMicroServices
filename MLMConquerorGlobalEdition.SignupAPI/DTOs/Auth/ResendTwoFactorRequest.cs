namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

public class ResendTwoFactorRequest
{
    public string ChallengeToken { get; set; } = string.Empty;
}
