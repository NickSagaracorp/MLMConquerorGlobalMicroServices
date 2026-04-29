using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateReplicateSite;

public record UpdateReplicateSiteCommand(UpdateReplicateSiteRequest Request) : IRequest<Result<string>>;
