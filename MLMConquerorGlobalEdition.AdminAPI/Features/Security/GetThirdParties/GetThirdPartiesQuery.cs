using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetThirdParties;

public record GetThirdPartiesQuery : IRequest<Result<IEnumerable<string>>>;
