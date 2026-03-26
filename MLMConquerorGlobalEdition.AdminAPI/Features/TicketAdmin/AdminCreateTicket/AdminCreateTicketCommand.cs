using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminCreateTicket;

public record AdminCreateTicketCommand(AdminCreateTicketRequest Request) : IRequest<Result<AdminTicketDto>>;
