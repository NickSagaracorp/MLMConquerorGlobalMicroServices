using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.DeleteCertificate;

/// <summary>Admin-only: removes a corrupt/incorrect certificate (file + stored URL).</summary>
public record DeleteCertificateCommand(string MemberRankHistoryId) : IRequest<Result<bool>>;
