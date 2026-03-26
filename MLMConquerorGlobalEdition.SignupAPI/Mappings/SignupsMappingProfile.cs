using AutoMapper;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Mappings;

public class SignupsMappingProfile : Profile
{
    public SignupsMappingProfile()
    {
        CreateMap<MembershipLevel, MembershipLevelDto>();
    }
}
