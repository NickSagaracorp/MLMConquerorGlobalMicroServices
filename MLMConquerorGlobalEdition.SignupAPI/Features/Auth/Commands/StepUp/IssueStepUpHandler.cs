using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;

/// <summary>
/// Manda el código de step-up por el canal preferido del usuario. El tope de emisiones y la
/// elección de destino los aplica <see cref="ITwoFactorService"/>: un SMS sin confirmar no es un
/// canal para step-up.
/// </summary>
public class IssueStepUpHandler : IRequestHandler<IssueStepUpCommand, Result<StepUpChallengeResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITwoFactorService            _twoFactor;
    private readonly AppDbContext                 _db;
    private readonly IConfiguration               _config;
    private readonly IDateTimeProvider            _dateTime;
    private readonly ILogger<IssueStepUpHandler>  _logger;

    public IssueStepUpHandler(
        UserManager<ApplicationUser> userManager,
        ITwoFactorService            twoFactor,
        AppDbContext                 db,
        IConfiguration               config,
        IDateTimeProvider            dateTime,
        ILogger<IssueStepUpHandler>  logger)
    {
        _userManager = userManager;
        _twoFactor   = twoFactor;
        _db          = db;
        _config      = config;
        _dateTime    = dateTime;
        _logger      = logger;
    }

    public async Task<Result<StepUpChallengeResponse>> Handle(IssueStepUpCommand command, CancellationToken ct)
    {
        var op = command.Request.OperationKey;
        if (!StepUpOperations.IsKnown(op))
            return Result<StepUpChallengeResponse>.Failure("UNKNOWN_OPERATION", "Operación de step-up desconocida.");

        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Step-up no emitido: usuario {UserId} inexistente o inactivo (operación {Operation}).",
                command.UserId, op);
            return Result<StepUpChallengeResponse>.Failure("USER_NOT_FOUND", "User not found.");
        }

        // Antes de emitir y no solo al canjear: un código que no se va a poder canjear es un SMS
        // pagado para nada, y además le confirmaría al atacante a qué canal llega ya.
        if (StepUpCooldown.ActiveUntil(user, _config, _dateTime.Now) is { } until)
        {
            _logger.LogWarning(
                "Step-up no emitido por espera tras cambio de factor: usuario {UserId}, operación {Operation}, hasta {Until}.",
                user.Id, op, until);
            return StepUpCooldown.Failure<StepUpChallengeResponse>(until);
        }

        var language = await TwoFactorLanguage.ResolveAsync(_db, user, ct);
        var issued = await _twoFactor.IssueAsync(user, TwoFactorPurpose.StepUp, op, languageCode: language, ct: ct);
        if (!issued.IsSuccess)
        {
            _logger.LogWarning("Step-up no emitido: usuario {UserId}, operación {Operation}, motivo {ErrorCode}.",
                user.Id, op, issued.ErrorCode);
            return Result<StepUpChallengeResponse>.Failure(issued.ErrorCode!, issued.Error!);
        }

        // Nunca el reto ni el código: solo quién, para qué y por dónde.
        _logger.LogInformation("Step-up emitido: usuario {UserId}, operación {Operation}, canal {Channel}.",
            user.Id, op, issued.Value!.Channel);

        return Result<StepUpChallengeResponse>.Success(new StepUpChallengeResponse
        {
            ChallengeToken = issued.Value!.ChallengeToken,
            Channel        = issued.Value.Channel.ToString(),
            MaskedTarget   = issued.Value.MaskedTarget,
            ExpiresAt      = issued.Value.ExpiresAt
        });
    }
}
