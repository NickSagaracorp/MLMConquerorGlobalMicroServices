namespace MLMConquerorGlobalEdition.Domain.Exceptions;

public class UnplacementWindowExpiredException : DomainException
{
    public UnplacementWindowExpiredException()
        : base("UNPLACEMENT_WINDOW_EXPIRED", "Unplacement window of 72 hours has expired.") { }
}
