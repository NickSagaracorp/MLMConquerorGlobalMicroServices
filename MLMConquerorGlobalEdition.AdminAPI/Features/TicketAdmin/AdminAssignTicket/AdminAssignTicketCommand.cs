using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminAssignTicket;

public record AdminAssignTicketCommand(string TicketId, AdminAssignTicketRequest Request)
    : IRequest<Result<AdminTicketDto>>;
