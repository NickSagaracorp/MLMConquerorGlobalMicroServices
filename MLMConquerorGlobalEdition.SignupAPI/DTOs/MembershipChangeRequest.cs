namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

public class MembershipChangeRequest
{
    public int NewMembershipLevelId { get; set; }
    public string? Reason { get; set; }
}
