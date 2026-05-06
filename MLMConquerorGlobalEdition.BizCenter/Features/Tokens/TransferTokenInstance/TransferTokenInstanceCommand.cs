using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.TransferTokenInstance;

public record TransferTokenInstanceCommand(
    string TokenCode,
    string RecipientMemberId,
    string? Notes = null) : IRequest<Result<TransferTokenInstanceResponse>>;

public record TransferTokenInstanceResponse(
    string TokenCode,
    string RecipientMemberId,
    DateTime TransferredAt);
