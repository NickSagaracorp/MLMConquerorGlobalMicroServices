namespace MLMConquerorGlobalEdition.Domain.Exceptions;

public class MembershipChangeNotAllowedException : DomainException
{
    public MembershipChangeNotAllowedException(string reason)
        : base("MEMBERSHIP_CHANGE_NOT_ALLOWED", reason) { }
}
