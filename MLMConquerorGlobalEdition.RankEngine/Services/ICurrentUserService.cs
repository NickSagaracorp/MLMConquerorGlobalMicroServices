namespace MLMConquerorGlobalEdition.RankEngine.Services;

public interface ICurrentUserService
{
    string UserId { get; }
    string MemberId { get; }
    string Email { get; }
    bool IsAdmin { get; }
    IEnumerable<string> Roles { get; }
}
