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

public static class AdminMappingExtensions
{
    // ─── Product ─────────────────────────────────────────────────────────────

    public static ProductDto ToDto(this Product entity) => new()
    {
        Id                     = entity.Id,
        Name                   = entity.Name,
        Description            = entity.Description,
        ImageUrl               = entity.ImageUrl,
        MonthlyFee             = entity.MonthlyFee,
        SetupFee               = entity.SetupFee,
        Price90Days            = entity.Price90Days,
        Price180Days           = entity.Price180Days,
        AnnualPrice            = entity.AnnualPrice,
        DescriptionPromo       = entity.DescriptionPromo,
        MonthlyFeePromo        = entity.MonthlyFeePromo,
        SetupFeePromo          = entity.SetupFeePromo,
        ImageUrlPromo          = entity.ImageUrlPromo,
        QualificationPoins     = entity.QualificationPoins,
        QualificationPoinsPromo = entity.QualificationPoinsPromo,
        IsActive               = entity.IsActive,
        CorporateFee           = entity.CorporateFee,
        JoinPageMembership     = entity.JoinPageMembership,
        MembershipLevelId      = entity.MembershipLevelId,
        OldSystemProductId     = entity.OldSystemProductId,
        ThemeClass             = entity.ThemeClass
    };

    // Description, DescriptionPromo, ThemeClass intentionally excluded —
    // the controller sanitizes them via IHtmlSanitizerService before assigning directly.
    public static Product ToNewEntity(this CreateProductDto dto) => new()
    {
        Name                   = dto.Name,
        Description            = string.Empty,   // overwritten by controller after sanitization
        ImageUrl               = dto.ImageUrl,
        MonthlyFee             = dto.MonthlyFee,
        SetupFee               = dto.SetupFee,
        Price90Days            = dto.Price90Days,
        Price180Days           = dto.Price180Days,
        AnnualPrice            = dto.AnnualPrice,
        MonthlyFeePromo        = dto.MonthlyFeePromo,
        SetupFeePromo          = dto.SetupFeePromo,
        ImageUrlPromo          = dto.ImageUrlPromo,
        QualificationPoins     = dto.QualificationPoins,
        QualificationPoinsPromo = dto.QualificationPoinsPromo,
        CorporateFee           = dto.CorporateFee,
        JoinPageMembership     = dto.JoinPageMembership,
        MembershipLevelId      = dto.MembershipLevelId,
        OldSystemProductId     = dto.OldSystemProductId
    };

    // Description, DescriptionPromo, ThemeClass excluded — controller applies them after sanitization.
    public static void ApplyTo(this UpdateProductDto dto, Product entity)
    {
        entity.Name                    = dto.Name;
        entity.ImageUrl                = dto.ImageUrl;
        entity.MonthlyFee              = dto.MonthlyFee;
        entity.SetupFee                = dto.SetupFee;
        entity.Price90Days             = dto.Price90Days;
        entity.Price180Days            = dto.Price180Days;
        entity.AnnualPrice             = dto.AnnualPrice;
        entity.MonthlyFeePromo         = dto.MonthlyFeePromo;
        entity.SetupFeePromo           = dto.SetupFeePromo;
        entity.ImageUrlPromo           = dto.ImageUrlPromo;
        entity.QualificationPoins      = dto.QualificationPoins;
        entity.QualificationPoinsPromo = dto.QualificationPoinsPromo;
        entity.IsActive                = dto.IsActive;
        entity.CorporateFee            = dto.CorporateFee;
        entity.JoinPageMembership      = dto.JoinPageMembership;
        entity.MembershipLevelId       = dto.MembershipLevelId;
    }

    // ─── MembershipLevel ─────────────────────────────────────────────────────

    public static MembershipLevelDto ToDto(this MembershipLevel entity) => new()
    {
        Id            = entity.Id,
        Name          = entity.Name,
        Description   = entity.Description,
        Price         = entity.Price,
        RenewalPrice  = entity.RenewalPrice,
        SortOrder     = entity.SortOrder,
        IsFree        = entity.IsFree,
        IsAutoRenew   = entity.IsAutoRenew,
        IsActive      = entity.IsActive
    };

