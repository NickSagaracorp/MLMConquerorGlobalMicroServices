namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <summary>Per-axis outcome of evaluating a member against one RankRequirement.</summary>
public sealed class RankQualificationResult
{
    public bool Qualifies { get; init; }
    public bool MeetsGate { get; init; }
    public bool MeetsDualTeam { get; init; }
    public bool MeetsEnrollmentTeam { get; init; }
    public bool MeetsExternalMembers { get; init; }
    public bool MeetsPersonalPoints { get; init; }
    public bool MeetsSalesVolume { get; init; }

    public int EligibleDualTeamPoints { get; init; }
    public int EligibleEnrollmentTeamPoints { get; init; }
    public int PersonalCustomerPoints { get; init; }
    public int SponsoredMembersCount { get; init; }
    public decimal SalesVolume { get; init; }
}
