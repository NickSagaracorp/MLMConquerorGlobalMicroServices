using AutoMapper;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;

namespace MLMConquerorGlobalEdition.CommissionEngine.Mappings;

public class CommissionEngineMappingProfile : Profile
{
    public CommissionEngineMappingProfile()
    {
        CreateMap<CommissionType, CommissionTypeResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null));

        CreateMap<CommissionEarning, CommissionEarningResponse>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.CommissionTypeName, opt => opt.MapFrom(src => src.CommissionType != null ? src.CommissionType.Name : string.Empty));
    }
}
