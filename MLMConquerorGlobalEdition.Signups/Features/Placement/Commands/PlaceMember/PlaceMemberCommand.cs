using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Signups.Features.Placement.Commands.PlaceMember;

public record PlaceMemberCommand(string MemberId, string PlaceUnderMemberId, string Side) : IRequest<Result<bool>>;
