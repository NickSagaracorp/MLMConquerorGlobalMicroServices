using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdatePassword;

public class UpdatePasswordHandler : IRequestHandler<UpdatePasswordCommand, Result<bool>>
{
    public Task<Result<bool>> Handle(UpdatePasswordCommand command, CancellationToken ct)
    {
        // Password changes are managed via the Identity service (out of scope for this API).
        return Task.FromResult(
            Result<bool>.Failure("NOT_IMPLEMENTED", "Password changes are managed via the Identity service."));
    }
}
