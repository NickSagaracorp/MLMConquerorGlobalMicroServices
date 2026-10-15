namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

public class StepUpVerifyRequest
{
    public string OperationKey   { get; set; } = string.Empty;
    public string ChallengeToken { get; set; } = string.Empty;
    public string Code           { get; set; } = string.Empty;
}
