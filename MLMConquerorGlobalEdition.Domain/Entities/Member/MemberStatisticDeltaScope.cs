namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>Plan Q: a quien se aplica un MemberStatisticDelta.</summary>
public enum MemberStatisticDeltaScope
{
    /// <summary>Solo a MemberId (las filas anteriores al Plan Q: una por ascendiente).</summary>
    Member = 0,
    /// <summary>MemberId es el patrocinador: el delta se aplica a el y a cada ascendiente de su Node; QualifiedSponsoredMembersDelta solo a el.</summary>
    SponsorLine = 1
}
