using System.Security.Claims;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Las guardas de propiedad del BizCenter usan el prefijo de TreePrefix: aunque la ruta del que
/// mira no acabara en "/", "/AMB-1" no deja ver a nadie de "/AMB-10/...". Y el nodo del que mira
/// es el más reciente (R8): un nodo obsoleto no le da acceso a su antiguo equipo.
/// </summary>
public class PrefijoDeSubarbolTests
{
    private static ClaimsPrincipal Miembro(string memberId) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-001"),
                new Claim(CallerIdentity.MemberIdClaim, memberId),
                new Claim(ClaimTypes.Role, AppRoles.Ambassador)
            ],
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    [Fact]
    public async Task La_guarda_de_patrocinio_no_confunde_AMB_1_con_AMB_10()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-1",  null,     "/AMB-1", 1);
        ReportesDb.Nodo(db, "AMB-10", null,     ReportesDb.Ruta("AMB-10"), 1);
        ReportesDb.Nodo(db, "MBR-9",  "AMB-10", ReportesDb.Ruta("AMB-10", "MBR-9"), 2);
        ReportesDb.Nodo(db, "MBR-2",  "AMB-1",  ReportesDb.Ruta("AMB-1", "MBR-2"), 2);
        await db.SaveChangesAsync();
        var guarda = new DownlineGuard(db);

        (await guarda.PuedeVerRamaDePatrocinioAsync(Miembro("AMB-1"), "MBR-9", CancellationToken.None)).Should().BeFalse();
        (await guarda.PuedeVerRamaDePatrocinioAsync(Miembro("AMB-1"), "MBR-2", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task La_guarda_binaria_no_confunde_AMB_1_con_AMB_10()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.NodoBinario(db, "AMB-1",  null,     "/AMB-1", TreeSide.Left);
        ReportesDb.NodoBinario(db, "AMB-10", null,     ReportesDb.Ruta("AMB-10"), TreeSide.Left);
        ReportesDb.NodoBinario(db, "MBR-9",  "AMB-10", ReportesDb.Ruta("AMB-10", "MBR-9"), TreeSide.Left);
        await db.SaveChangesAsync();

        (await new DownlineGuard(db).PuedeVerNodoBinarioAsync(Miembro("AMB-1"), "MBR-9", CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Un_nodo_obsoleto_del_que_mira_no_le_abre_su_antiguo_equipo()
    {
        await using var db = ReportesDb.Nueva();
        await ReportesDb.NodoObsoletoAsync(db, "AMB-1", null, ReportesDb.Ruta("AMB-1"), 1);
        ReportesDb.Nodo(db, "AMB-1", "AMB-7", ReportesDb.Ruta("AMB-7", "AMB-1"), 2);
        ReportesDb.Nodo(db, "MBR-2", "AMB-5", ReportesDb.Ruta("AMB-1", "MBR-2"), 2);
        await db.SaveChangesAsync();

        (await new DownlineGuard(db).PuedeVerRamaDePatrocinioAsync(Miembro("AMB-1"), "MBR-2", CancellationToken.None))
            .Should().BeFalse("su ruta actual es /AMB-7/AMB-1/ y MBR-2 cuelga de la antigua");
    }

    [Fact]
    public async Task Un_objetivo_que_se_fue_no_se_ve_aunque_le_quede_un_nodo_antiguo_bajo_mi_prefijo()
    {
        // R8 también sobre el objetivo: MBR-2 colgó de AMB-1, pero su nodo actual es de AMB-7.
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-1", null, ReportesDb.Ruta("AMB-1"), 1);
        await ReportesDb.NodoObsoletoAsync(db, "MBR-2", "AMB-1", ReportesDb.Ruta("AMB-1", "MBR-2"), 2);
        ReportesDb.Nodo(db, "MBR-2", "AMB-7", ReportesDb.Ruta("AMB-7", "MBR-2"), 2);
        await db.SaveChangesAsync();

        (await new DownlineGuard(db).PuedeVerRamaDePatrocinioAsync(Miembro("AMB-1"), "MBR-2", CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Un_objetivo_binario_que_se_fue_no_se_ve_aunque_le_quede_un_nodo_antiguo()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.NodoBinario(db, "AMB-1", null, ReportesDb.Ruta("AMB-1"), TreeSide.Left);
        var antiguo = ReportesDb.NodoBinario(db, "MBR-2", "AMB-1", ReportesDb.Ruta("AMB-1", "MBR-2"), TreeSide.Left);
        await db.SaveChangesAsync();
        antiguo.CreationDate = new DateTime(2020, 1, 1);
        await db.SaveChangesAsync();
        ReportesDb.NodoBinario(db, "MBR-2", "AMB-7", ReportesDb.Ruta("AMB-7", "MBR-2"), TreeSide.Right);
        await db.SaveChangesAsync();

        (await new DownlineGuard(db).PuedeVerNodoBinarioAsync(Miembro("AMB-1"), "MBR-2", CancellationToken.None))
            .Should().BeFalse();
    }
}
