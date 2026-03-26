using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateProfile;

public record UpdateProfileCommand(UpdateProfileRequest Request) : IRequest<Result<ProfileDto>>;
