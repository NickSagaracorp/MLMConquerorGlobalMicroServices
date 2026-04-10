using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.CommissionEngine.Mappings;

public static class CommissionEngineMappingExtensions
{
    public static CommissionTypeResponse ToResponse(this CommissionType entity) => new()
    {
        Id                 = entity.Id,
        Name               = entity.Name,
        Description        = entity.Description,
        CategoryName       = entity.Category?.Name,
        Percentage         = entity.Percentage,
        PaymentDelayDays   = entity.PaymentDelayDays,
        IsActive           = entity.IsActive,
        IsRealTime         = entity.IsRealTime,
        IsPaidOnSignup     = entity.IsPaidOnSignup,
        IsPaidOnRenewal    = entity.IsPaidOnRenewal,
        ResidualBased      = entity.ResidualBased,
        ResidualPercentage = entity.ResidualPercentage,
        LevelNo            = entity.LevelNo,
        PersonalPoints     = entity.PersonalPoints,
        TeamPoints         = entity.TeamPoints
    };

    public static CommissionEarningResponse ToResponse(this CommissionEarning entity) => new()
    {
        Id                 = entity.Id,
        BeneficiaryMemberId = entity.BeneficiaryMemberId,
        SourceMemberId     = entity.SourceMemberId,
        SourceOrderId      = entity.SourceOrderId,
        CommissionTypeId   = entity.CommissionTypeId,
        CommissionTypeName = entity.CommissionType?.Name ?? string.Empty,
        Amount             = entity.Amount,
        Status             = entity.Status.ToString(),
        EarnedDate         = entity.EarnedDate,
        PaymentDate        = entity.PaymentDate,
        PeriodDate         = entity.PeriodDate,
        IsManualEntry      = entity.IsManualEntry,
        Notes              = entity.Notes
    };
}
