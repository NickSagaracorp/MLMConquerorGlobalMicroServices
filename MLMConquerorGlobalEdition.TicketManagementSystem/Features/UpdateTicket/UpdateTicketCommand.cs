using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.UpdateTicket;

public record UpdateTicketCommand(string TicketId, UpdateTicketRequest Request) : IRequest<Result<bool>>;
