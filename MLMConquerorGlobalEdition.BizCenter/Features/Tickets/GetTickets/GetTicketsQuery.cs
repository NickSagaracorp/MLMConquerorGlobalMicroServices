using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTickets;

public record GetTicketsQuery(int Page, int PageSize) : IRequest<Result<PagedResult<TicketDto>>>;
