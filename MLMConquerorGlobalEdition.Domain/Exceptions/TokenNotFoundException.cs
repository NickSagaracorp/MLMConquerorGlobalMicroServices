namespace MLMConquerorGlobalEdition.Domain.Exceptions;

/// <summary>
/// Thrown when a token code submitted at signup does not match any known redeemable token instance.
/// Surface a generic "token not valid" message to the user — never reveal the specific cause.
/// </summary>
public class TokenNotFoundException : DomainException
{
    public TokenNotFoundException()
        : base("TOKEN_NOT_VALID", "This token is not valid for this signup.") { }
}
