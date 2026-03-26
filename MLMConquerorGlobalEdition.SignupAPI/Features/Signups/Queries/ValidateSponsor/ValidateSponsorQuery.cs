using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.ValidateSponsor;

public record ValidateSponsorQuery(string SponsorMemberId) : IRequest<Result<SponsorInfoResponse>>;
