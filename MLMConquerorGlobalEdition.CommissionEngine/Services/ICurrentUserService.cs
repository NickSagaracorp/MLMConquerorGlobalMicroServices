namespace MLMConquerorGlobalEdition.CommissionEngine.Services;

public interface ICurrentUserService
{
    string UserId { get; }
    string MemberId { get; }
    bool IsAdmin { get; }
}
