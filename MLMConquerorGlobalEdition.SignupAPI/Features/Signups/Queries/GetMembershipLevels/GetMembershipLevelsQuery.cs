using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetMembershipLevels;

public record GetMembershipLevelsQuery : IRequest<Result<IEnumerable<MembershipLevelDto>>>;
