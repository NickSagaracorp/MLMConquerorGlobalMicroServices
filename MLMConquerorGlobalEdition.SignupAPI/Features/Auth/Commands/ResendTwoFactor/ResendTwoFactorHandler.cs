using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResendTwoFactor;

public class ResendTwoFactorHandler : IRequestHandler<ResendTwoFactorCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly AppDbContext                   _db;
    private readonly ITwoFactorChallengeService     _twoFactor;
    private readonly IEmailService                  _email;

    public ResendTwoFactorHandler(
        UserManager<ApplicationUser> userManager,
        AppDbContext                 db,
        ITwoFactorChallengeService   twoFactor,
        IEmailService                email)
    {
        _userManager = userManager;
        _db          = db;
        _twoFactor   = twoFactor;
        _email       = email;
    }

    public async Task<Result<AuthResponse>> Handle(ResendTwoFactorCommand command, CancellationToken ct)
    {
        // Allow expired challenge tokens within ResendGraceWindow so a user
        // whose original code expired can still request a fresh one without
        // re-entering credentials.
        var validation = _twoFactor.ValidateChallenge(command.Request.ChallengeToken, allowExpired: true);
        if (!validation.IsSuccess)
            return Result<AuthResponse>.Failure(validation.ErrorCode!, validation.Error!);

        var claims = validation.Value!;
        var user = await _userManager.FindByIdAsync(claims.UserId);
        if (user is null || !user.IsActive || !user.TwoFactorEnabled)
            return Result<AuthResponse>.Failure("INVALID_CHALLENGE", "Challenge token is invalid.");

        var memberId = user.MemberProfileId ?? string.Empty;
        var defaultLanguage = string.IsNullOrEmpty(memberId)
            ? null
            : await _db.MemberProfiles.AsNoTracking()
                .Where(m => m.MemberId == memberId)
                .Select(m => m.DefaultLanguage)
                .FirstOrDefaultAsync(ct);

        var code        = _twoFactor.GenerateCode();
        var codeHash    = _twoFactor.HashCode(code);
        var newChallenge = _twoFactor.IssueChallenge(user.Id, user.Email!, codeHash);

        await _email.SendAsync(
            toEmail:      user.Email!,
            toName:       user.Email!,
            languageCode: string.IsNullOrEmpty(defaultLanguage) ? "en" : defaultLanguage,
            eventType:    NotificationEvents.TwoFactorCode,
            variables:    new Dictionary<string, string>
            {
                ["Code"]             = code,
                ["ExpiresInMinutes"] = ((int)_twoFactor.ChallengeLifetime.TotalMinutes).ToString()
            },
            ct: ct);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId            = user.Id,
            Email             = user.Email!,
            RequiresTwoFactor = true,
            ChallengeToken    = newChallenge,
            MaskedEmail       = _twoFactor.MaskEmail(user.Email!)
        });
    }
}
