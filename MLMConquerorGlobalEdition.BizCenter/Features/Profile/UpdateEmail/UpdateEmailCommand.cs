using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateEmail;

public record UpdateEmailCommand(UpdateEmailRequest Request) : IRequest<Result<bool>>;
