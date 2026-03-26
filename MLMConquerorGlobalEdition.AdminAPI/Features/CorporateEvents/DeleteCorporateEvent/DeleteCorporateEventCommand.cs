using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.DeleteCorporateEvent;

public record DeleteCorporateEventCommand(string EventId) : IRequest<Result<bool>>;
