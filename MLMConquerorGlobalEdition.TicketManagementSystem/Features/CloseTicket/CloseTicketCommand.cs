using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.CloseTicket;

public record CloseTicketCommand(string TicketId) : IRequest<Result<bool>>;
