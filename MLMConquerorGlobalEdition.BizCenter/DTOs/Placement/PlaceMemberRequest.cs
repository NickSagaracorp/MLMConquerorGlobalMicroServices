using System.ComponentModel.DataAnnotations;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;

public class PlaceMemberRequest
{
    [Required]
    public string MemberToPlaceId       { get; set; } = string.Empty;

    [Required]
    public string TargetParentMemberId  { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Left|Right)$", ErrorMessage = "Side must be 'Left' or 'Right'.")]
    public string Side                  { get; set; } = string.Empty;
}
