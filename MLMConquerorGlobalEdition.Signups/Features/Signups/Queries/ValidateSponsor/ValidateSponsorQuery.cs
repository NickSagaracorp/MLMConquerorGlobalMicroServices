using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Queries.ValidateSponsor;

public record ValidateSponsorQuery(string SponsorMemberId) : IRequest<Result<SponsorInfoResponse>>;
