using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.GetAdminTicketDetail;

public record GetAdminTicketDetailQuery(string TicketId) : IRequest<Result<AdminTicketDetailDto>>;
