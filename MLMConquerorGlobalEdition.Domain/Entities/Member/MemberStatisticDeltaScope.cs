namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>Plan Q: a quien se aplica un MemberStatisticDelta.</summary>
public enum MemberStatisticDeltaScope
{
    /// <summary>Solo a MemberId (las filas anteriores al Plan Q: una por ascendiente).</summary>
    Member = 0,
    /// <summary>
    /// MemberId es el patrocinador (o, en los puntos por membresia, el propio miembro): el delta se aplica a el y a cada
    /// ascendiente de su Node; QualifiedSponsoredMembersDelta solo a el. Encola rango hasta GenealogyUpline.EnrollmentLevels.
    /// </summary>
    SponsorLine = 1,
    /// <summary>
    /// Plan Q (DQ13): MemberId es el miembro cuya suscripcion cambio; el delta se aplica a cada ascendiente de su Node
    /// (distancia 1 en adelante), no a el. No encola evaluaciones de rango. Lo escribe solo QualifiedTeamDeltas.
    /// </summary>
    AncestorsOf = 2
}
