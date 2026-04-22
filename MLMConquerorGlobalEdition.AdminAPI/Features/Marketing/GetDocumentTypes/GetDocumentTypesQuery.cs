using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.GetDocumentTypes;

public record GetDocumentTypesQuery : IRequest<Result<List<DocumentTypeDto>>>;
