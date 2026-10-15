using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;

/// <summary>
/// Canjea el código por el permiso. La librería comprueba propósito y operación dentro del reto,
/// aplica el límite de intentos y quema el reto; aquí falta que el reto sea de quien llama, que la
/// cuenta siga activa y que no esté en la espera tras un cambio de factor.
/// </summary>
public class VerifyStepUpHandler : IRequestHandler<VerifyStepUpCommand, Result<StepUpGrantResponse>>
{
    private readonly ITwoFactorService            _twoFactor;
    private readonly IStepUpGrantService          _grants;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration               _config;
    private readonly IDateTimeProvider            _dateTime;
    private readonly ILogger<VerifyStepUpHandler> _logger;

    public VerifyStepUpHandler(
        ITwoFactorService            twoFactor,
        IStepUpGrantService          grants,
        UserManager<ApplicationUser> userManager,
        IConfiguration               config,
        IDateTimeProvider            dateTime,
        ILogger<VerifyStepUpHandler> logger)
    {
        _twoFactor   = twoFactor;
        _grants      = grants;
        _userManager = userManager;
        _config      = config;
        _dateTime    = dateTime;
        _logger      = logger;
    }

    public async Task<Result<StepUpGrantResponse>> Handle(VerifyStepUpCommand command, CancellationToken ct)
    {
        var req = command.Request;
        if (!StepUpOperations.IsKnown(req.OperationKey))
            return Result<StepUpGrantResponse>.Failure("UNKNOWN_OPERATION", "Operación de step-up desconocida.");

        // Se recarga la cuenta en vez de fiarse de la emisión: entre el reto y el canje (minutos)
        // pueden haberla desactivado o haberle cambiado un factor. Va antes de verificar para no
        // gastar el reto ni un intento en un canje que de todas formas se va a rechazar.
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Step-up no canjeado: usuario {UserId} inexistente o inactivo (operación {Operation}).",
                command.UserId, req.OperationKey);
            return Result<StepUpGrantResponse>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (StepUpCooldown.ActiveUntil(user, _config, _dateTime.Now) is { } until)
        {
            _logger.LogWarning(
                "Step-up no canjeado por espera tras cambio de factor: usuario {UserId}, operación {Operation}, hasta {Until}.",
                user.Id, req.OperationKey, until);
            return StepUpCooldown.Failure<StepUpGrantResponse>(until);
        }

        var verified = await _twoFactor.VerifyAsync(
            req.ChallengeToken, req.Code, TwoFactorPurpose.StepUp, req.OperationKey, ct);
        if (!verified.IsSuccess)
        {
            // INVALID_CHALLENGE y TOO_MANY_ATTEMPTS son los que delatan a alguien probando códigos
            // o repitiendo un reto; un CODE_INVALID suelto suele ser un error al teclear.
            _logger.LogWarning("Step-up no canjeado: usuario {UserId}, operación {Operation}, motivo {ErrorCode}.",
                command.UserId, req.OperationKey, verified.ErrorCode);
            return Result<StepUpGrantResponse>.Failure(verified.ErrorCode!, verified.Error!);
        }

        // Mismo corte que VerifyPhoneHandler: un reto ajeno no puede autorizar a esta cuenta.
        if (!string.Equals(verified.Value!.UserId, command.UserId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Step-up no canjeado: reto de {ChallengeUserId} presentado con la sesión de {UserId} (operación {Operation}), motivo INVALID_CHALLENGE.",
                verified.Value.UserId, command.UserId, req.OperationKey);
            return Result<StepUpGrantResponse>.Failure("INVALID_CHALLENGE", "Este challenge no es de esta cuenta.");
        }

        _logger.LogInformation("Step-up canjeado: usuario {UserId}, operación {Operation}.",
            command.UserId, req.OperationKey);

        return Result<StepUpGrantResponse>.Success(new StepUpGrantResponse
        {
            StepUpToken = _grants.Issue(command.UserId, req.OperationKey)
        });
    }
}