    public static MembershipLevel ToNewEntity(this CreateMembershipLevelDto dto) => new()
    {
        Name         = dto.Name,
        Description  = dto.Description,
        Price        = dto.Price,
        RenewalPrice = dto.RenewalPrice,
        SortOrder    = dto.SortOrder,
        IsFree       = dto.IsFree,
        IsAutoRenew  = dto.IsAutoRenew
    };

    public static void ApplyTo(this UpdateMembershipLevelDto dto, MembershipLevel entity)
    {
        entity.Name        = dto.Name;
        entity.Description = dto.Description;
        entity.Price       = dto.Price;
        entity.RenewalPrice = dto.RenewalPrice;
        entity.SortOrder   = dto.SortOrder;
        entity.IsFree      = dto.IsFree;
        entity.IsAutoRenew = dto.IsAutoRenew;
        entity.IsActive    = dto.IsActive;
    }

    // ─── RankDefinition ──────────────────────────────────────────────────────

    public static RankDefinitionDto ToDto(this RankDefinition entity) => new()
    {
        Id                     = entity.Id,
        Name                   = entity.Name,
        Description            = entity.Description,
        SortOrder              = entity.SortOrder,
        Status                 = entity.Status.ToString(),
        CertificateTemplateUrl = entity.CertificateTemplateUrl
    };

    public static RankDefinition ToNewEntity(this CreateRankDefinitionDto dto) => new()
    {
        Name                   = dto.Name,
        Description            = dto.Description,
        SortOrder              = dto.SortOrder,
        CertificateTemplateUrl = dto.CertificateTemplateUrl
    };

    public static void ApplyTo(this UpdateRankDefinitionDto dto, RankDefinition entity)
    {
        entity.Name                   = dto.Name;
        entity.Description            = dto.Description;
        entity.SortOrder              = dto.SortOrder;
        entity.Status                 = Enum.Parse<RankDefinitionStatus>(dto.Status);
        entity.CertificateTemplateUrl = dto.CertificateTemplateUrl;
    }

    // ─── RankRequirement ─────────────────────────────────────────────────────

    public static RankRequirementDto ToDto(this RankRequirement entity) => new()
    {
        Id                               = entity.Id,
        RankDefinitionId                 = entity.RankDefinitionId,
        LevelNo                          = entity.LevelNo,
        PersonalPoints                   = entity.PersonalPoints,
        TeamPoints                       = entity.TeamPoints,
        MaxTeamPointsPerBranch           = entity.MaxTeamPointsPerBranch,
        EnrollmentTeam                   = entity.EnrollmentTeam,
        PlacementQualifiedTeamMembers    = entity.PlacementQualifiedTeamMembers,
        EnrollmentQualifiedTeamMembers   = entity.EnrollmentQualifiedTeamMembers,
        MaxEnrollmentTeamPointsPerBranch = entity.MaxEnrollmentTeamPointsPerBranch,
        ExternalMembers                  = entity.ExternalMembers,
        SponsoredMembers                 = entity.SponsoredMembers,
        SalesVolume                      = entity.SalesVolume,
        RankBonus                        = entity.RankBonus,
        DailyBonus                       = entity.DailyBonus,
        MonthlyBonus                     = entity.MonthlyBonus,
        LifetimeHoldingDuration          = entity.LifetimeHoldingDuration,
        RankDescription                  = entity.RankDescription,
        CurrentRankDescription           = entity.CurrentRankDescription,
        AchievementMessage               = entity.AchievementMessage,
        CertificateUrl                   = entity.CertificateUrl
    };

