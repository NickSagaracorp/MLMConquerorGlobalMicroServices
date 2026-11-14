using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class MemberCountrySnapshotWriterTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 1, 30, 0);
    private static readonly DateTime Dia   = new(2026, 11, 13);

    private const MemberType Amb = MemberType.Ambassador;
    private const MemberType Cli = MemberType.ExternalMember;

    private static AppDbContext Base() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void Miembro(AppDbContext db, string id, string pais, MemberType tipo, MemberAccountStatus estado,
        bool borrado = false) =>
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = id, FirstName = "N", LastName = id, Email = $"{id}@example.com",
            Country = pais, MemberType = tipo, Status = estado, IsDeleted = borrado,
            CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });

    private static MemberCountrySnapshotWriter Escritor(AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        return new MemberCountrySnapshotWriter(db, reloj.Object);
    }

    private static async Task<List<(string, MemberType, MemberAccountStatus, int)>> Filas(AppDbContext db, DateTime dia) =>
        (await db.MemberCountrySnapshots.Where(s => s.SnapshotDate == dia).ToListAsync())
            .Select(s => (s.Country, s.MemberType, s.Status, s.MemberCount)).ToList();

    [Fact]
    public async Task Cuenta_por_pais_tipo_y_estado_sin_borrados()
    {
        using var db = Base();
        Miembro(db, "AMB-1", "US", Amb, MemberAccountStatus.Active);
        Miembro(db, "AMB-2", "US", Amb, MemberAccountStatus.Active);
        Miembro(db, "CUS-1", "US", Cli, MemberAccountStatus.Active);
        Miembro(db, "AMB-3", "US", Amb, MemberAccountStatus.Suspended);
        Miembro(db, "AMB-4", "Peru", Amb, MemberAccountStatus.Terminated);
        Miembro(db, "AMB-5", "US", Amb, MemberAccountStatus.Active, borrado: true);
        await db.SaveChangesAsync();

        var antes = DateTime.Now;
        var n = await Escritor(db).WriteAsync(Dia.AddHours(23));

        n.Should().Be(4);
        (await Filas(db, Dia)).Should().BeEquivalentTo(new[]
        {
            ("US", Amb, MemberAccountStatus.Active, 2),
            ("US", Cli, MemberAccountStatus.Active, 1),
            ("US", Amb, MemberAccountStatus.Suspended, 1),
            ("Peru", Amb, MemberAccountStatus.Terminated, 1)
        });
        // CreationDate la sella el AuditInterceptor del contexto al insertar (DateTime.Now, hora del servidor), por
        // encima del valor que pone el escritor; el autor si es el del escritor.
        var fila = await db.MemberCountrySnapshots.FirstAsync();
        fila.CreatedBy.Should().Be(MemberCountrySnapshotWriter.Author);
        fila.CreationDate.Should().BeOnOrAfter(antes).And.BeOnOrBefore(DateTime.Now);
    }

    [Fact]
    public async Task Repetir_el_dia_reemplaza_sus_filas_y_no_toca_otros_dias()
    {
        using var db = Base();
        Miembro(db, "AMB-1", "US", Amb, MemberAccountStatus.Active);
        Miembro(db, "AMB-2", "US", Amb, MemberAccountStatus.Active);
        Miembro(db, "AMB-4", "Peru", Amb, MemberAccountStatus.Terminated);
        await db.SaveChangesAsync();
        await Escritor(db).WriteAsync(Dia.AddDays(-1));
        await Escritor(db).WriteAsync(Dia);

        (await db.MemberProfiles.SingleAsync(m => m.MemberId == "AMB-2")).Status = MemberAccountStatus.Suspended;
        (await db.MemberProfiles.SingleAsync(m => m.MemberId == "AMB-4")).IsDeleted = true;
        await db.SaveChangesAsync();
        await Escritor(db).WriteAsync(Dia);

        (await Filas(db, Dia)).Should().BeEquivalentTo(new[]
        {
            ("US", Amb, MemberAccountStatus.Active, 1),
            ("US", Amb, MemberAccountStatus.Suspended, 1)
        });
        (await Filas(db, Dia.AddDays(-1))).Should().BeEquivalentTo(new[]
        {
            ("US", Amb, MemberAccountStatus.Active, 2),
            ("Peru", Amb, MemberAccountStatus.Terminated, 1)
        });
    }
}
