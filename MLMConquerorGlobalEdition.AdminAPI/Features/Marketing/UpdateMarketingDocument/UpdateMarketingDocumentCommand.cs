using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UpdateMarketingDocument;

public record UpdateMarketingDocumentCommand(int Id, UpdateMarketingDocumentRequest Request)
    : IRequest<Result<MarketingDocumentDto>>;
