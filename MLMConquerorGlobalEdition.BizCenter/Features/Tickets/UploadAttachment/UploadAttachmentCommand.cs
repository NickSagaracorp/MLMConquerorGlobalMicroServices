using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.UploadAttachment;

/// <summary>
/// Persists an already-validated (size + extension) ticket attachment file to disk and
/// records the row in <c>TicketAttachments</c>. The controller hands over the bytes —
/// the handler does not touch <see cref="Microsoft.AspNetCore.Http.IFormFile"/> directly,
/// keeping the handler free of HTTP plumbing and trivially unit-testable.
/// </summary>
public record UploadAttachmentCommand(
    string TicketId,
    string OriginalFileName,
    string ContentType,
    long   FileSizeBytes,
    byte[] Content) : IRequest<Result<TicketAttachmentDto>>;