    public static RankRequirement ToNewEntity(this CreateRankRequirementDto dto) => new()
    {
        RankDefinitionId                 = dto.RankDefinitionId,
        LevelNo                          = dto.LevelNo,
        PersonalPoints                   = dto.PersonalPoints,
        TeamPoints                       = dto.TeamPoints,
        MaxTeamPointsPerBranch           = dto.MaxTeamPointsPerBranch,
        EnrollmentTeam                   = dto.EnrollmentTeam,
        PlacementQualifiedTeamMembers    = dto.PlacementQualifiedTeamMembers,
        EnrollmentQualifiedTeamMembers   = dto.EnrollmentQualifiedTeamMembers,
        MaxEnrollmentTeamPointsPerBranch = dto.MaxEnrollmentTeamPointsPerBranch,
        ExternalMembers                  = dto.ExternalMembers,
        SponsoredMembers                 = dto.SponsoredMembers,
        SalesVolume                      = dto.SalesVolume,
        RankBonus                        = dto.RankBonus,
        DailyBonus                       = dto.DailyBonus,
        MonthlyBonus                     = dto.MonthlyBonus,
        LifetimeHoldingDuration          = dto.LifetimeHoldingDuration,
        RankDescription                  = dto.RankDescription,
        CurrentRankDescription           = dto.CurrentRankDescription,
        AchievementMessage               = dto.AchievementMessage,
        CertificateUrl                   = dto.CertificateUrl
    };

    // ─── TokenType ───────────────────────────────────────────────────────────

    public static TokenTypeDto ToDto(this TokenType entity) => new()
    {
        Id          = entity.Id,
        Name        = entity.Name,
        Description = entity.Description,
        IsGuestPass = entity.IsGuestPass,
        TemplateUrl = entity.TemplateUrl,
        IsActive    = entity.IsActive
    };

    public static TokenType ToNewEntity(this CreateTokenTypeDto dto) => new()
    {
        Name        = dto.Name,
        Description = dto.Description,
        IsGuestPass = dto.IsGuestPass,
        TemplateUrl = dto.TemplateUrl
    };

    public static void ApplyTo(this UpdateTokenTypeDto dto, TokenType entity)
    {
        entity.Name        = dto.Name;
        entity.Description = dto.Description;
        entity.IsGuestPass = dto.IsGuestPass;
        entity.TemplateUrl = dto.TemplateUrl;
        entity.IsActive    = dto.IsActive;
    }

    // ─── CommissionCategory ──────────────────────────────────────────────────

    public static CommissionCategoryDto ToDto(this CommissionCategory entity) => new()
    {
        Id          = entity.Id,
        Name        = entity.Name,
        Description = entity.Description,
        IsActive    = entity.IsActive
    };

    public static CommissionCategory ToNewEntity(this CreateCommissionCategoryDto dto) => new()
    {
        Name        = dto.Name,
        Description = dto.Description
    };

    public static void ApplyTo(this UpdateCommissionCategoryDto dto, CommissionCategory entity)
    {
        entity.Name        = dto.Name;
        entity.Description = dto.Description;
        entity.IsActive    = dto.IsActive;
    }

    // ─── CommissionType ──────────────────────────────────────────────────────

    public static CommissionTypeDto ToDto(this CommissionType entity) => new()
    {
        Id                           = entity.Id,
        CommissionCategoryId         = entity.CommissionCategoryId,
        Name                         = entity.Name,
        Description                  = entity.Description,
        Percentage                   = entity.Percentage,
        Amount                       = entity.Amount,
        AmountPromo                  = entity.AmountPromo,
        PaymentDelayDays             = entity.PaymentDelayDays,
        IsActive                     = entity.IsActive,
        IsRealTime                   = entity.IsRealTime,
        IsPaidOnSignup               = entity.IsPaidOnSignup,
        IsPaidOnRenewal              = entity.IsPaidOnRenewal,
        Cummulative                  = entity.Cummulative,
        TriggerOrder                 = entity.TriggerOrder,
        NewMembers                   = entity.NewMembers,
        DaysAfterJoining             = entity.DaysAfterJoining,
        MembersRebill                = entity.MembersRebill,
        LifeTimeRank                 = entity.LifeTimeRank,
        CurrentRank                  = entity.CurrentRank,
        LevelNo                      = entity.LevelNo,
        ResidualBased                = entity.ResidualBased,
        ResidualOverCommissionType   = entity.ResidualOverCommissionType,
        ResidualPercentage           = entity.ResidualPercentage,
        PersonalPoints               = entity.PersonalPoints,
        TeamPoints                   = entity.TeamPoints,
        MaxTeamPointsPerBranch       = entity.MaxTeamPointsPerBranch,
        EnrollmentTeam               = entity.EnrollmentTeam,
        MaxEnrollmentTeamPointsPerBranch = entity.MaxEnrollmentTeamPointsPerBranch,
        ExternalMembers              = entity.ExternalMembers,
        SponsoredMembers             = entity.SponsoredMembers,
        ReverseId                    = entity.ReverseId
    };

