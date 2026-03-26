using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetPriorities;

public record GetPrioritiesQuery : IRequest<Result<IEnumerable<PriorityDto>>>;
