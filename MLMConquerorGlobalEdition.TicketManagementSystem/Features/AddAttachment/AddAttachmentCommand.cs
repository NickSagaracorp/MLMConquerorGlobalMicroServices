using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.AddAttachment;

public record AddAttachmentCommand(string TicketId, AddAttachmentRequest Request) : IRequest<Result<TicketAttachmentDto>>;
