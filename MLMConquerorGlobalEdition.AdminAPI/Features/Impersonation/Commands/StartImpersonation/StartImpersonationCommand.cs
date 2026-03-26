using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StartImpersonation;

public record StartImpersonationCommand(
    string AdminUserId,
    IEnumerable<string> AdminRoles,
    string TargetMemberId) : IRequest<Result<StartImpersonationResult>>;

public record StartImpersonationResult(
    string AccessToken,
    string MemberId,
    string MemberName,
    bool IsReadOnly,
    DateTime ExpiresAt);
