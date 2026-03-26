using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminResolveTicket;

public record AdminResolveTicketCommand(string TicketId, AdminResolveTicketRequest Request)
    : IRequest<Result<AdminTicketDto>>;
