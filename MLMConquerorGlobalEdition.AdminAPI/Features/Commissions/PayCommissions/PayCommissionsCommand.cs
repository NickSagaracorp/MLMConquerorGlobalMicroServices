using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.PayCommissions;

public record PayCommissionsCommand(PayCommissionsRequest Request) : IRequest<Result<int>>;
