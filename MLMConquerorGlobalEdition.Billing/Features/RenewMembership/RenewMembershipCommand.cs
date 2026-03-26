using MediatR;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.RenewMembership;

public record RenewMembershipCommand(MembershipRenewalRequest Request) : IRequest<Result<ChargeResponse>>;
