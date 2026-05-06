namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// The signup-page action that produced a fingerprint event. Allows analytics to slice
/// risk by funnel step (just typing a token vs. actually submitting a signup).
/// </summary>
public enum SignupRiskFlow
{
    AmbassadorSignup = 0,
    MemberSignup     = 1,
    TokenValidation  = 2
}
