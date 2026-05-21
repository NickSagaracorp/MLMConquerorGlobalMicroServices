namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <summary>
/// GlobalParameter keys and fallback defaults for the universal rank gate (§2.3 of the spec):
///   (sponsored >= MinSponsoredMembers AND pcp >= MinPersonalPointsWithSponsors)
///   OR (pcp >= MinPersonalPointsWithoutSponsors)
/// </summary>
public static class RankGateParameters
{
    public const string MinSponsoredMembersKey            = "RankGate.MinSponsoredMembers";
    public const string MinPersonalPointsWithSponsorsKey  = "RankGate.MinPersonalPointsWithSponsors";
    public const string MinPersonalPointsWithoutSponsorsKey = "RankGate.MinPersonalPointsWithoutSponsors";

    public const int DefaultMinSponsoredMembers             = 3;
    public const int DefaultMinPersonalPointsWithSponsors   = 9;
    public const int DefaultMinPersonalPointsWithoutSponsors = 12;
}
