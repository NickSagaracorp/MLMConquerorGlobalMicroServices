using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.UpdateSettings;

public record UpdateRecurringPerformanceSettingsCommand(
    UpdateRecurringPerformanceSettingsRequest Request
) : IRequest<Result<RecurringPerformanceSettingsDto>>;
