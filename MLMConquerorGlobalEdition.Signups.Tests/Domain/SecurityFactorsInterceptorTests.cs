using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Domain;

/// <summary>
/// La marca de cambio de factor se sella al guardar, no en los manejadores. Estas pruebas pasan por
/// el <see cref="AppDbContext"/> de verdad —con el interceptor que registra él mismo— y, donde
/// importa, por el <see cref="UserStore{TUser}"/> de Identity, porque su <c>UpdateAsync</c> marca
/// todas las columnas como modificadas y es justo lo que haría sellar de más.
/// </summary>
public class SecurityFactorsInterceptorTests
{
    private const string UserId = "user-001";

    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(_dbName).Options);

    private async Task SeedAsync()
    {
        await using var db = NewContext();
        db.Users.Add(new ApplicationUser
        {
            Id                        = UserId,
            UserName                  = "usuario@dominio.com",
            Email                     = "usuario@dominio.com",
            NormalizedEmail           = "USUARIO@DOMINIO.COM",
            PreferredTwoFactorChannel = TwoFactorChannel.Email
        });
        await db.SaveChangesAsync();
    }

    private async Task<DateTime?> StampAsync()
    {
        await using var db = NewContext();
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == UserId)).SecurityFactorsChangedAt;
    }

    /// <summary>Carga la cuenta, aplica el cambio y guarda como lo haría UserManager.</summary>
    private async Task<(DateTime Before, DateTime After)> UpdateThroughIdentityAsync(Action<ApplicationUser> change)
    {
        await using var db = NewContext();
        using var store = new UserStore<ApplicationUser>(db);

        var user = await store.FindByIdAsync(UserId);
        change(user!);

        var before = DateTime.Now;
        (await store.UpdateAsync(user!)).Succeeded.Should().BeTrue();
        return (before, DateTime.Now);
    }

    [Fact]
    public async Task Una_cuenta_nueva_no_nace_en_espera()
    {
        await SeedAsync();

        (await StampAsync()).Should().BeNull();
    }

    public static TheoryData<string, Action<ApplicationUser>> FactorChanges => new()
    {
        { "Email",                     u => u.Email = "otro@dominio.com" },
        { "NormalizedEmail",           u => u.NormalizedEmail = "OTRO@DOMINIO.COM" },
        { "TwoFactorPhoneEncrypted",   u => u.TwoFactorPhoneEncrypted = "ENC:+14155552671" },
        { "TwoFactorPhoneConfirmed",   u => u.TwoFactorPhoneConfirmed = true },
        { "PreferredTwoFactorChannel", u => u.PreferredTwoFactorChannel = TwoFactorChannel.Sms },
        { "TwoFactorEnrolledAt",       u => u.TwoFactorEnrolledAt = new DateTime(2026, 10, 14, 9, 0, 0) }
    };

    [Theory]
    [MemberData(nameof(FactorChanges))]
    public async Task Cambiar_un_factor_sella_la_cuenta_con_la_hora_de_servidor(
        string property, Action<ApplicationUser> change)
    {
        await SeedAsync();

        var (before, after) = await UpdateThroughIdentityAsync(change);

        (await StampAsync()).Should().NotBeNull(because: $"{property} es un factor")
            .And.BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task UpdateAsync_de_Identity_sin_cambiar_factores_no_sella()
    {
        await SeedAsync();

        // UserStore.UpdateAsync marca todas las columnas como modificadas; un inicio de sesión que
        // solo toca LastLoginAt no puede dejar la cuenta un día sin step-up.
        await UpdateThroughIdentityAsync(u =>
        {
            u.LastLoginAt        = DateTime.Now;
            u.RefreshToken       = "hash";
            u.RefreshTokenExpiry = DateTime.Now.AddDays(30);
        });

        (await StampAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Reasignar_el_mismo_valor_no_sella()
    {
        await SeedAsync();

        await UpdateThroughIdentityAsync(u => u.Email = "usuario@dominio.com");

        (await StampAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Regenerar_la_clave_del_autenticador_sella()
    {
        await SeedAsync();

        // El camino de ResetAuthenticatorKeyAsync: la clave va a AspNetUserTokens, no a la cuenta.
        await using (var db = NewContext())
        using (var store = new UserStore<ApplicationUser>(db))
        {
            var user = await store.FindByIdAsync(UserId);
            await store.SetAuthenticatorKeyAsync(user!, "CLAVE1", default);
            await store.UpdateAsync(user!);
        }

        var first = await StampAsync();
        first.Should().NotBeNull();

        // Al regenerarla la fila ya existe y solo cambia su valor: también reapunta el autenticador.
        await using (var db = NewContext())
        {
            // Borrar la marca a mano no es un cambio de factor: no vuelve a sellarse.
            (await db.Users.SingleAsync(u => u.Id == UserId)).SecurityFactorsChangedAt = null;
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        using (var store = new UserStore<ApplicationUser>(db))
        {
            var user = await store.FindByIdAsync(UserId);
            await store.SetAuthenticatorKeyAsync(user!, "CLAVE2", default);
            await store.UpdateAsync(user!);
        }

        (await StampAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task La_clave_del_autenticador_sella_aunque_la_cuenta_no_este_cargada()
    {
        await SeedAsync();

        await using (var db = NewContext())
        {
            db.Set<IdentityUserToken<string>>().Add(new IdentityUserToken<string>
            {
                UserId = UserId, LoginProvider = "[AspNetUserStore]", Name = "AuthenticatorKey", Value = "CLAVE"
            });
            await db.SaveChangesAsync();
        }

        (await StampAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task Otro_token_de_Identity_no_sella()
    {
        await SeedAsync();

        await using (var db = NewContext())
        {
            db.Set<IdentityUserToken<string>>().Add(new IdentityUserToken<string>
            {
                UserId = UserId, LoginProvider = "[AspNetUserStore]", Name = "RecoveryCodes", Value = "x"
            });
            await db.SaveChangesAsync();
        }

        (await StampAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Tambien_sella_con_SaveChanges_sincrono()
    {
        await SeedAsync();

        using (var db = NewContext())
        {
            var user = db.Users.Single(u => u.Id == UserId);
            user.Email = "otro@dominio.com";
            db.SaveChanges();
        }

        (await StampAsync()).Should().NotBeNull();
    }
}
