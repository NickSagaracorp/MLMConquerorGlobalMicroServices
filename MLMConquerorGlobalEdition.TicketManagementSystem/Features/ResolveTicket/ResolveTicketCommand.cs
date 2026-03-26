using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.ResolveTicket;

public record ResolveTicketCommand(string TicketId, ResolveTicketRequest Request) : IRequest<Result<bool>>;
