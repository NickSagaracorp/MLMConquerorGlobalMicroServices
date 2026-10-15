using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.Repository.Interceptors;

/// <summary>
/// Sella <see cref="ApplicationUser.SecurityFactorsChangedAt"/> cada vez que se guarda un cambio en
/// por dónde puede recibir un código la cuenta: correo, teléfono de SMS, su confirmación, el canal
/// preferido, el enrolamiento del autenticador o su clave.
/// </summary>
/// <remarks>
/// POR QUÉ AQUÍ Y NO EN CADA MANEJADOR. Los cambios de factor viven en media docena de manejadores
/// repartidos entre SignupAPI, BizCenter y AdminAPI, y mañana habrá otro. Si la marca dependiera de
/// que cada uno se acuerde de escribirla, la espera del step-up tendría un agujero por cada
/// manejador olvidado —y el que se olvida no falla en ninguna prueba, simplemente no sella—. Al
/// guardar, en cambio, no hay forma de cambiar el correo sin pasar por aquí.
///
/// POR QUÉ COMPARA VALORES Y NO <c>IsModified</c>. <c>UserStore.UpdateAsync</c> de Identity llama a
/// <c>Context.Update(user)</c>, que marca TODAS las columnas como modificadas. Con
/// <c>IsModified</c> cualquier <c>UpdateAsync</c> —un inicio de sesión que solo toca
/// <c>LastLoginAt</c>— sellaría la cuenta y la dejaría sin step-up un día entero. Se compara el
/// valor original con el actual.
///
/// LA CLAVE DEL AUTENTICADOR no está en <see cref="ApplicationUser"/>: Identity la guarda en
/// <c>AspNetUserTokens</c> (proveedor <c>[AspNetUserStore]</c>, nombre <c>AuthenticatorKey</c>).
/// Reemplazarla deja inservible el autenticador anterior y habilita uno nuevo, así que también
/// sella, aunque el enrolamiento no se haya confirmado todavía: es lo conservador.
///
/// UNA CUENTA RECIÉN CREADA NO SE SELLA. El alta da de alta el correo, pero no hay un factor
/// anterior que alguien pueda haber desviado: la espera protege al dueño de una cuenta que ya
/// existía frente a quien le robó la sesión. Sellar el alta solo dejaría a todo miembro nuevo un
/// día sin poder elegir dónde cobrar. Lo que el miembro configure después —su teléfono, su
/// autenticador— sí sella, como cualquier otro cambio.
/// </remarks>
public sealed class SecurityFactorsInterceptor : SaveChangesInterceptor
{
    // Lo que define a dónde llega un código. TwoFactorPhoneLast4 no está porque se deriva del
    // teléfono cifrado; PasswordHash tampoco, porque cambiar la contraseña no reapunta ningún
    // canal (y ya revoca las sesiones abiertas, ver SessionRevocation).
    private static readonly string[] FactorProperties =
    {
        nameof(ApplicationUser.Email),
        nameof(ApplicationUser.NormalizedEmail),
        nameof(ApplicationUser.TwoFactorPhoneEncrypted),
        nameof(ApplicationUser.TwoFactorPhoneConfirmed),
        nameof(ApplicationUser.PreferredTwoFactorChannel),
        nameof(ApplicationUser.TwoFactorEnrolledAt)
    };

    // Los nombres que usa UserStore de Identity para la clave del autenticador. Son constantes
    // internas de Identity; si algún día cambian, la prueba de la clave del autenticador lo dirá.
    internal const string IdentityStoreProvider = "[AspNetUserStore]";
    internal const string AuthenticatorKeyName  = "AuthenticatorKey";

    private readonly Func<DateTime> _now;

    /// <param name="now">
    /// Solo para pruebas. Por defecto <see cref="DateTime.Now"/>: hora de servidor, igual que el
    /// resto de marcas de negocio y que <see cref="AuditInterceptor"/>.
    /// </param>
    public SecurityFactorsInterceptor(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.Now);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            foreach (var userId in ChangedUserIds(context))
            {
                var user = FindTracked(context, userId) ?? context.Set<ApplicationUser>().Find(userId);
                if (user is not null) Stamp(context, user);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            foreach (var userId in ChangedUserIds(context))
            {
                var user = FindTracked(context, userId)
                           ?? await context.Set<ApplicationUser>().FindAsync(new object[] { userId }, cancellationToken);
                if (user is not null) Stamp(context, user);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Usuarios con un factor cambiado en este guardado. Se materializa antes de sellar: sellar
    /// toca el ChangeTracker y no se puede hacer mientras se recorre.
    /// </summary>
    private static List<string> ChangedUserIds(DbContext context)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case ApplicationUser user when entry.State == EntityState.Modified && FactorChanged(entry):
                    ids.Add(user.Id);
                    break;

                // La fila de la clave se añade la primera vez, se modifica al regenerarla y se
                // borra al quitarla: los tres reapuntan el autenticador.
                case IdentityUserToken<string> token
                    when token.LoginProvider == IdentityStoreProvider
                         && token.Name == AuthenticatorKeyName
                         && (entry.State is EntityState.Added or EntityState.Deleted
                             || (entry.State == EntityState.Modified && ValueChanged(entry.Property(nameof(token.Value))))):
                    ids.Add(token.UserId);
                    break;
            }
        }

        return ids.ToList();
    }

    private static bool FactorChanged(EntityEntry entry) =>
        FactorProperties.Any(name => ValueChanged(entry.Property(name)));

    private static bool ValueChanged(PropertyEntry property) =>
        property.IsModified && !Equals(property.OriginalValue, property.CurrentValue);

    private static ApplicationUser? FindTracked(DbContext context, string userId) =>
        context.ChangeTracker.Entries<ApplicationUser>()
               .Select(e => e.Entity)
               .FirstOrDefault(u => u.Id == userId);

    private void Stamp(DbContext context, ApplicationUser user)
    {
        // Una cuenta que se está creando en este mismo guardado no se sella: ver la nota de la clase.
        var entry = context.Entry(user);
        if (entry.State is EntityState.Added or EntityState.Deleted)
            return;

        entry.Property(nameof(ApplicationUser.SecurityFactorsChangedAt)).CurrentValue = _now();
    }
}
