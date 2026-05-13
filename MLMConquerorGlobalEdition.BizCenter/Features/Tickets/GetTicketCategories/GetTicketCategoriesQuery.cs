using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTicketCategories;

public record GetTicketCategoriesQuery() : IRequest<Result<IEnumerable<TicketCategoryDto>>>;
