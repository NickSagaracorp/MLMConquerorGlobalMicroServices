using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTicket;

public record GetTicketQuery(string TicketId) : IRequest<Result<TicketDetailDto>>;
