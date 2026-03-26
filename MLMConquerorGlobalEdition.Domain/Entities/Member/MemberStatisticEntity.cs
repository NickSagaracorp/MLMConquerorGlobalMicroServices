using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

public class MemberStatisticEntity : AuditChangesLongKey
{
    public required string MemberId { get; set; }
    public int PersonalPoints { get; set; }
    public int ExternalCustomerPoints { get; set; }
    public int DualTeamSize { get; set; }
    public int EnrollmentTeamSize { get; set; }
    public int DualTeamPoints { get; set; }
    public int EnrollmentPoints { get; set; }
    public int QualifiedSponsoredMembers { get; set; }
    public int QualifiedSponsoredExternalCustomers { get; set; }
    public int EnrollmentTeamGrowth { get; set; }
    public int DualteamGrowth { get; set; }
    public int EnrollmentTeamPointsGrowth { get; set; }
    public int DualTeamPointsGrowth { get; set; }
    public decimal CurrentWeekIncomeGrowth { get; set; }
    public decimal CurrentMonthIncomeGrowth { get; set; }
    public decimal CurrentYearIncomeGrowth { get; set; }
}
