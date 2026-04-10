namespace MLMConquerorGlobalEdition.SharedKernel;

/// <summary>
/// Centralised cache key patterns. Use the static helper methods to build keys.
/// TTLs are defined as static TimeSpan fields alongside each key pattern.
/// </summary>
public static class CacheKeys
{
    public static string MemberProfile(string memberId)      => $"member:{memberId}:profile";
    public static string MemberRank(string memberId)         => $"member:{memberId}:rank";
    public static string MemberStats(string memberId)        => $"member:{memberId}:stats";
    public static string MemberTokenBalances(string memberId)=> $"member:{memberId}:token-balances";

    public const string RankDefinitions  = "rank:definitions";
    public const string MembershipLevels = "membership:levels";
    public const string CommissionTypes  = "commission:types";

    public static readonly TimeSpan MemberProfileTtl       = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MemberRankTtl          = TimeSpan.FromHours(1);
    public static readonly TimeSpan MemberStatsTtl         = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MemberTokenBalancesTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RankDefinitionsTtl     = TimeSpan.FromHours(24);
    public static readonly TimeSpan MembershipLevelsTtl    = TimeSpan.FromHours(24);
    public static readonly TimeSpan CommissionTypesTtl     = TimeSpan.FromHours(24);
}
