using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.CreateDocumentType;

public record CreateDocumentTypeCommand(CreateDocumentTypeRequest Request) : IRequest<Result<DocumentTypeDto>>;
