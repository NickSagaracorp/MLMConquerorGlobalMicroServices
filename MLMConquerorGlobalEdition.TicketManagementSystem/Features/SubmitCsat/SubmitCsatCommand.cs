using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.SubmitCsat;

public record SubmitCsatCommand(string TicketId, SubmitCsatRequest Request) : IRequest<Result<bool>>;
