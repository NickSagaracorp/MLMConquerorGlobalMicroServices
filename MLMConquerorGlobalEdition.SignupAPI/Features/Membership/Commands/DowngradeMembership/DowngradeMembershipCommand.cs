using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.DowngradeMembership;

public record DowngradeMembershipCommand(string MemberId, int NewMembershipLevelId, string? Reason) : IRequest<Result<bool>>;
