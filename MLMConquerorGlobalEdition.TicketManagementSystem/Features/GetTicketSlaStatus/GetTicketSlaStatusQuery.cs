using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTicketSlaStatus;

public record GetTicketSlaStatusQuery(string TicketId) : IRequest<Result<SlaStatusDto>>;
