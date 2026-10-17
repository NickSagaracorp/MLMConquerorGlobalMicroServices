using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// «Calificado» según Nick (corrección N3): membresía activa Y (inscrito hace menos de 12 meses, O
/// anualidad pagada, O marca KeepQualified). La anualidad todavía no existe (Plan K): hoy la da
/// AnnualFeeNotModeled, que siempre dice que no.
/// </summary>
public class QualificationServiceTests
{
    private static readonly RelojFijo Reloj = new(new DateTime(2026, 10, 16, 12, 0, 0));

    private sealed class AnualidadPagadaPor(params string[] miembros) : IAnnualFeeStatus
    {
        public Task<IReadOnlySet<string>> PaidCurrentAnnualFeeAsync(
            IReadOnlyCollection<string> memberIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(memberIds.Intersect(miembros).ToHashSet());
    }

    private static QualificationService Servicio(AppDbContext db, IAnnualFeeStatus? anualidad = null) =>
        new(db, Reloj, anualidad ?? new AnnualFeeNotModeled());

    private static async Task<AppDbContext> ConMiembroAsync(
        DateTime alta, MembershipStatus? estado, bool mantener = false, string id = "AMB-000001")
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, id, alta: alta).KeepQualified = mantener;
        if (estado is { } e) ReportesDb.Suscripcion(db, id, 2, e, alta);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Activo_e_inscrito_hace_menos_de_12_meses_esta_calificado()
    {
        await using var db = await ConMiembroAsync(new DateTime(2026, 7, 1), MembershipStatus.Active);
        (await Servicio(db).IsQualifiedAsync("AMB-000001")).Should().BeTrue();
    }

    [Fact]
    public async Task Activo_e_inscrito_hace_mas_de_12_meses_sin_anualidad_ni_marca_no_lo_esta()
    {
        await using var db = await ConMiembroAsync(new DateTime(2025, 9, 1), MembershipStatus.Active);
        (await Servicio(db).IsQualifiedAsync("AMB-000001")).Should().BeFalse();
    }

    [Fact]
    public async Task La_marca_KeepQualified_lo_mantiene_calificado()
    {
        await using var db = await ConMiembroAsync(new DateTime(2025, 9, 1), MembershipStatus.Active, mantener: true);
        (await Servicio(db).IsQualifiedAsync("AMB-000001")).Should().BeTrue();
    }

    [Fact]
    public async Task La_anualidad_pagada_lo_mantiene_calificado()
    {
        await using var db = await ConMiembroAsync(new DateTime(2025, 9, 1), MembershipStatus.Active);
        (await Servicio(db, new AnualidadPagadaPor("AMB-000001")).IsQualifiedAsync("AMB-000001")).Should().BeTrue();
    }

    [Theory]
    [InlineData(MembershipStatus.OnHold)]
    [InlineData(MembershipStatus.HoldByBilling)]
    [InlineData(MembershipStatus.Cancelled)]
    [InlineData(MembershipStatus.Expired)]
    [InlineData(MembershipStatus.Pending)]
    public async Task Sin_membresia_activa_nada_califica(MembershipStatus estado)
    {
        await using var db = await ConMiembroAsync(new DateTime(2026, 7, 1), estado, mantener: true);
        (await Servicio(db, new AnualidadPagadaPor("AMB-000001")).IsQualifiedAsync("AMB-000001")).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_suscripcion_no_esta_calificado()
    {
        await using var db = await ConMiembroAsync(new DateTime(2026, 7, 1), null, mantener: true);
        (await Servicio(db).IsQualifiedAsync("AMB-000001")).Should().BeFalse();
    }

    [Fact]
    public async Task El_borde_de_los_12_meses_es_estricto()
    {
        await using var justo = await ConMiembroAsync(new DateTime(2025, 10, 16, 12, 0, 0), MembershipStatus.Active);
        (await Servicio(justo).IsQualifiedAsync("AMB-000001")).Should().BeFalse("hace justo 12 meses ya no es «menos de 12»");

        await using var casi = await ConMiembroAsync(new DateTime(2025, 10, 17), MembershipStatus.Active);
        (await Servicio(casi).IsQualifiedAsync("AMB-000001")).Should().BeTrue();
    }

    [Fact]
    public async Task Manda_la_suscripcion_actual_la_de_inicio_mas_reciente()
    {
        await using var db = await ConMiembroAsync(new DateTime(2026, 5, 1), MembershipStatus.Active);
        ReportesDb.Suscripcion(db, "AMB-000001", 3, MembershipStatus.Expired, new DateTime(2026, 9, 1));
        await db.SaveChangesAsync();

        (await Servicio(db).IsQualifiedAsync("AMB-000001")).Should().BeFalse();
    }

    [Fact]
    public async Task Por_lote_responde_por_cada_miembro_pedido()
    {
        await using var db = await ConMiembroAsync(new DateTime(2026, 7, 1), MembershipStatus.Active);

        var r = await Servicio(db).AreQualifiedAsync(new[] { "AMB-000001", "AMB-999999" });

        r.Should().BeEquivalentTo(new Dictionary<string, bool> { ["AMB-000001"] = true, ["AMB-999999"] = false });
    }

    [Fact]
    public async Task La_anualidad_no_modelada_nunca_esta_pagada()
        => (await new AnnualFeeNotModeled().PaidCurrentAnnualFeeAsync(new[] { "AMB-000001" })).Should().BeEmpty();
}
