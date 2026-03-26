using AutoMapper;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.DTOs;

namespace MLMConquerorGlobalEdition.RankEngine.Mappings;

public class RankEngineMappingProfile : Profile
{
    public RankEngineMappingProfile()
    {
        CreateMap<RankDefinition, RankDefinitionResponse>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

        CreateMap<RankRequirement, RankRequirementResponse>();
    }
}
