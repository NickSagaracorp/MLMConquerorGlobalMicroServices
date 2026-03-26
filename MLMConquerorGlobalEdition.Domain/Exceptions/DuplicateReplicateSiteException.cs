namespace MLMConquerorGlobalEdition.Domain.Exceptions;

public class DuplicateReplicateSiteException : DomainException
{
    public DuplicateReplicateSiteException(string slug)
        : base("DUPLICATE_REPLICATE_SITE", $"Replicate site slug '{slug}' is already taken.") { }
}
