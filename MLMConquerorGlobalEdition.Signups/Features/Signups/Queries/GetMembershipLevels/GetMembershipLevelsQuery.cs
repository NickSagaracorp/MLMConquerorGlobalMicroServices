using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Queries.GetMembershipLevels;

public record GetMembershipLevelsQuery : IRequest<Result<IEnumerable<MembershipLevelDto>>>;
