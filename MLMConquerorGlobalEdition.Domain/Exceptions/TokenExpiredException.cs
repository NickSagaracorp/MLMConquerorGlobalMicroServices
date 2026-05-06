namespace MLMConquerorGlobalEdition.Domain.Exceptions;

/// <summary>
/// Thrown when a token instance has passed its ExpiresAt date.
/// Surface as the generic "token not valid" message — never reveal the actual expiry date.
/// </summary>
public class TokenExpiredException : DomainException
{
    public TokenExpiredException()
        : base("TOKEN_NOT_VALID", "This token is not valid for this signup.") { }
}
