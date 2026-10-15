using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.Repository.Services.Security;

/// <summary>
/// Idioma de las plantillas de seguridad (código de 2FA, step-up, aviso de cambio de factor). El
/// personal no tiene MemberProfile: devuelve null y el transporte cae a "en".
///
/// Vive en Repository y no en SignupAPI porque el BizCenter también avisa al dueño cuando cambia su
/// correo, y el idioma de ese aviso tiene que salir de la misma regla.
/// </summary>
public static class TwoFactorLanguage
{
    public static async Task<string?> ResolveAsync(AppDbContext db, ApplicationUser user, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(user.MemberProfileId))
            return null;

        var language = await db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == user.MemberProfileId)
            .Select(m => m.DefaultLanguage)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(language) ? null : language;
    }
}
