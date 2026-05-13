using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminUploadAttachment;

/// <summary>
/// Persists an admin/staff-uploaded ticket attachment file to disk and records it in
/// <c>TicketAttachments</c>. Mirrors the BizCenter <c>UploadAttachmentCommand</c> shape
/// but does not require ticket ownership — admins can attach to any ticket.
/// </summary>
public record AdminUploadAttachmentCommand(
    string TicketId,
    string OriginalFileName,
    string ContentType,
    long   FileSizeBytes,
    byte[] Content) : IRequest<Result<AdminTicketAttachmentDto>>;
