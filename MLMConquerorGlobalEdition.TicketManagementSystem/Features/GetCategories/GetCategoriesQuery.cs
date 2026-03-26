using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetCategories;

public record GetCategoriesQuery : IRequest<Result<IEnumerable<TicketCategoryDto>>>;
