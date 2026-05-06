namespace MLMConquerorGlobalEdition.Domain.Exceptions;

/// <summary>
/// Thrown when an ambassador tries to transfer a token to a recipient who is not in
/// their enrollment subtree. Tokens can only flow downward through the genealogy.
/// </summary>
public class TokenNotInDownlineException : DomainException
{
    public TokenNotInDownlineException()
        : base("RECIPIENT_NOT_IN_DOWNLINE",
              "Recipient must be a member of your enrollment team.") { }
}
