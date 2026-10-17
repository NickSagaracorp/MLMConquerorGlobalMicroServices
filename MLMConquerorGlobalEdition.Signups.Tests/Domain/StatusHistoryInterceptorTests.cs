using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Domain;

/// <summary>
/// El historial de estados se escribe al guardar, como SecurityFactorsInterceptor sella los
/// factores: así lo cubren también los que no lo escribían (activación del alta, facturación
/// recurrente, subida y bajada de nivel). Pasa por el AppDbContext real, que registra el
/// interceptor él mismo.
/// </summary>
public class StatusHistoryInterceptorTests
{
    private const string MemberId = "AMB-000001";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(_dbName).Options);

    /// <summary>Perfil Pending y suscripción Pending, como deja el alta. Devuelve el id de la suscripción.</summary>
    private async Task<string> SeedAsync()
    {
        await using var db = NewContext();
        db.MemberProfiles.Add(new MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = MemberId, FirstName = "Ana", LastName = "Ruiz",
            Country = "US", Status = MemberAccountStatus.Pending, CreatedBy = "seed"
        });
        var suscripcion = new MembershipSubscription
        {
            MemberId = MemberId, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Pending,
            StartDate = new DateTime(2026, 3, 1), CreatedBy = "seed"
        };
        db.MembershipSubscriptions.Add(suscripcion);
        await db.SaveChangesAsync();
        return suscripcion.Id;
    }

    [Fact]
    public async Task Crear_el_perfil_no_es_un_cambio_de_estado_de_la_cuenta()
    {
        await SeedAsync();

        await using var db = NewContext();
        (await db.MemberStatusHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Crear_una_suscripcion_deja_su_alta_sin_estado_anterior()
    {
        var id = await SeedAsync();

        await using var db = NewContext();
        var fila = await db.MembershipStatusHistories.SingleAsync();
        (fila.MemberId, fila.SubscriptionId, fila.MembershipLevelId, fila.OldStatus, fila.NewStatus)
            .Should().Be((MemberId, id, 3, (MembershipStatus?)null, MembershipStatus.Pending));
        fila.CreatedBy.Should().Be("seed");
    }

    [Fact]
    public async Task Cambiar_el_estado_de_la_cuenta_deja_una_fila_con_la_hora_de_servidor()
    {
        await SeedAsync();
        var antes = DateTime.Now;

        await using (var db = NewContext())
        {
            var perfil = await db.MemberProfiles.SingleAsync();
            perfil.Status       = MemberAccountStatus.Active;
            perfil.LastUpdateBy = "admin-001";
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        var fila = await lectura.MemberStatusHistories.SingleAsync();
        (fila.MemberId, fila.OldStatus, fila.NewStatus, fila.CreatedBy)
            .Should().Be((MemberId, MemberAccountStatus.Pending, MemberAccountStatus.Active, "admin-001"));
        fila.ChangedAt.Should().BeOnOrAfter(antes).And.BeOnOrBefore(DateTime.Now);
        fila.ChangedAt.Kind.Should().NotBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Guardar_sin_cambiar_el_estado_no_deja_fila_aunque_todo_se_marque_modificado()
    {
        await SeedAsync();

        await using (var db = NewContext())
        {
            var perfil = await db.MemberProfiles.SingleAsync();
            perfil.FirstName = "Ana María";
            db.Update(perfil);
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        (await lectura.MemberStatusHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Si_el_manejador_ya_escribio_la_fila_con_su_motivo_no_se_duplica()
    {
        await SeedAsync();

        await using (var db = NewContext())
        {
            var perfil = await db.MemberProfiles.SingleAsync();
            perfil.Status = MemberAccountStatus.Active;
            db.MemberStatusHistories.Add(new MemberStatusHistory
            {
                MemberId = MemberId, OldStatus = MemberAccountStatus.Pending, NewStatus = MemberAccountStatus.Active,
                Reason = "Pago verificado a mano", ChangedAt = DateTime.Now, CreatedBy = "admin-001"
            });
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        (await lectura.MemberStatusHistories.SingleAsync()).Reason.Should().Be("Pago verificado a mano");
    }

    [Fact]
    public async Task Cambiar_el_estado_de_la_suscripcion_deja_fila_con_suscripcion_y_nivel()
    {
        var id = await SeedAsync();

        await using (var db = NewContext())
        {
            var suscripcion = await db.MembershipSubscriptions.SingleAsync();
            suscripcion.SubscriptionStatus = MembershipStatus.Active;
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        var fila = await lectura.MembershipStatusHistories.SingleAsync(h => h.OldStatus != null);
        (fila.SubscriptionId, fila.MembershipLevelId, fila.OldStatus, fila.NewStatus)
            .Should().Be((id, 3, (MembershipStatus?)MembershipStatus.Pending, MembershipStatus.Active));
    }

    [Fact]
    public async Task Un_LastUpdateBy_que_no_cambio_en_este_guardado_no_es_el_autor()
    {
        // Quien tocó el perfil la última vez no es quien cambia ahora el estado: si este guardado no
        // pone LastUpdateBy, el autor es «system», no el de antes.
        await SeedAsync();
        await using (var db = NewContext())
        {
            var perfil = await db.MemberProfiles.SingleAsync();
            perfil.LastUpdateBy = "admin-antiguo";
            perfil.FirstName    = "Ana María";
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var perfil = await db.MemberProfiles.SingleAsync();
            perfil.Status = MemberAccountStatus.Suspended;
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var suscripcion = await db.MembershipSubscriptions.SingleAsync();
            suscripcion.LastUpdateBy = "billing-antiguo";
            suscripcion.IsAutoRenew  = true;
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var suscripcion = await db.MembershipSubscriptions.SingleAsync();
            suscripcion.SubscriptionStatus = MembershipStatus.HoldByBilling;
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        (await lectura.MemberStatusHistories.SingleAsync()).CreatedBy.Should().Be("system");
        (await lectura.MembershipStatusHistories.SingleAsync(h => h.OldStatus != null)).CreatedBy.Should().Be("system");
    }

    [Fact]
    public async Task No_duplica_la_fila_de_membresia_si_el_guardado_ya_la_trae()
    {
        var id = await SeedAsync();

        await using (var db = NewContext())
        {
            var suscripcion = await db.MembershipSubscriptions.SingleAsync();
            suscripcion.SubscriptionStatus = MembershipStatus.Active;
            db.MembershipStatusHistories.Add(new MembershipStatusHistory
            {
                MemberId = MemberId, SubscriptionId = id, MembershipLevelId = 3,
                OldStatus = MembershipStatus.Pending, NewStatus = MembershipStatus.Active,
                ChangedAt = DateTime.Now, CreatedBy = "manejador"
            });
            await db.SaveChangesAsync();
        }

        await using var lectura = NewContext();
        (await lectura.MembershipStatusHistories.SingleAsync(h => h.OldStatus != null)).CreatedBy.Should().Be("manejador");
    }
}
