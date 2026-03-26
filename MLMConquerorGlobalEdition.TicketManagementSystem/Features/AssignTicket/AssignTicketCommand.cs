using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.AssignTicket;

public record AssignTicketCommand(string TicketId, AssignTicketRequest Request) : IRequest<Result<bool>>;
