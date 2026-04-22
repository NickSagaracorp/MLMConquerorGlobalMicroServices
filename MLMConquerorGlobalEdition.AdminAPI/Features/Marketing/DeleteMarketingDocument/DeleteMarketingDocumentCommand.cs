using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.DeleteMarketingDocument;

public record DeleteMarketingDocumentCommand(int Id) : IRequest<Result<bool>>;
