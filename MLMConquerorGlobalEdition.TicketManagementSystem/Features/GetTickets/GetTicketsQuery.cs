using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTickets;

public record GetTicketsQuery(PagedRequest Page, string? StatusFilter) : IRequest<Result<PagedResult<TicketDto>>>;
