using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminAddComment;

public record AdminAddCommentCommand(string TicketId, AdminAddCommentRequest Request)
    : IRequest<Result<AdminTicketCommentDto>>;
