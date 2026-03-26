using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.DeleteTokenTypeCommission;

public record DeleteTokenTypeCommissionCommand(int Id) : IRequest<Result<bool>>;
