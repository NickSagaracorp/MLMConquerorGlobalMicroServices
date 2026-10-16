using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// La cuenta del alta puede entrar al portal: IsActive y correo confirmado. Hasta aquí LoginHandler
/// la rechazaba, que es lo que se quiere mientras el dinero no esté cobrado. Idempotente: con un
/// usuario ya activo no hace nada (un Re-check de un pago ya liquidado lo usa para reparar).
/// </summary>
public static class SignupUserActivation
{
    public static async Task<bool> ActivateAsync(UserManager<ApplicationUser> users, string email)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || user.IsActive) return false;

        user.IsActive       = true;
        user.EmailConfirmed = true;
        await users.UpdateAsync(user);
        return true;
    }
}
