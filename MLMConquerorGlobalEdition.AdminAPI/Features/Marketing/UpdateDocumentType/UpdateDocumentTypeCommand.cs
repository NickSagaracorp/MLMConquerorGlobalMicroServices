using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UpdateDocumentType;

public record UpdateDocumentTypeCommand(int Id, UpdateDocumentTypeRequest Request) : IRequest<Result<DocumentTypeDto>>;
