using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Marketing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Marketing.GetMarketingDocuments;

public record GetMarketingDocumentsQuery(int? DocumentTypeId = null) : IRequest<Result<List<MarketingDocumentSummaryDto>>>;
