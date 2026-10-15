using MediatR;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;

/// <summary>
/// Canjea el código por el permiso. La librería comprueba propósito y operación dentro del reto,
/// aplica el límite de intentos y quema el reto; aquí falta solo que el reto sea de quien llama.
/// </summary>
public class VerifyStepUpHandler : IRequestHandler<VerifyStepUpCommand, Result<StepUpGrantResponse>>
{
    private readonly ITwoFactorService   _twoFactor;
    private readonly IStepUpGrantService _grants;

    public VerifyStepUpHandler(ITwoFactorService twoFactor, IStepUpGrantService grants)
    {
        _twoFactor = twoFactor;
        _grants    = grants;
    }

    public async Task<Result<StepUpGrantResponse>> Handle(VerifyStepUpCommand command, CancellationToken ct)
    {
        var req = command.Request;
        if (!StepUpOperations.IsKnown(req.OperationKey))
            return Result<StepUpGrantResponse>.Failure("UNKNOWN_OPERATION", "Operación de step-up desconocida.");

        var verified = await _twoFactor.VerifyAsync(
            req.ChallengeToken, req.Code, TwoFactorPurpose.StepUp, req.OperationKey, ct);
        if (!verified.IsSuccess)
            return Result<StepUpGrantResponse>.Failure(verified.ErrorCode!, verified.Error!);

        // Mismo corte que VerifyPhoneHandler: un reto ajeno no puede autorizar a esta cuenta.
        if (!string.Equals(verified.Value!.UserId, command.UserId, StringComparison.Ordinal))
            return Result<StepUpGrantResponse>.Failure("INVALID_CHALLENGE", "Este challenge no es de esta cuenta.");

        return Result<StepUpGrantResponse>.Success(new StepUpGrantResponse
        {
            StepUpToken = _grants.Issue(command.UserId, req.OperationKey)
        });
    }
}
