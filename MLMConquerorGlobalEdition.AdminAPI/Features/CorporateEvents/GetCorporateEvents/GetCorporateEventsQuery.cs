using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.GetCorporateEvents;

public record GetCorporateEventsQuery(PagedRequest Page) : IRequest<Result<PagedResult<CorporateEventDto>>>;
