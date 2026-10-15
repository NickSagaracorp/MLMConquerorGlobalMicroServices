using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
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

    public IssueStepUpHandler(UserManager<ApplicationUser> userManager, ITwoFactorService twoFactor, AppDbContext db)
    {
        _userManager = userManager;
        _twoFactor   = twoFactor;
        _db          = db;
    }

    public async Task<Result<StepUpChallengeResponse>> Handle(IssueStepUpCommand command, CancellationToken ct)
    {
        var op = command.Request.OperationKey;
        if (!StepUpOperations.IsKnown(op))
            return Result<StepUpChallengeResponse>.Failure("UNKNOWN_OPERATION", "Operación de step-up desconocida.");

        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<StepUpChallengeResponse>.Failure("USER_NOT_FOUND", "User not found.");

        var language = await TwoFactorLanguage.ResolveAsync(_db, user, ct);
        var issued = await _twoFactor.IssueAsync(user, TwoFactorPurpose.StepUp, op, languageCode: language, ct: ct);
        if (!issued.IsSuccess)
            return Result<StepUpChallengeResponse>.Failure(issued.ErrorCode!, issued.Error!);

        return Result<StepUpChallengeResponse>.Success(new StepUpChallengeResponse
        {
            ChallengeToken = issued.Value!.ChallengeToken,
            Channel        = issued.Value.Channel.ToString(),
            MaskedTarget   = issued.Value.MaskedTarget,
            ExpiresAt      = issued.Value.ExpiresAt
        });
    }
}