    public static CommissionType ToNewEntity(this CreateCommissionTypeDto dto) => new()
    {
        CommissionCategoryId         = dto.CommissionCategoryId,
        Name                         = dto.Name,
        Description                  = dto.Description,
        Percentage                   = dto.Percentage,
        Amount                       = dto.Amount,
        AmountPromo                  = dto.AmountPromo,
        PaymentDelayDays             = dto.PaymentDelayDays,
        IsRealTime                   = dto.IsRealTime,
        IsPaidOnSignup               = dto.IsPaidOnSignup,
        IsPaidOnRenewal              = dto.IsPaidOnRenewal,
        Cummulative                  = dto.Cummulative,
        TriggerOrder                 = dto.TriggerOrder,
        NewMembers                   = dto.NewMembers,
        DaysAfterJoining             = dto.DaysAfterJoining,
        MembersRebill                = dto.MembersRebill,
        LifeTimeRank                 = dto.LifeTimeRank,
        CurrentRank                  = dto.CurrentRank,
        LevelNo                      = dto.LevelNo,
        ResidualBased                = dto.ResidualBased,
        ResidualOverCommissionType   = dto.ResidualOverCommissionType,
        ResidualPercentage           = dto.ResidualPercentage,
        PersonalPoints               = dto.PersonalPoints,
        TeamPoints                   = dto.TeamPoints,
        MaxTeamPointsPerBranch       = dto.MaxTeamPointsPerBranch,
        EnrollmentTeam               = dto.EnrollmentTeam,
        MaxEnrollmentTeamPointsPerBranch = dto.MaxEnrollmentTeamPointsPerBranch,
        ExternalMembers              = dto.ExternalMembers,
        SponsoredMembers             = dto.SponsoredMembers,
        ReverseId                    = dto.ReverseId
    };

    public static void ApplyTo(this UpdateCommissionTypeDto dto, CommissionType entity)
    {
        entity.CommissionCategoryId         = dto.CommissionCategoryId;
        entity.Name                         = dto.Name;
        entity.Description                  = dto.Description;
        entity.Percentage                   = dto.Percentage;
        entity.Amount                       = dto.Amount;
        entity.AmountPromo                  = dto.AmountPromo;
        entity.PaymentDelayDays             = dto.PaymentDelayDays;
        entity.IsActive                     = dto.IsActive;
        entity.IsRealTime                   = dto.IsRealTime;
        entity.IsPaidOnSignup               = dto.IsPaidOnSignup;
        entity.IsPaidOnRenewal              = dto.IsPaidOnRenewal;
        entity.Cummulative                  = dto.Cummulative;
        entity.TriggerOrder                 = dto.TriggerOrder;
        entity.NewMembers                   = dto.NewMembers;
        entity.DaysAfterJoining             = dto.DaysAfterJoining;
        entity.MembersRebill                = dto.MembersRebill;
        entity.LifeTimeRank                 = dto.LifeTimeRank;
        entity.CurrentRank                  = dto.CurrentRank;
        entity.LevelNo                      = dto.LevelNo;
        entity.ResidualBased                = dto.ResidualBased;
        entity.ResidualOverCommissionType   = dto.ResidualOverCommissionType;
        entity.ResidualPercentage           = dto.ResidualPercentage;
        entity.PersonalPoints               = dto.PersonalPoints;
        entity.TeamPoints                   = dto.TeamPoints;
        entity.MaxTeamPointsPerBranch       = dto.MaxTeamPointsPerBranch;
        entity.EnrollmentTeam               = dto.EnrollmentTeam;
        entity.MaxEnrollmentTeamPointsPerBranch = dto.MaxEnrollmentTeamPointsPerBranch;
        entity.ExternalMembers              = dto.ExternalMembers;
        entity.SponsoredMembers             = dto.SponsoredMembers;
        entity.ReverseId                    = dto.ReverseId;
    }
}
