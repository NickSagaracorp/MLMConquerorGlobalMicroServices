using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.GetAdminTickets;

public record GetAdminTicketsQuery(PagedRequest Page) : IRequest<Result<PagedResult<AdminTicketDto>>>;
