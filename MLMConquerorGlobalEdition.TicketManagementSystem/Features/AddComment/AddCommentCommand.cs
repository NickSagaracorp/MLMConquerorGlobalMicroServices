using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.AddComment;

public record AddCommentCommand(string TicketId, AddCommentRequest Request) : IRequest<Result<TicketCommentDto>>;
