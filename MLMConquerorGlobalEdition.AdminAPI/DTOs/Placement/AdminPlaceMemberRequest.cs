using System.ComponentModel.DataAnnotations;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement;

public class AdminPlaceMemberRequest
{
    [Required] public string MemberToPlaceId      { get; set; } = string.Empty;
    [Required] public string TargetParentMemberId { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Left|Right)$", ErrorMessage = "Side must be 'Left' or 'Right'.")]
    public string Side { get; set; } = string.Empty;
}
