using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminUpdateTicket;

public record AdminUpdateTicketCommand(string TicketId, AdminUpdateTicketRequest Request)
    : IRequest<Result<AdminTicketDto>>;
