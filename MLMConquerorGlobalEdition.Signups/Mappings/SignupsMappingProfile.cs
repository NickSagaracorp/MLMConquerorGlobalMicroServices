using AutoMapper;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Mappings;

public class SignupsMappingProfile : Profile
{
    public SignupsMappingProfile()
    {
        CreateMap<MembershipLevel, MembershipLevelDto>();
    }
}
