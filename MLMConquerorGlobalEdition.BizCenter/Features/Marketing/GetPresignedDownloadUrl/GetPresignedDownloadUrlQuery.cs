using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Marketing.GetPresignedDownloadUrl;

public record GetPresignedDownloadUrlQuery(int DocumentId) : IRequest<Result<string>>;
