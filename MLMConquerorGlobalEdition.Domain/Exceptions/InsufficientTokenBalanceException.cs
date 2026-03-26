namespace MLMConquerorGlobalEdition.Domain.Exceptions;

public class InsufficientTokenBalanceException : DomainException
{
    public InsufficientTokenBalanceException()
        : base("INSUFFICIENT_TOKEN_BALANCE", "Insufficient token balance to complete this operation.") { }
}
