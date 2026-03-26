using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.UpgradeMembership;

public record UpgradeMembershipCommand(string MemberId, int NewMembershipLevelId, string? Reason) : IRequest<Result<bool>>;
