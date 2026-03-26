using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.CreateTicket;

public record CreateTicketCommand(CreateTicketRequest Request) : IRequest<Result<TicketDto>>;
