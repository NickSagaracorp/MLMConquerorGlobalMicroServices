using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan Q (tercera revision del bloque 2, menor): el SaveChanges SINCRONO espera en el acto las lecturas asincronas de
/// MembershipPointsDeltas (GetAwaiter().GetResult() en QualifiedTeamCountsInterceptor). Con un contexto de sincronizacion
/// de un solo hilo (Blazor Server, WinForms) eso solo no se interbloquea si ninguna espera de la cadena vuelve a ese
/// contexto: todas llevan ConfigureAwait(false). Aqui el contexto no ejecuta nada de lo que se le manda: si alguna espera
/// volviera a el, el guardado no terminaria nunca.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerGuardadoSincronoTests
{
    private sealed class SinBomba : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) { /* el hilo esta bloqueado: nunca se ejecuta */ }
        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }

    [SqlServerFact]
    public void Un_guardado_sincrono_que_activa_una_suscripcion_no_se_interbloquea_en_un_contexto_de_un_hilo()
    {
        Exception? error = null;
        var hecho = false;
        var hilo = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new SinBomba());
            try
            {
                using var db = SqlServerReportes.Db();
                using var tx = db.Database.BeginTransaction();
                var m = BinarioDb.Prefijo("PQS") + "M";
                db.MemberProfiles.Add(new MemberProfile
                {
                    UserId = Guid.NewGuid(), MemberId = m, Email = $"{m.ToLowerInvariant()}@prueba.test", FirstName = "Plan",
                    LastName = "Q", Country = "US", MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active,
                    EnrollDate = DateTime.Now, CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now, CreatedBy = "plan-q-prueba"
                });
                db.MemberStatistics.Add(new MemberStatisticEntity { MemberId = m, CreatedBy = "plan-q-prueba", CreationDate = DateTime.Now });
                var sus = ReportesDb.Suscripcion(db, m, 3, MembershipStatus.Pending, DateTime.Now);
                db.SaveChanges();
                sus.SubscriptionStatus = MembershipStatus.Active;
                db.SaveChanges();
                db.MemberStatistics.AsNoTracking().Single(s => s.MemberId == m).PersonalPoints.Should().BeGreaterThan(0);
                hecho = true;
            }
            catch (Exception ex) { error = ex; }
        }) { IsBackground = true };
        hilo.Start();
        hilo.Join(TimeSpan.FromSeconds(60)).Should().BeTrue("el guardado sincrono no debe quedarse esperando al contexto bloqueado");
        error.Should().BeNull(error?.InnerException?.Message);
        hecho.Should().BeTrue();
    }
}
