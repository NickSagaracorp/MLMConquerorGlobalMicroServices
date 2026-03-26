namespace MLMConquerorGlobalEdition.Domain.Exceptions;

public class CommissionAlreadyPaidException : DomainException
{
    public CommissionAlreadyPaidException()
        : base("COMMISSION_ALREADY_PAID", "A paid commission cannot be cancelled.") { }
}
