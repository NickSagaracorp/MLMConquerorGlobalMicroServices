using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Trees;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P: con un hueco repetido (antes de reparar), el recalculador elegia la pierna con un FirstOrDefault
/// sin orden: uno cualquiera de los hijos del hueco, asi que los puntos no eran reproducibles. Ahora cuenta
/// el subarbol del que se queda el hueco, el mas antiguo por (CreationDate, Id): la misma regla que la
/// reparacion (decision de Nick, 2026-10-18).
/// </summary>
public class RecalculadorDePiernasOrdenTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => new(2026, 10, 18, 10, 0, 0);
        public DateTime UtcNow => Now;
    }

    private static DualTeamEntity Nodo(string id, string memberId, string? padre, string ruta, DateTime alta) => new()
    {
        Id = id, MemberId = memberId, ParentMemberId = padre, Side = TreeSide.Left, HierarchyPath = ruta,
        CreationDate = alta, LastUpdateDate = alta, CreatedBy = "plan-p-prueba"
    };

    private static MemberStatisticEntity Puntos(string memberId, int puntos) => new()
    {
        MemberId = memberId, PersonalPoints = puntos, CreationDate = DateTime.Now, CreatedBy = "plan-p-prueba"
    };

    [Theory]
    [InlineData("A-INTRUSO", "Z-GUARDIAN")]   // el intruso va antes por Id
    [InlineData("Z-INTRUSO", "A-GUARDIAN")]   // el guardian va antes por Id
    public async Task Con_un_hueco_repetido_cuenta_la_pierna_del_mas_antiguo(string idIntruso, string idGuardian)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var antes = new DateTime(2026, 1, 1);
        db.DualTeamTree.AddRange(
            Nodo("P-ID", "P", null, "/P/", antes),
            Nodo(idIntruso, "I", "P", "/P/I/", antes.AddDays(2)),
            Nodo(idGuardian, "K", "P", "/P/K/", antes.AddDays(1)));
        db.MemberStatistics.AddRange(Puntos("P", 0), Puntos("K", 10), Puntos("I", 99));
        await db.SaveChangesAsync();
        // AuditInterceptor pone CreationDate = ahora al insertar; las fechas de alta se fijan despues.
        foreach (var n in db.DualTeamTree.ToList())
            n.CreationDate = n.MemberId switch { "K" => antes.AddDays(1), "I" => antes.AddDays(2), _ => antes };
        await db.SaveChangesAsync();

        await new DualTeamPointsRecalculator(db, new Reloj()).RecalculateForUplinesAsync("P");

        (await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == "P")).LeftLegPoints.Should().Be(10);
    }
}
