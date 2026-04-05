using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.EscalateTicket;

public record EscalateTicketCommand(string TicketId, EscalateTicketRequest Request)
    : IRequest<Result<bool>>;
