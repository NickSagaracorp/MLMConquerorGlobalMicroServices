using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTicket;

public record GetTicketQuery(string TicketId) : IRequest<Result<TicketDetailDto>>;
