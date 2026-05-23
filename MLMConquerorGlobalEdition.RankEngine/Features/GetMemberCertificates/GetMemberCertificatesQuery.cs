using MediatR;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GetMemberCertificates;

/// <summary>Admin-only: lists a member's certificate-eligible rank achievements.</summary>
public record GetMemberCertificatesQuery(string MemberId)
    : IRequest<Result<List<MemberCertificateDto>>>;
