using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;

public class UpdateMemberStatusRequest
{
    public MemberAccountStatus Status { get; set; }
    public string? Reason { get; set; }
}
