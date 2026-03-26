using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.CreateTicket;

public record CreateTicketCommand(CreateTicketRequest Request) : IRequest<Result<TicketDto>>;
