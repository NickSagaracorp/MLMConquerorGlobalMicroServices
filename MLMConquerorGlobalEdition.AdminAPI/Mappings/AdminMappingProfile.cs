using AutoMapper;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.MembershipLevels;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Products;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;

namespace MLMConquerorGlobalEdition.AdminAPI.Mappings;

public class AdminMappingProfile : Profile
{
    public AdminMappingProfile()
    {
        // Products
        // Description, DescriptionPromo, and ThemeClass are intentionally ignored
        // here — the controller sanitizes them via IHtmlSanitizerService and validates
        // ThemeClass against a whitelist before assigning to the entity directly.
        CreateMap<Product, ProductDto>();
        CreateMap<CreateProductDto, Product>()
            .ForMember(d => d.Description,      o => o.Ignore())
            .ForMember(d => d.DescriptionPromo, o => o.Ignore())
            .ForMember(d => d.ThemeClass,       o => o.Ignore());
        CreateMap<UpdateProductDto, Product>()
            .ForMember(d => d.Description,      o => o.Ignore())
            .ForMember(d => d.DescriptionPromo, o => o.Ignore())
            .ForMember(d => d.ThemeClass,       o => o.Ignore());

        // MembershipLevel
        CreateMap<MembershipLevel, MembershipLevelDto>();
        CreateMap<CreateMembershipLevelDto, MembershipLevel>();
        CreateMap<UpdateMembershipLevelDto, MembershipLevel>();

        // RankDefinition
        CreateMap<RankDefinition, RankDefinitionDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<CreateRankDefinitionDto, RankDefinition>();
        CreateMap<UpdateRankDefinitionDto, RankDefinition>()
            .ForMember(d => d.Status, o => o.MapFrom(s =>
                Enum.Parse<MLMConquerorGlobalEdition.Domain.Entities.Rank.RankDefinitionStatus>(s.Status)));

        // RankRequirement
        CreateMap<RankRequirement, RankRequirementDto>();
        CreateMap<CreateRankRequirementDto, RankRequirement>();

        // TokenType
        CreateMap<TokenType, TokenTypeDto>();
        CreateMap<CreateTokenTypeDto, TokenType>();
        CreateMap<UpdateTokenTypeDto, TokenType>();

        // CommissionCategory
        CreateMap<CommissionCategory, CommissionCategoryDto>();
        CreateMap<CreateCommissionCategoryDto, CommissionCategory>();
        CreateMap<UpdateCommissionCategoryDto, CommissionCategory>();

        // CommissionType
        CreateMap<CommissionType, CommissionTypeDto>();
        CreateMap<CreateCommissionTypeDto, CommissionType>();
        CreateMap<UpdateCommissionTypeDto, CommissionType>();
    }
}
