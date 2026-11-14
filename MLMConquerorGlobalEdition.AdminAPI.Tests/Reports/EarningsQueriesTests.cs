using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Reports;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>La regla de ganancias de los reportes: pagadas + pendientes, sin canceladas, EarnedDate en [desde, hasta).</summary>
public class EarningsQueriesTests
{
    [Fact]
    public async Task Suma_pagadas_y_pendientes_de_la_ventana_sin_canceladas()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Comision(db, "AMB-1", 100m, new DateTime(2026, 5, 17, 23, 59, 0));    // antes de desde
        ClubRecognitionTestData.Comision(db, "AMB-1", 200m, new DateTime(2026, 5, 18));               // desde: entra
        ClubRecognitionTestData.Comision(db, "AMB-1", 300m, new DateTime(2026, 11, 13, 23, 59, 0), CommissionEarningStatus.Pending);
        ClubRecognitionTestData.Comision(db, "AMB-1", 400m, new DateTime(2026, 11, 14));              // hasta: no entra
        ClubRecognitionTestData.Comision(db, "AMB-1", 500m, new DateTime(2026, 6, 1), CommissionEarningStatus.Cancelled);
        ClubRecognitionTestData.Comision(db, "AMB-2", 50m, new DateTime(2026, 6, 1));
        await db.SaveChangesAsync();

        var totales = await EarningsQueries.TotalsByMember(db, new DateTime(2026, 5, 18), new DateTime(2026, 11, 14))
            .ToListAsync();

        totales.Select(t => (t.MemberId, t.Total)).Should().BeEquivalentTo(new[] { ("AMB-1", 500m), ("AMB-2", 50m) });
    }

    [Fact]
    public async Task Sin_desde_suma_toda_la_vida_hasta_el_corte()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Comision(db, "AMB-1", 100m, new DateTime(2020, 1, 1));
        ClubRecognitionTestData.Comision(db, "AMB-1", 200m, new DateTime(2026, 11, 13));
        ClubRecognitionTestData.Comision(db, "AMB-1", 400m, new DateTime(2026, 11, 14));
        await db.SaveChangesAsync();

        var total = await EarningsQueries.TotalsByMember(db, null, new DateTime(2026, 11, 14)).SingleAsync();

        total.Total.Should().Be(300m);
    }
}
