using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.AddTicketComment;

public record AddTicketCommentCommand(string TicketId, AddCommentRequest Request) : IRequest<Result<TicketCommentDto>>;
