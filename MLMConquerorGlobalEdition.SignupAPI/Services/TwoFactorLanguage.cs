using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Idioma de la plantilla del código. El personal no tiene MemberProfile: devuelve null y la
/// librería cae a "en".
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
