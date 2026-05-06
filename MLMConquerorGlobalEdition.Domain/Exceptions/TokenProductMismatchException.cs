namespace MLMConquerorGlobalEdition.Domain.Exceptions;

/// <summary>
/// Thrown when the products selected by the user at signup are not all covered by
/// the TokenType.ProductLinks (Role = Granted) of the redeemed token.
/// Unlike the other token exceptions, this one carries a SPECIFIC message naming the
/// allowed products so the user can correct their selection — the attacker already
/// knows what they themselves selected, so this is not an information leak.
/// </summary>
public class TokenProductMismatchException : DomainException
{
    public TokenProductMismatchException(string allowedProductNames)
        : base("TOKEN_PRODUCT_MISMATCH",
              $"This token only covers: {allowedProductNames}. Please adjust your product selection.") { }
}
