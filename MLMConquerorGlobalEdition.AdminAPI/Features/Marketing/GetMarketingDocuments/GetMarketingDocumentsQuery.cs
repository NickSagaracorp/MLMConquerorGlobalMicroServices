using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.GetMarketingDocuments;

public record GetMarketingDocumentsQuery(int? DocumentTypeId = null, bool? IsActive = null)
    : IRequest<Result<List<MarketingDocumentDto>>>;
