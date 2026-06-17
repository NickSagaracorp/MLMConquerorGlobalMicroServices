using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.UnblockFingerprint;

/// <summary>
/// Marks every matching SignupRiskFingerprint row as Cleared so it stops counting toward the
/// duplicate-threshold guard. Returns the number of rows cleared.
/// </summary>
public record UnblockFingerprintCommand(UnblockFingerprintRequest Request) : IRequest<Result<int>>;
