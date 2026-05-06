namespace MLMConquerorGlobalEdition.Domain.Exceptions;

/// <summary>
/// Thrown when a token instance is in a non-redeemable status (Used, Voided, Expired)
/// at the moment of validation. Surface as the generic "token not valid" message —
/// never reveal that the token has already been used.
/// </summary>
public class TokenAlreadyUsedException : DomainException
{
    public TokenAlreadyUsedException()
        : base("TOKEN_NOT_VALID", "This token is not valid for this signup.") { }
}
