using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.MergeTicket;

public record MergeTicketCommand(string TicketId, MergeTicketRequest Request) : IRequest<Result<bool>>;
