using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Queries.ValidateReplicateSite;

public record ValidateReplicateSiteQuery(string Slug) : IRequest<Result<bool>>;
