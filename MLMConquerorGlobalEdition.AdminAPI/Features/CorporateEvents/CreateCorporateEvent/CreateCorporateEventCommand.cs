using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.CreateCorporateEvent;

public record CreateCorporateEventCommand(CreateCorporateEventRequest Request) : IRequest<Result<CorporateEventDto>>;
