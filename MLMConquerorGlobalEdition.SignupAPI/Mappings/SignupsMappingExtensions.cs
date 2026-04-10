using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Mappings;

public static class SignupsMappingExtensions
{
    public static MembershipLevelDto ToDto(this MembershipLevel entity) => new()
    {
        Id           = entity.Id,
        Name         = entity.Name,
        Description  = entity.Description,
        Price        = entity.Price,
        RenewalPrice = entity.RenewalPrice,
        SortOrder    = entity.SortOrder,
        IsFree       = entity.IsFree,
        IsAutoRenew  = entity.IsAutoRenew
    };
}
