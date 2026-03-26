using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.UpdateCorporateEvent;

public record UpdateCorporateEventCommand(string EventId, UpdateCorporateEventRequest Request) : IRequest<Result<CorporateEventDto>>;
