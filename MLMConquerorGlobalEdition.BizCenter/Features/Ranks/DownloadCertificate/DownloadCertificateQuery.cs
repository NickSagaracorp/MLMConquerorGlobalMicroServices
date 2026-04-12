using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.DownloadCertificate;

public record DownloadCertificateQuery(string RankHistoryId) : IRequest<Result<string>>;
