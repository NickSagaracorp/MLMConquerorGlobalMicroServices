using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.PlaceMember;

public record PlaceMemberCommand(string MemberId, string PlaceUnderMemberId, string Side) : IRequest<Result<bool>>;
