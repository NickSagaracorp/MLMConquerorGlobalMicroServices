namespace MLMConquerorGlobalEdition.Domain.Exceptions;

public class PlacementWindowExpiredException : DomainException
{
    public PlacementWindowExpiredException()
        : base("PLACEMENT_WINDOW_EXPIRED", "Placement window of 30 days has expired.") { }
}
