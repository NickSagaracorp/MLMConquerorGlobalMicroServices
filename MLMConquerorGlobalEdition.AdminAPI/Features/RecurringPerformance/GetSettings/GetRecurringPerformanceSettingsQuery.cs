using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.GetSettings;

public record GetRecurringPerformanceSettingsQuery : IRequest<Result<RecurringPerformanceSettingsDto>>;
