namespace MLMConquerorGlobalEdition.Domain.Exceptions;

public class UnplacementLimitExceededException : DomainException
{
    public UnplacementLimitExceededException()
        : base("UNPLACEMENT_LIMIT_EXCEEDED", "Maximum of 2 unplacements has been exceeded.") { }
}
