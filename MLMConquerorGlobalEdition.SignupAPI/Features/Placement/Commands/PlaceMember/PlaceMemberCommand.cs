using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.PlaceMember;

/// <param name="ByStaff">El que coloca es personal (admin o soporte): no se le aplica la regla de red (I8).</param>
public record PlaceMemberCommand(string MemberId, string PlaceUnderMemberId, string Side, bool ByStaff = false) : IRequest<Result<bool>>;
