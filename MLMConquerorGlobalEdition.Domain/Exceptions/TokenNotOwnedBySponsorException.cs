namespace MLMConquerorGlobalEdition.Domain.Exceptions;

/// <summary>
/// Thrown when the sponsor under whom the signup is being performed is not the current owner
/// of the token instance. Surface as the generic "token not valid" message —
/// never reveal who actually owns the token.
/// </summary>
public class TokenNotOwnedBySponsorException : DomainException
{
    public TokenNotOwnedBySponsorException()
        : base("TOKEN_NOT_VALID", "This token is not valid for this signup.") { }
}
