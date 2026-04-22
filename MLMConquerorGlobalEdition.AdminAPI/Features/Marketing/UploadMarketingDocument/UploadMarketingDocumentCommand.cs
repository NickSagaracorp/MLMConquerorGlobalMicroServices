using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UploadMarketingDocument;

public record UploadMarketingDocumentCommand(
    string Title,
    int    DocumentTypeId,
    string LanguageCode,
    string LanguageName,
    Stream FileStream,
    string OriginalFileName,
    long   FileSizeBytes,
    string ContentType
) : IRequest<Result<MarketingDocumentDto>>;
