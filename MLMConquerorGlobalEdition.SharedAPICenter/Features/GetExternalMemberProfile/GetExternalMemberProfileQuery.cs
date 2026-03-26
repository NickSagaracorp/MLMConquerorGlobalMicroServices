using MediatR;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.GetExternalMemberProfile;

public record GetExternalMemberProfileQuery(string MemberId) : IRequest<Result<ExternalMemberProfileDto>>;
