# Reportes del Admin — Plan B: Lifetime/Current Rank y Top Countries

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar los reportes Lifetime Rank y Current Rank (un servicio, un par de endpoints y una página con dos rutas) y Top Countries (foto diaria de miembros por país, proceso nocturno, tabla con Change contra 30 días antes y gráfica mensual/semanal/diaria), con exportación a CSV y sus entradas en el menú Reports.

**Architecture:** Rangos: `MemberRankQueries` gana dos consultas sobre toda la población (`LifetimeRankSinceRows`: mayor `SortOrder` y la PRIMERA vez que se alcanzó; `CurrentRankSinceRows`: la fila más reciente, la misma regla de `CurrentRankRows`). `RankReportService` (Repository) las une con `MemberProfiles` en una sola consulta SQL, guarda la lista completa 10 minutos en memoria por modo (como `EarningsRankingService`) y filtra/pagina en memoria. Países: tabla nueva `MemberCountrySnapshots` (día, país, tipo, estado, conteo; clave única) que escribe `MemberCountrySnapshotWriter` (Repository), llamado por un job nocturno de Hangfire en el BizCenter y por un endpoint manual del AdminAPI; `TopCountriesReportService` lee solo esa tabla. AdminAPI expone dos controladores nuevos bajo `api/v1/admin/reports/...`; AdminWeb tiene dos páginas nuevas y un componente de filtro de estado compartido entre ambas.

**Tech Stack:** .NET 10, EF Core 10 (SQL Server), ASP.NET Core Web API, Hangfire, Blazor Server + Syncfusion (Grid y Charts 33.1.44), xUnit + FluentAssertions + Moq + EF InMemory.

**Especificación:** `docs/superpowers/specs/2026-11-14-reportes-admin-design.md` (secciones Base común, Reporte 1 y 1b, Reporte 4, Reporte 5, Historial de rangos y migración, Decisiones tomadas).

**Plan anterior (hecho):** `docs/superpowers/plans/2026-11-14-reportes-admin-A-base-y-earnings.md` — de él se reutilizan `CsvReportWriter`, `Permissions.Reports.Export`, `IsCorporateAccount`, `ServerGridAdaptor.OnCount`, el grupo Reports del menú y el patrón de caché de `EarningsRankingService`.

**Fuera de este plan:** importar el histórico de rank achievement desde legacy (sección «Historial de rangos y migración»: lo hace la migración; mientras no exista, «Since» de un migrado es la fecha de la migración) y Club Recognition (Plan C).

---

## Reglas del repo que este plan respeta

- Hora del servidor (`IDateTimeProvider.Now`), nunca `UtcNow`, para días y cortes. El cron de Hangfire se escribe en UTC (como los demás jobs) y el día de la foto lo decide `Now`, no la hora del cron.
- Toda migración nueva obliga a regenerar `DeploymentScripts/migrate-rds.sql` (lo comprueba `BizCenter.Tests/Migraciones/ScriptDeDespliegueTests.cs`).
- `MLMConquerorGlobalEditionDb_Escala` es de **solo lectura**: se mide con `SELECT`, nunca se escribe ni se migra.
- Commits sin coautoría ni menciones de herramientas: solo la descripción de lo que se hizo.
- Componentes y servicios se escriben una vez; lo específico se inyecta (un servicio y una página para los dos reportes de rango; un filtro de estado para las dos páginas).

## Decisiones de diseño (y valores por defecto elegidos)

1. **Rangos rápidos sin tocar el motor de rangos.** `MemberStatisticEntity` no guarda rango vigente ni de por vida (comprobado), así que la opción «columnas precalculadas» obligaría a cambiar cada escritor de `MemberRankHistories`. Se hace lo primero que pide la especificación: una consulta agregada por miembro (`MAX(SortOrder)` y `MIN(AchievedAt)` de ese rango), que usa el índice existente `IX_MemberRankHistories_MemberId_AchievedAt` (incluye `RankDefinitionId`), unida a `MemberProfiles` en SQL; la lista completa se cachea 10 minutos por modo (2 claves). Si la medición de la Tarea 3 no cumple, se avisa a Nick con los números (ver «Preguntas abiertas», 1).
2. **Orden por defecto.** Rango (`SortOrder`) de mayor a menor; dentro de un mismo rango, «Since» de la más reciente a la más antigua (lo que pide Current Rank); para Lifetime se usa el mismo desempate (la especificación no lo fija); al final `MemberId` ordinal para que sea estable.
3. **Filtro de estado de los rangos.** Sin estados = Active, Inactive, Suspended y Pending. `Terminated` se descarta siempre, aunque llegue en la petición.
4. **Foto diaria.** El job corre a las **05:30 UTC** (`"30 5 * * *"`, `TimeZoneInfo.Utc`, como los demás jobs del BizCenter). El servidor está en hora del Este: 05:30 UTC son las 01:30 en verano (UTC−4) y las 00:30 en invierno (UTC−5), siempre después de la medianoche local. Escribe `Now.Date − 1` (el día que cerró) con el estado de ese momento. Vive en el BizCenter (`BizCenter/Jobs`, cola `bizcenter`), junto a `MemberStatisticSnapshotJob`, porque el AdminAPI no tiene Hangfire.
5. **Foto manual.** `POST api/v1/admin/reports/countries/snapshot` (solo SuperAdmin) escribe la foto de **hoy** con el estado actual: sirve para tener la primera foto el día que se activa el feature y para ver el estado del día; esa misma noche el job la reemplaza con el cierre. No rellena días pasados (no se puede reconstruir el pasado). Desde el panel de Hangfire del BizCenter (`/hangfire`, «Recurring jobs» → `member-country-snapshot` → «Trigger now») se puede relanzar la del día anterior.
6. **Change.** Se compara contra la última foto con fecha ≤ (foto mostrada − 30 días). Si no hay ninguna, Change = null («—»). La tabla dice contra qué fecha compara.
7. **Gráfica.** Cada período toma la última foto dentro del período. Mensual por defecto (últimos 12 meses); «Weeks of a month» = semanas de lunes a domingo cortadas en los límites del mes; «Days of a week» = los días de la semana (ya cortada por el mes) que contiene la fecha elegida. Un período sin foto devuelve `null` (barra vacía); un período con foto pero sin filas de los estados elegidos devuelve 0.
8. **Top Countries cuenta a todos** (también cuentas corporativas): la especificación dice «Total cuenta a todos los miembros; el estado es un filtro». El país se muestra tal como está en `MemberProfile.Country`.
9. **Controladores nuevos** en vez de crecer `AdminReportsController` (su constructor y sus pruebas no cambian). Ya existe `AdminRankReportsController` (`api/v1/admin/ranks/reports`, otros reportes): el nuevo se llama `AdminRankStandingsReportController`.
10. **Desplegable de rangos:** se reutiliza `GET api/v1/admin/rank-definitions` (ya devuelve `SortOrder` y `Status`); la página filtra `Status == "Active"` y `SortOrder > 0`.
11. **Gráfica:** Syncfusion (`SfChart`; decisión de Nick: las gráficas del admin son de Syncfusion, no ApexCharts/`mlm-charts.js`). AdminWeb aún no referencia `Syncfusion.Blazor.Charts`: se añade a la misma versión que el resto (33.1.44).

## Mapa de archivos

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `MLMConquerorGlobalEdition.Repository/Queries/MemberRankQueries.cs` | Modificar | `MemberRankSince`, `LifetimeRankSinceRows()`, `CurrentRankSinceRows()` |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/RankReportViews.cs` | Crear | `RankReportMode`, `RankReportFilter`, `RankReportRowView`, `RankReportSourceRow` |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/RankReportService.cs` | Crear | consulta SQL, caché, filtros, página y exportación |
| `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminRankStandingsReportController.cs` | Crear | `POST reports/ranks/grid`, `GET reports/ranks/export` |
| `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberCorporateAccountController.cs` | Modificar | invalida también la caché de rangos |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Shared/MemberStatusFilter.razor` | Crear | multiselección de estados (las dos páginas) |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRankReport.razor` | Crear | página con dos rutas (lifetime / current) |
| `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberCountrySnapshot.cs` | Crear | entidad de la foto diaria |
| `MLMConquerorGlobalEdition.Repository/Configurations/MemberCountrySnapshotConfiguration.cs` | Crear | `date`, longitudes y clave única |
| `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs` | Modificar | `DbSet<MemberCountrySnapshot>` |
| `MLMConquerorGlobalEdition.Repository/Migrations/<ts>_FotoDiariaDeMiembrosPorPais.cs` | Crear (EF) | tabla e índice único |
| `DeploymentScripts/migrate-rds.sql` | Regenerar | script idempotente |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/MemberCountrySnapshotWriter.cs` | Crear | escribe/reescribe la foto de un día |
| `MLMConquerorGlobalEdition.BizCenter/Jobs/MemberCountrySnapshotJob.cs` | Crear | job nocturno (ayer en hora del servidor) |
| `MLMConquerorGlobalEdition.BizCenter/Program.cs` | Modificar | registro y `RecurringJob` |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/ReportPeriods.cs` | Crear | meses, semanas cortadas por el mes, días de una semana |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/TopCountriesViews.cs` | Crear | vistas de la tabla y de la gráfica |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/TopCountriesReportService.cs` | Crear | tabla con Change y serie de la gráfica |
| `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminTopCountriesReportController.cs` | Crear | `table`, `chart`, `export`, `snapshot` |
| `MLMConquerorGlobalEdition.AdminAPI/Program.cs` | Modificar | registro de los tres servicios |
| `MLMConquerorGlobalEdition.AdminWeb/MLMConquerorGlobalEdition.AdminWeb.csproj` | Modificar | paquete `Syncfusion.Blazor.Charts` 33.1.44 |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminTopCountriesReport.razor` | Crear | gráfica, tabla, exportación y foto manual |
| `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor` | Modificar | Top Countries, Lifetime Rank, Current Rank |
| `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/*.cs` | Crear | pruebas de consultas, servicios, escritor, periodos y controladores |
| `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerReporteDeRangosTests.cs` | Crear | traducción a SQL Server y SQL para medir |
| `MLMConquerorGlobalEdition.BizCenter.Tests/MemberCountrySnapshotJobTests.cs` | Crear | el job escribe ayer y va en la cola `bizcenter` |

---

### Task 1: Rango de por vida y vigente con su fecha, sobre toda la población

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/MemberRankQueries.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/RangoDesdeQueriesTests.cs`

Reglas (especificación, Reportes 4 y 5):
- De por vida: el rango de mayor `SortOrder` del historial; «Since» = el `AchievedAt` **más antiguo** de las filas de ese rango (la primera vez que lo alcanzó), no la fila de menor Id como `LifetimeRanks`.
- Vigente: la fila con el `AchievedAt` más reciente (la misma regla y desempate de `CurrentRankRows`); «Since» = su `AchievedAt`.
- Una fila por miembro con historial. El rango 0 no se excluye aquí (lo excluye el reporte).

- [ ] **Step 1: Escribir las pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Rango de por vida (el de mayor SortOrder, desde la PRIMERA vez que se alcanzó) y rango vigente (la fila más reciente)
/// de cada miembro con historial, para los reportes Lifetime Rank y Current Rank.
/// </summary>
public class RangoDesdeQueriesTests
{
    /// <summary>Base InMemory con el rango 0 (Lifestyle Consultant) y Silver, Gold y Platinum.</summary>
    internal static AppDbContext ConRangos()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.RankDefinitions.AddRange(
            new RankDefinition { Id = 20, Name = "Lifestyle Consultant", SortOrder = 0, CreatedBy = "seed" },
            new RankDefinition { Id = 1,  Name = "Silver",               SortOrder = 1, CreatedBy = "seed" },
            new RankDefinition { Id = 2,  Name = "Gold",                 SortOrder = 2, CreatedBy = "seed" },
            new RankDefinition { Id = 3,  Name = "Platinum",             SortOrder = 3, CreatedBy = "seed" });
        return db;
    }

    internal static void Rango(AppDbContext db, string memberId, int rankId, DateTime fecha) =>
        db.MemberRankHistories.Add(new MemberRankHistory
        {
            MemberId = memberId, RankDefinitionId = rankId, AchievedAt = fecha,
            CreationDate = fecha, LastUpdateDate = fecha, CreatedBy = "seed"
        });

    /// <summary>Silver, Gold, baja a Silver, vuelve a Gold y baja otra vez a Silver.</summary>
    private static async Task<AppDbContext> SubeYBajaAsync()
    {
        var db = ConRangos();
        Rango(db, "AMB-1", 1, new DateTime(2026, 1, 10));
        Rango(db, "AMB-1", 2, new DateTime(2026, 3, 1, 9, 0, 0));
        Rango(db, "AMB-1", 1, new DateTime(2026, 5, 1));
        Rango(db, "AMB-1", 2, new DateTime(2026, 6, 1));
        Rango(db, "AMB-1", 1, new DateTime(2026, 7, 1));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task De_por_vida_es_el_mayor_rango_desde_la_primera_vez_que_se_alcanzo()
    {
        using var db = await SubeYBajaAsync();

        var r = await db.LifetimeRankSinceRows().SingleAsync();

        (r.MemberId, r.RankDefinitionId, r.SortOrder, r.Since)
            .Should().Be(("AMB-1", 2, 2, new DateTime(2026, 3, 1, 9, 0, 0)));
    }

    [Fact]
    public async Task Vigente_es_la_fila_mas_reciente_con_su_fecha()
    {
        using var db = await SubeYBajaAsync();

        var r = await db.CurrentRankSinceRows().SingleAsync();

        (r.MemberId, r.RankDefinitionId, r.SortOrder, r.Since)
            .Should().Be(("AMB-1", 1, 1, new DateTime(2026, 7, 1)));
    }

    [Fact]
    public async Task Una_fila_por_miembro_con_historial()
    {
        using var db = ConRangos();
        Rango(db, "AMB-1", 3, new DateTime(2026, 2, 1));
        Rango(db, "AMB-2", 20, new DateTime(2026, 2, 1));
        Rango(db, "AMB-2", 20, new DateTime(2026, 4, 1));
        await db.SaveChangesAsync();

        (await db.LifetimeRankSinceRows().Select(r => r.MemberId).ToListAsync()).Should().BeEquivalentTo("AMB-1", "AMB-2");
        (await db.CurrentRankSinceRows().Select(r => r.MemberId).ToListAsync()).Should().BeEquivalentTo("AMB-1", "AMB-2");
        (await db.LifetimeRankSinceRows().SingleAsync(r => r.MemberId == "AMB-2")).Since.Should().Be(new DateTime(2026, 2, 1));
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~RangoDesdeQueriesTests"`
Expected: FAIL de compilación («LifetimeRankSinceRows no existe»).

- [ ] **Step 3: Implementar**

En `MemberRankQueries.cs`, justo después del `record MemberRankEntry(...)` (fuera de la clase estática):

```csharp
/// <summary>
/// El rango de un miembro y desde cuándo lo tiene (reportes Lifetime Rank y Current Rank). Clase con init (no record
/// posicional) para que EF pueda seguir componiendo la consulta (filtrar y unir) después de la proyección.
/// </summary>
public sealed class MemberRankSince
{
    public string   MemberId         { get; init; } = string.Empty;
    public int      RankDefinitionId { get; init; }
    public int      SortOrder        { get; init; }
    public DateTime Since            { get; init; }
}
```

Dentro de `public static class MemberRankQueries`, al final:

```csharp
    /// <summary>
    /// Rango vigente de TODOS los miembros con historial (la fila de <see cref="CurrentRankRows"/>), con su SortOrder y la
    /// fecha de esa fila. Reporte Current Rank.
    /// </summary>
    public static IQueryable<MemberRankSince> CurrentRankSinceRows(this AppDbContext db) =>
        from r in db.CurrentRankRows()
        join d in db.RankDefinitions.AsNoTracking() on r.RankDefinitionId equals d.Id
        select new MemberRankSince
        {
            MemberId = r.MemberId, RankDefinitionId = r.RankDefinitionId, SortOrder = d.SortOrder, Since = r.AchievedAt
        };

    /// <summary>
    /// Rango de por vida de TODOS los miembros con historial: el de mayor SortOrder, con la fecha de la PRIMERA vez que lo
    /// alcanzaron (MIN(AchievedAt) de sus filas de ese rango). Reporte Lifetime Rank. A diferencia de
    /// <see cref="LifetimeRanks"/> (lista de ids, fila de menor Id), es una agregacion por miembro: MAX(SortOrder) y, unida
    /// a ella, MIN(AchievedAt). Lee el indice IX_MemberRankHistories_MemberId_AchievedAt, que ya incluye el rango.
    /// Si dos definiciones compartieran SortOrder, gana el Id de definicion menor (las del seed no lo comparten).
    /// </summary>
    public static IQueryable<MemberRankSince> LifetimeRankSinceRows(this AppDbContext db)
    {
        var rows = from r in db.MemberRankHistories.AsNoTracking()
                   join d in db.RankDefinitions.AsNoTracking() on r.RankDefinitionId equals d.Id
                   select new { r.MemberId, r.RankDefinitionId, d.SortOrder, r.AchievedAt };

        var top = rows.GroupBy(x => x.MemberId)
                      .Select(g => new { MemberId = g.Key, SortOrder = g.Max(x => x.SortOrder) });

        return from x in rows
               join t in top on new { x.MemberId, x.SortOrder } equals new { t.MemberId, t.SortOrder }
               group x by new { x.MemberId, x.SortOrder } into g
               select new MemberRankSince
               {
                   MemberId         = g.Key.MemberId,
                   SortOrder        = g.Key.SortOrder,
                   RankDefinitionId = g.Min(y => y.RankDefinitionId),
                   Since            = g.Min(y => y.AchievedAt)
               };
    }
```

- [ ] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~RangoDesdeQueriesTests"`
Expected: PASS (3).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Queries/MemberRankQueries.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/RangoDesdeQueriesTests.cs
git commit -m "Reportes: rango de por vida (mayor rango desde la primera vez que se alcanzo) y rango vigente con su fecha para toda la poblacion"
```

---

### Task 2: Servicio de los reportes de rango

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/RankReportViews.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/RankReportService.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/RankReportServiceTests.cs`

Reglas (especificación, Reportes 4 y 5, y Decisiones):
- Solo `MemberType = Ambassador`. Fuera: rango con `SortOrder = 0`, sin historial, `Status = Terminated`, `IsCorporateAccount`.
- Lifetime: `LifetimeRankSinceRows`; Current: `CurrentRankSinceRows` (un vigente de rango 0 lo saca, aunque de por vida tenga otro).
- Orden: `SortOrder` desc, `Since` desc, `MemberId` ordinal.
- Filtros en memoria sobre la lista cacheada: búsqueda (nombre completo, email o país, sin mayúsculas), rango (`RankDefinitionId`), estados (sin estados = los cuatro visibles; `Terminated` se descarta), «Since» desde/hasta por día completo e inclusivo.
- Caché: lista completa por modo, 10 minutos, con `InvalidateAll()`; los pedidos en frío se serializan (un cálculo por clave).

- [ ] **Step 1: Vistas**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Lifetime Rank (el mayor rango alcanzado) o Current Rank (el vigente).</summary>
public enum RankReportMode
{
    Lifetime,
    Current
}

/// <summary>
/// Filtros de los reportes de rango. Sin estados = los visibles (Active, Inactive, Suspended, Pending); Terminated nunca
/// aparece. SinceFrom y SinceTo son días completos e inclusivos sobre la fecha «Since».
/// </summary>
public sealed record RankReportFilter(
    RankReportMode Mode,
    string? Search = null,
    int? RankDefinitionId = null,
    IReadOnlyCollection<MemberAccountStatus>? Statuses = null,
    DateTime? SinceFrom = null,
    DateTime? SinceTo = null);

/// <summary>Una fila de Lifetime Rank / Current Rank.</summary>
public sealed class RankReportRowView
{
    public string              MemberId         { get; init; } = string.Empty;
    public string              MemberName       { get; init; } = string.Empty;
    public string              Email            { get; init; } = string.Empty;
    public string              Country          { get; init; } = string.Empty;
    public MemberAccountStatus Status           { get; init; }
    public int                 RankDefinitionId { get; init; }
    public string              RankName         { get; init; } = string.Empty;
    public int                 RankSortOrder    { get; init; }
    public DateTime            Since            { get; init; }
}

/// <summary>Lo que trae la consulta SQL (<see cref="RankReportService.SourceQuery"/>) antes de dar forma a la fila.</summary>
public sealed class RankReportSourceRow
{
    public string              MemberId         { get; init; } = string.Empty;
    public int                 RankDefinitionId { get; init; }
    public string              RankName         { get; init; } = string.Empty;
    public int                 SortOrder        { get; init; }
    public DateTime            Since            { get; init; }
    public string              FirstName        { get; init; } = string.Empty;
    public string              LastName         { get; init; } = string.Empty;
    public string              Email            { get; init; } = string.Empty;
    public string              Country          { get; init; } = string.Empty;
    public MemberAccountStatus Status           { get; init; }
}
```

(El AdminAPI serializa los enum como texto — `JsonStringEnumConverter` en `Program.cs` — así que `Status` llega a la página como `"Active"`.)

- [ ] **Step 2: Pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class RankReportServiceTests
{
    private static readonly DateTime Hoy = new(2026, 11, 14);

    private static void Miembro(AppDbContext db, string id, string nombre, string apellido, string pais,
        MemberAccountStatus estado = MemberAccountStatus.Active, MemberType tipo = MemberType.Ambassador,
        bool corporativa = false) =>
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = id, FirstName = nombre, LastName = apellido,
            Email = $"{nombre.ToLowerInvariant()}@example.com", Country = pais, Status = estado, MemberType = tipo,
            IsCorporateAccount = corporativa, CreatedBy = "seed", CreationDate = Hoy, LastUpdateDate = Hoy
        });

    private static void Rango(AppDbContext db, string id, int rango, DateTime fecha) =>
        RangoDesdeQueriesTests.Rango(db, id, rango, fecha);

    /// <summary>
    /// AMB-1 Ana: sube y baja (de por vida Gold desde el 1-mar, vigente Silver desde el 1-jul). AMB-2 Beto (Suspended):
    /// Platinum. AMB-7 Fito (Inactive): de por vida Silver, vigente el rango 0. Fuera siempre: AMB-3 (solo rango 0),
    /// AMB-4 (Terminated), AMB-5 (corporativa), CUS-1 (cliente), AMB-6 (sin historial).
    /// </summary>
    private static AppDbContext Base()
    {
        var db = RangoDesdeQueriesTests.ConRangos();
        Miembro(db, "AMB-1", "Ana", "Lopez", "Bulgaria");
        Rango(db, "AMB-1", 1, new DateTime(2026, 1, 10));
        Rango(db, "AMB-1", 2, new DateTime(2026, 3, 1, 9, 0, 0));
        Rango(db, "AMB-1", 1, new DateTime(2026, 5, 1));
        Rango(db, "AMB-1", 2, new DateTime(2026, 6, 1));
        Rango(db, "AMB-1", 1, new DateTime(2026, 7, 1));
        Miembro(db, "AMB-2", "Beto", "Diaz", "France", MemberAccountStatus.Suspended);
        Rango(db, "AMB-2", 3, new DateTime(2026, 4, 1));
        Miembro(db, "AMB-3", "Carla", "Ruiz", "Japan");
        Rango(db, "AMB-3", 20, new DateTime(2026, 2, 1));
        Miembro(db, "AMB-4", "Dora", "Paz", "Chile", MemberAccountStatus.Terminated);
        Rango(db, "AMB-4", 2, new DateTime(2026, 2, 1));
        Miembro(db, "AMB-5", "MWR", "Corp.", "United States", corporativa: true);
        Rango(db, "AMB-5", 3, new DateTime(2026, 2, 1));
        Miembro(db, "CUS-1", "Eli", "Cruz", "Spain", tipo: MemberType.ExternalMember);
        Rango(db, "CUS-1", 1, new DateTime(2026, 2, 1));
        Miembro(db, "AMB-6", "Eva", "Mora", "Mexico", MemberAccountStatus.Pending);
        Miembro(db, "AMB-7", "Fito", "Gil", "Peru", MemberAccountStatus.Inactive);
        Rango(db, "AMB-7", 1, new DateTime(2026, 2, 1));
        Rango(db, "AMB-7", 20, new DateTime(2026, 8, 1));
        db.SaveChanges();
        return db;
    }

    private static RankReportService Servicio(AppDbContext db, IMemoryCache? cache = null) =>
        new(db, cache ?? new MemoryCache(new MemoryCacheOptions()));

    private static async Task<List<string>> Ids(AppDbContext db, RankReportFilter filtro, IMemoryCache? cache = null) =>
        (await Servicio(db, cache).GetAllAsync(filtro)).Select(f => f.MemberId).ToList();

    [Fact]
    public async Task Lifetime_da_el_mayor_rango_desde_la_primera_vez_y_excluye_a_quien_no_debe_salir()
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new RankReportFilter(RankReportMode.Lifetime));

        filas.Select(f => (f.MemberId, f.RankName, f.Since)).Should().Equal(
            ("AMB-2", "Platinum", new DateTime(2026, 4, 1)),
            ("AMB-1", "Gold", new DateTime(2026, 3, 1, 9, 0, 0)),
            ("AMB-7", "Silver", new DateTime(2026, 2, 1)));
    }

    [Fact]
    public async Task Current_da_la_fila_mas_reciente_y_saca_al_que_hoy_tiene_el_rango_0()
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new RankReportFilter(RankReportMode.Current));

        filas.Select(f => (f.MemberId, f.RankName, f.Since)).Should().Equal(
            ("AMB-2", "Platinum", new DateTime(2026, 4, 1)),
            ("AMB-1", "Silver", new DateTime(2026, 7, 1)));
    }

    [Fact]
    public async Task A_igual_rango_va_primero_la_fecha_mas_reciente()
    {
        using var db = Base();
        Miembro(db, "AMB-8", "Gabi", "Sol", "Peru");
        Rango(db, "AMB-8", 1, new DateTime(2026, 9, 1));
        await db.SaveChangesAsync();

        (await Ids(db, new RankReportFilter(RankReportMode.Current))).Should().Equal("AMB-2", "AMB-8", "AMB-1");
    }

    [Fact]
    public async Task La_fila_trae_nombre_email_pais_estado_y_rango()
    {
        using var db = Base();

        var ana = (await Servicio(db).GetAllAsync(new RankReportFilter(RankReportMode.Lifetime)))
            .Single(f => f.MemberId == "AMB-1");

        ana.Should().BeEquivalentTo(new
        {
            MemberId = "AMB-1", MemberName = "Ana Lopez", Email = "ana@example.com", Country = "Bulgaria",
            Status = MemberAccountStatus.Active, RankDefinitionId = 2, RankName = "Gold", RankSortOrder = 2
        });
    }

    [Fact]
    public async Task Filtra_por_rango()
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Current, RankDefinitionId: 1))).Should().Equal("AMB-1");

    [Fact]
    public async Task Filtra_por_estados()
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Lifetime, Statuses: new[] { MemberAccountStatus.Suspended })))
            .Should().Equal("AMB-2");

    [Fact]
    public async Task Terminated_nunca_aparece_aunque_se_pida()
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Lifetime,
                Statuses: new[] { MemberAccountStatus.Terminated, MemberAccountStatus.Active })))
            .Should().Equal("AMB-1");

    [Fact]
    public async Task El_rango_de_fechas_de_Since_es_por_dia_completo_e_inclusivo()
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Lifetime,
                SinceFrom: new DateTime(2026, 3, 1), SinceTo: new DateTime(2026, 3, 1))))
            .Should().Equal("AMB-1");

    [Theory]
    [InlineData("FRANCE", "AMB-2")]
    [InlineData("ana@", "AMB-1")]
    [InlineData("fito gi", "AMB-7")]
    public async Task Busca_por_nombre_email_o_pais(string termino, string esperado)
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Lifetime, Search: termino))).Should().Equal(esperado);

    [Fact]
    public async Task Pagina_y_cuenta_con_la_busqueda_de_la_rejilla()
    {
        using var db = Base();
        var filtro = new RankReportFilter(RankReportMode.Lifetime);

        var pagina2 = await Servicio(db).GetPageAsync(filtro, new GridDataRequest { Page = 2, PageSize = 2 });
        var buscado = await Servicio(db).GetPageAsync(filtro, new GridDataRequest { Search = "beto" });

        pagina2.TotalCount.Should().Be(3);
        pagina2.Items.Select(i => i.MemberId).Should().Equal("AMB-7");
        buscado.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task InvalidateAll_obliga_a_recalcular()
    {
        using var db = Base();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var filtro = new RankReportFilter(RankReportMode.Lifetime);

        var antes = await Ids(db, filtro, cache);
        Rango(db, "AMB-6", 2, new DateTime(2026, 10, 1));
        await db.SaveChangesAsync();
        var sinInvalidar = await Ids(db, filtro, cache);
        RankReportService.InvalidateAll();
        var despues = await Ids(db, filtro, cache);

        antes.Should().HaveCount(3);
        sinInvalidar.Should().HaveCount(3);
        despues.Should().HaveCount(4).And.Contain("AMB-6");
    }
}
```

- [ ] **Step 3: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~RankReportServiceTests"`
Expected: FAIL de compilación («RankReportService no existe»).

- [ ] **Step 4: Implementar el servicio**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IRankReportService
{
    /// <summary>Una página. La búsqueda llega en <see cref="GridDataRequest.Search"/> (la del filtro se ignora).</summary>
    Task<PagedResult<RankReportRowView>> GetPageAsync(RankReportFilter filter, GridDataRequest request,
        CancellationToken ct = default);

    /// <summary>Todas las filas que cumplen el filtro (exportación).</summary>
    Task<IReadOnlyList<RankReportRowView>> GetAllAsync(RankReportFilter filter, CancellationToken ct = default);
}

/// <summary>
/// Lifetime Rank y Current Rank: un Ambassador por fila con su rango (de por vida o vigente) y desde cuándo lo tiene.
/// Fuera: el rango 0, quien no tiene historial, Terminated, las cuentas corporativas y los clientes. La lista completa
/// de cada modo sale de UNA consulta SQL (<see cref="SourceQuery"/>) y se guarda 10 minutos en memoria; buscar, filtrar
/// y paginar trabajan sobre esa lista. Un cambio del indicador de cuenta corporativa la invalida con
/// <see cref="InvalidateAll"/>; un rango nuevo o un cambio de estado se ven, como mucho, 10 minutos después.
/// </summary>
public class RankReportService : IRankReportService
{
    /// <summary>Los estados que pueden aparecer (todos menos Terminated); también el valor por defecto del filtro.</summary>
    public static readonly MemberAccountStatus[] VisibleStatuses =
    {
        MemberAccountStatus.Active, MemberAccountStatus.Inactive, MemberAccountStatus.Suspended, MemberAccountStatus.Pending
    };

    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    // Solo hay dos claves (una por modo): un cerrojo basta para que un pedido en frio calcule y el resto espere.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    // Cada entrada lleva el token vigente al calcular; InvalidateAll lo cancela y las vence todas.
    private static CancellationTokenSource _invalidation = new();

    /// <summary>Descarta las listas guardadas (p. ej. al cambiar el indicador de cuenta corporativa).</summary>
    public static void InvalidateAll() => Interlocked.Exchange(ref _invalidation, new CancellationTokenSource()).Cancel();

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public RankReportService(AppDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    /// <summary>Sin estados = los visibles; Terminated se descarta siempre.</summary>
    public static IReadOnlyCollection<MemberAccountStatus> NormalizeStatuses(IEnumerable<MemberAccountStatus>? statuses)
    {
        var set = (statuses ?? Array.Empty<MemberAccountStatus>())
            .Where(s => s != MemberAccountStatus.Terminated).Distinct().ToArray();
        return set.Length == 0 ? VisibleStatuses : set;
    }

    /// <summary>La consulta SQL del modo, con las exclusiones (expuesta para comprobar su traducción y medirla).</summary>
    public static IQueryable<RankReportSourceRow> SourceQuery(AppDbContext db, RankReportMode mode)
    {
        var ranks = mode == RankReportMode.Lifetime ? db.LifetimeRankSinceRows() : db.CurrentRankSinceRows();
        return from r in ranks
               where r.SortOrder > 0
               join d in db.RankDefinitions.AsNoTracking() on r.RankDefinitionId equals d.Id
               join m in db.MemberProfiles.AsNoTracking() on r.MemberId equals m.MemberId
               where m.MemberType == MemberType.Ambassador
                  && m.Status != MemberAccountStatus.Terminated
                  && !m.IsCorporateAccount
               select new RankReportSourceRow
               {
                   MemberId         = r.MemberId,
                   RankDefinitionId = r.RankDefinitionId,
                   RankName         = d.Name,
                   SortOrder        = r.SortOrder,
                   Since            = r.Since,
                   FirstName        = m.FirstName,
                   LastName         = m.LastName,
                   Email            = m.Email,
                   Country          = m.Country,
                   Status           = m.Status
               };
    }

    public async Task<PagedResult<RankReportRowView>> GetPageAsync(RankReportFilter filter, GridDataRequest request,
        CancellationToken ct = default)
    {
        var rows = Apply(await ListAsync(filter.Mode, ct), filter with { Search = request.Search });
        var (page, pageSize) = GridQueryableExtensions.ResolvePage(request);
        return new PagedResult<RankReportRowView>
        {
            Items      = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = rows.Count,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<IReadOnlyList<RankReportRowView>> GetAllAsync(RankReportFilter filter, CancellationToken ct = default) =>
        Apply(await ListAsync(filter.Mode, ct), filter);

    /// <summary>Filtra la lista guardada. Devuelve siempre una lista nueva: nunca la cacheada.</summary>
    private static List<RankReportRowView> Apply(IReadOnlyList<RankReportRowView> rows, RankReportFilter f)
    {
        var statuses = NormalizeStatuses(f.Statuses);
        var from     = f.SinceFrom?.Date;
        var to       = f.SinceTo?.Date.AddDays(1);                         // hasta incluye su día entero
        var term     = string.IsNullOrWhiteSpace(f.Search) ? null : f.Search.Trim();

        return rows.Where(r =>
                statuses.Contains(r.Status)
                && (f.RankDefinitionId is null || r.RankDefinitionId == f.RankDefinitionId)
                && (from is null || r.Since >= from)
                && (to is null || r.Since < to)
                && (term is null
                    || r.MemberName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Country.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private async Task<IReadOnlyList<RankReportRowView>> ListAsync(RankReportMode mode, CancellationToken ct)
    {
        var key = $"reports:ranks:{mode}";
        if (_cache.TryGetValue(key, out IReadOnlyList<RankReportRowView>? cached) && cached is not null) return cached;

        await Gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(key, out cached) && cached is not null) return cached;
            // El token se toma ANTES de leer: una invalidacion durante el calculo deja la entrada ya vencida.
            var token  = Volatile.Read(ref _invalidation).Token;
            var source = await SourceQuery(_db, mode).ToListAsync(ct);
            IReadOnlyList<RankReportRowView> rows = source
                .OrderByDescending(r => r.SortOrder)
                .ThenByDescending(r => r.Since)
                .ThenBy(r => r.MemberId, StringComparer.Ordinal)
                .Select(r => new RankReportRowView
                {
                    MemberId         = r.MemberId,
                    MemberName       = $"{r.FirstName} {r.LastName}".Trim(),
                    Email            = r.Email,
                    Country          = r.Country,
                    Status           = r.Status,
                    RankDefinitionId = r.RankDefinitionId,
                    RankName         = r.RankName,
                    RankSortOrder    = r.SortOrder,
                    Since            = r.Since
                }).ToList().AsReadOnly();

            _cache.Set(key, rows, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheFor }
                .AddExpirationToken(new CancellationChangeToken(token)));
            return rows;
        }
        finally
        {
            Gate.Release();
        }
    }
}
```

- [ ] **Step 5: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~RankReportServiceTests"`
Expected: PASS (13: 10 hechos + 3 casos de la teoría).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Reports/RankReportViews.cs MLMConquerorGlobalEdition.Repository/Services/Reports/RankReportService.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/RankReportServiceTests.cs
git commit -m "Reportes: servicio de Lifetime Rank y Current Rank (solo Ambassadors, sin rango 0, sin historial, cancelados ni cuentas corporativas; filtros de busqueda, rango, estado y fecha; lista cacheada por modo)"
```

---

### Task 3: Traducción a SQL Server y medición contra la base de escala

**Files:**
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerReporteDeRangosTests.cs`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Lifetime Rank y Current Rank se traducen a SQL Server (la agregación por miembro, la antijoin del vigente y la unión
/// con los perfiles) y eligen lo mismo que en memoria. La segunda prueba escribe el SQL en la salida para medirlo.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerReporteDeRangosTests
{
    private readonly ITestOutputHelper _salida;

    public SqlServerReporteDeRangosTests(ITestOutputHelper salida) => _salida = salida;

    private static void Rango(AppDbContext db, string memberId, int rankId, DateTime fecha) =>
        db.MemberRankHistories.Add(new MemberRankHistory
        {
            MemberId = memberId, RankDefinitionId = rankId, AchievedAt = fecha,
            CreationDate = fecha, LastUpdateDate = fecha, CreatedBy = "prueba"
        });

    [SqlServerFact]
    public Task Los_dos_modos_se_traducen_y_eligen_bien() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId("RK");
        ReportesDb.Miembro(db, yo);
        Rango(db, yo, 1, new DateTime(2026, 1, 10));     // Silver
        Rango(db, yo, 2, new DateTime(2026, 3, 1));      // Gold, la primera vez
        Rango(db, yo, 1, new DateTime(2026, 5, 1));
        Rango(db, yo, 2, new DateTime(2026, 6, 1));
        Rango(db, yo, 1, new DateTime(2026, 7, 1));      // vigente: Silver
        await db.SaveChangesAsync();
        var servicio = new RankReportService(db, new MemoryCache(new MemoryCacheOptions()));
        var correo = $"{yo.ToLowerInvariant()}@prueba.test";

        var porVida = (await servicio.GetAllAsync(new RankReportFilter(RankReportMode.Lifetime, Search: correo))).Single();
        var vigente = (await servicio.GetAllAsync(new RankReportFilter(RankReportMode.Current, Search: correo))).Single();

        (porVida.RankDefinitionId, porVida.Since).Should().Be((2, new DateTime(2026, 3, 1)));
        (vigente.RankDefinitionId, vigente.Since).Should().Be((1, new DateTime(2026, 7, 1)));
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public void Las_consultas_se_traducen_a_SQL()
    {
        using var db = SqlServerReportes.Db();
        foreach (var modo in new[] { RankReportMode.Lifetime, RankReportMode.Current })
        {
            var sql = RankReportService.SourceQuery(db, modo).ToQueryString();
            sql.Should().Contain("[MemberProfiles]");
            _salida.WriteLine($"-- {modo}{Environment.NewLine}{sql}{Environment.NewLine}");
        }
    }
}
```

- [ ] **Step 2: Correr**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~SqlServerReporteDeRangosTests" --logger "console;verbosity=detailed"`
Expected: PASS (2), o SKIPPED si no hay SQL Server local. Si falla con «could not be translated», la composición de `LifetimeRankSinceRows` no se tradujo: no seguir, revisarla con Nick.

- [ ] **Step 3: Medir contra la base de escala (solo lectura)**

1. Copiar de la salida del Step 2 el SQL de cada modo a `%TEMP%\rangos-lifetime.sql` y `%TEMP%\rangos-current.sql` (fuera del repo).
2. La base de escala no tiene la migración `CuentaCorporativa`: quitar del SQL la condición `AND [m].[IsCorporateAccount] = CAST(0 AS bit)` (o como la haya escrito EF, `NOT ([m].[IsCorporateAccount] = CAST(1 AS bit))`).
3. Anteponer `SET NOCOUNT ON; SET STATISTICS TIME, IO ON;` y envolver el SELECT en `SELECT COUNT(*) FROM ( ... ) q;` para no medir el envío de filas; después correrlo también sin envolver.
4. Run (tres veces cada uno; `-I` es obligatorio: sin `QUOTED_IDENTIFIER ON` SQL Server no usa índices filtrados):
   `sqlcmd -S . -E -C -I -d MLMConquerorGlobalEditionDb_Escala -i "%TEMP%\rangos-lifetime.sql"`
   `sqlcmd -S . -E -C -I -d MLMConquerorGlobalEditionDb_Escala -i "%TEMP%\rangos-current.sql"`
   (Si `sqlcmd` rechaza `-i` con «-E and -U/-P are mutually exclusive», pasar el contenido con `-Q "$(cat archivo)"` desde Git Bash.)
5. Anotar en «Medidas» (final de este plan): filas, lecturas lógicas, CPU y tiempo de cada corrida.

Expected: < 2 s por modo con la máquina sin otra carga. Si no se cumple, anotarlo y avisar a Nick antes de la Tarea 5: la alternativa de la especificación es guardar rango vigente y de por vida en la tabla de estadísticas del miembro (ver «Preguntas abiertas», 1). La caché de 10 minutos ya evita repetir el coste en cada página.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerReporteDeRangosTests.cs docs/superpowers/plans/2026-11-14-reportes-admin-B-rangos-y-paises.md
git commit -m "Reportes: pruebas contra SQL Server de Lifetime Rank y Current Rank y medidas contra la base de escala"
```

---

### Task 4: Endpoints de los reportes de rango

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminRankStandingsReportController.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberCorporateAccountController.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs` (junto al registro de `IEarningsRankingService`, línea ~108)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminRankStandingsReportControllerTests.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/RankReportServiceTests.cs` (una prueba más)

Contrato:
- `POST api/v1/admin/reports/ranks/grid?mode=Lifetime|Current&rankId=2&statuses=Active&statuses=Pending&sinceFrom=2026-01-01&sinceTo=2026-11-14` con `GridDataRequest` en el cuerpo (la búsqueda va en `Search`) → `ApiResponse<PagedResult<RankReportRowView>>`. Sin `mode` = Lifetime.
- `GET api/v1/admin/reports/ranks/export?mode=...&rankId=...&statuses=...&sinceFrom=...&sinceTo=...&search=...` → CSV `lifetime-rank-yyyy-MM-dd.csv` o `current-rank-yyyy-MM-dd.csv` (fecha de hoy, hora del servidor). Columnas: Ambassador, Email, Country, Status, «Lifetime Rank»/«Current Rank», «… Since».
- Clase y exportación: SuperAdmin y Admin.

- [ ] **Step 1: Pruebas que fallan**

```csharp
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminRankStandingsReportControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 30, 0);

    private static (AdminRankStandingsReportController C, Mock<IRankReportService> S) Montar()
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        var servicio = new Mock<IRankReportService>();
        return (new AdminRankStandingsReportController(servicio.Object, new CsvReportWriter(), reloj.Object), servicio);
    }

    [Fact]
    public async Task La_rejilla_pasa_los_filtros_al_servicio()
    {
        var (c, s) = Montar();
        var pedido = new GridDataRequest { Search = "ana" };
        var desde  = new DateTime(2026, 1, 1);
        var hasta  = new DateTime(2026, 11, 14);
        s.Setup(x => x.GetPageAsync(
                It.Is<RankReportFilter>(f => f.Mode == RankReportMode.Current && f.RankDefinitionId == 2
                    && f.Statuses!.SequenceEqual(new[] { MemberAccountStatus.Active })
                    && f.SinceFrom == desde && f.SinceTo == hasta),
                pedido, It.IsAny<CancellationToken>()))
         .ReturnsAsync(new PagedResult<RankReportRowView>());

        var r = await c.Grid(pedido, RankReportMode.Current, 2, new[] { MemberAccountStatus.Active }, desde, hasta, default);

        r.Should().BeOfType<OkObjectResult>();
        s.VerifyAll();
    }

    [Fact]
    public async Task Exportar_da_un_csv_con_las_columnas_del_modo()
    {
        var (c, s) = Montar();
        s.Setup(x => x.GetAllAsync(It.Is<RankReportFilter>(f => f.Mode == RankReportMode.Lifetime && f.Search == "ana"),
                It.IsAny<CancellationToken>()))
         .ReturnsAsync(new[]
         {
             new RankReportRowView
             {
                 MemberId = "AMB-1", MemberName = "Ana Lopez", Email = "ana@example.com", Country = "Bulgaria",
                 Status = MemberAccountStatus.Active, RankDefinitionId = 2, RankName = "Gold", RankSortOrder = 2,
                 Since = new DateTime(2026, 3, 1, 9, 0, 0)
             }
         });

        var r = await c.Export(RankReportMode.Lifetime, null, null, null, null, "ana", default);

        var archivo = r.Should().BeOfType<FileContentResult>().Subject;
        archivo.ContentType.Should().Be(CsvReportWriter.ContentType);
        archivo.FileDownloadName.Should().Be("lifetime-rank-2026-11-14.csv");
        Encoding.UTF8.GetString(archivo.FileContents, 3, archivo.FileContents.Length - 3).Should().Be(
            "Ambassador,Email,Country,Status,Lifetime Rank,Lifetime Rank Since\r\n" +
            "Ana Lopez,ana@example.com,Bulgaria,Active,Gold,2026-03-01\r\n");
    }

    [Fact]
    public async Task Exportar_current_usa_su_nombre_de_archivo()
    {
        var (c, s) = Montar();
        s.Setup(x => x.GetAllAsync(It.IsAny<RankReportFilter>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Array.Empty<RankReportRowView>());

        var r = await c.Export(RankReportMode.Current, null, null, null, null, null, default);

        r.Should().BeOfType<FileContentResult>().Subject.FileDownloadName.Should().Be("current-rank-2026-11-14.csv");
    }

    [Fact]
    public void Leer_y_exportar_es_de_SuperAdmin_y_Admin()
    {
        typeof(AdminRankStandingsReportController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin");
        typeof(AdminRankStandingsReportController).GetMethod(nameof(AdminRankStandingsReportController.Export))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
    }
}
```

Y al final de la clase `RankReportServiceTests`:

```csharp
    [Fact]
    public async Task Marcar_la_cuenta_corporativa_saca_al_miembro_del_reporte_guardado()
    {
        using var db = Base();
        var cache = new MemoryCache(new MemoryCacheOptions());
        (await Ids(db, new RankReportFilter(RankReportMode.Lifetime), cache)).Should().Contain("AMB-1");

        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Hoy);
        var usuario = new Mock<ICurrentUserService>();
        usuario.SetupGet(u => u.UserId).Returns("admin-1");
        await new AdminMemberCorporateAccountController(db, reloj.Object, usuario.Object).Set("AMB-1", new(true), default);

        (await Ids(db, new RankReportFilter(RankReportMode.Lifetime), cache)).Should().NotContain("AMB-1");
    }
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminRankStandingsReportControllerTests|FullyQualifiedName~Marcar_la_cuenta_corporativa"`
Expected: FAIL de compilación («AdminRankStandingsReportController no existe»). Tras el Step 3 (sin el Step 4), `Marcar_la_cuenta_corporativa_saca_al_miembro_del_reporte_guardado` falla: el reporte guardado sigue mostrando a AMB-1.

- [ ] **Step 3: Implementar el controlador**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Lifetime Rank y Current Rank (un endpoint con el parámetro mode). Lectura y exportación a CSV para SuperAdmin y Admin.
/// No confundir con AdminRankReportsController (api/v1/admin/ranks/reports: primeros logros y antigüedad).
/// </summary>
[ApiController]
[Route("api/v1/admin/reports/ranks")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminRankStandingsReportController : ControllerBase
{
    private readonly IRankReportService _ranks;
    private readonly CsvReportWriter    _csv;
    private readonly IDateTimeProvider  _clock;

    public AdminRankStandingsReportController(IRankReportService ranks, CsvReportWriter csv, IDateTimeProvider clock)
    {
        _ranks = ranks;
        _csv   = csv;
        _clock = clock;
    }

    /// <summary>POST reports/ranks/grid?mode=&amp;rankId=&amp;statuses=&amp;sinceFrom=&amp;sinceTo= — la búsqueda va en el cuerpo.</summary>
    [HttpPost("grid")]
    public async Task<IActionResult> Grid([FromBody] GridDataRequest request, [FromQuery] RankReportMode mode,
        [FromQuery] int? rankId, [FromQuery] MemberAccountStatus[]? statuses, [FromQuery] DateTime? sinceFrom,
        [FromQuery] DateTime? sinceTo, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<RankReportRowView>>.Ok(await _ranks.GetPageAsync(
            new RankReportFilter(mode, null, rankId, statuses, sinceFrom, sinceTo), request, ct)));

    /// <summary>GET reports/ranks/export?mode=&amp;rankId=&amp;statuses=&amp;sinceFrom=&amp;sinceTo=&amp;search= — CSV.</summary>
    [HttpGet("export")]
    // Punto de enganche para acotar la exportacion a un rol propio mas adelante (permiso reports.export).
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Export([FromQuery] RankReportMode mode, [FromQuery] int? rankId,
        [FromQuery] MemberAccountStatus[]? statuses, [FromQuery] DateTime? sinceFrom, [FromQuery] DateTime? sinceTo,
        [FromQuery] string? search, CancellationToken ct)
    {
        var rows  = await _ranks.GetAllAsync(new RankReportFilter(mode, search, rankId, statuses, sinceFrom, sinceTo), ct);
        var label = mode == RankReportMode.Lifetime ? "Lifetime Rank" : "Current Rank";
        var bytes = _csv.Write(rows,
            ("Ambassador", r => r.MemberName), ("Email", r => r.Email), ("Country", r => r.Country),
            ("Status", r => r.Status.ToString()), (label, r => r.RankName), ($"{label} Since", r => r.Since));
        var file = $"{(mode == RankReportMode.Lifetime ? "lifetime" : "current")}-rank-{_clock.Now:yyyy-MM-dd}.csv";
        return File(bytes, CsvReportWriter.ContentType, file);
    }
}
```

- [ ] **Step 4: Invalidar también los rangos al marcar la cuenta corporativa**

En `AdminMemberCorporateAccountController.cs`, justo después de `EarningsRankingService.InvalidateAll();   // el ranking guardado ya no es valido`:

```csharp
            RankReportService.InvalidateAll();        // ni los reportes de rango guardados
```

- [ ] **Step 5: Registro en Program.cs**

Justo después de `builder.Services.AddSingleton<MLMConquerorGlobalEdition.AdminAPI.Reports.CsvReportWriter>();`:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Reports.IRankReportService,
                            MLMConquerorGlobalEdition.Repository.Services.Reports.RankReportService>();
```

- [ ] **Step 6: Correr las pruebas y compilar**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~Reports"`
Expected: PASS (todas las de la carpeta, también las del Plan A).
Run: `dotnet build MLMConquerorGlobalEdition.AdminAPI`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminRankStandingsReportControllerTests.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/RankReportServiceTests.cs
git commit -m "Admin: endpoints de Lifetime Rank y Current Rank (rejilla de servidor y exportacion a CSV) e invalidacion de su cache al marcar la cuenta corporativa"
```

---

### Task 5: Página de Lifetime Rank y Current Rank en AdminWeb

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Shared/MemberStatusFilter.razor`
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRankReport.razor`

Una página, dos rutas: `/admin/reports/lifetime-rank` y `/admin/reports/current-rank`. El filtro de estado es un componente porque también lo usa Top Countries (con `Terminated`).

- [ ] **Step 1: Filtro de estado (multiselección)**

```razor
@* Multiseleccion de estados de membresia para los reportes del admin. Ninguno marcado = todos (lo mismo que hace el
   servidor): al desmarcar el ultimo, se vuelven a marcar todos. *@

<div class="d-flex flex-wrap gap-1 align-items-center">
    <span class="small text-muted me-1">Status</span>
    @foreach (var option in Options)
    {
        var id = $"{IdPrefix}-{option}";
        <input type="checkbox" class="btn-check" id="@id" autocomplete="off"
               checked="@Selected.Contains(option)" @onchange="() => ToggleAsync(option)" />
        <label class="btn btn-sm btn-outline-primary" for="@id">@option</label>
    }
</div>

@code {
    /// <summary>Los estados que se ofrecen, con el nombre del enum (Active, Inactive, ...).</summary>
    [Parameter, EditorRequired] public IReadOnlyList<string> Options { get; set; } = Array.Empty<string>();

    /// <summary>El conjunto del anfitrion; el componente lo modifica en sitio.</summary>
    [Parameter, EditorRequired] public HashSet<string> Selected { get; set; } = new();

    /// <summary>Prefijo de los id de los checkbox, para que dos filtros en una pagina no choquen.</summary>
    [Parameter] public string IdPrefix { get; set; } = "status";

    [Parameter] public EventCallback Changed { get; set; }

    private async Task ToggleAsync(string option)
    {
        if (!Selected.Remove(option)) Selected.Add(option);
        if (Selected.Count == 0)
            foreach (var o in Options) Selected.Add(o);
        await Changed.InvokeAsync();
    }
}
```

- [ ] **Step 2: Escribir la página**

```razor
@page "/admin/reports/lifetime-rank"
@page "/admin/reports/current-rank"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin,Admin")]
@using MLMConquerorGlobalEdition.SharedComponents.Components.Grid
@using MLMConquerorGlobalEdition.SharedComponents.Constants
@using MLMConquerorGlobalEdition.SharedComponents.Services
@using Syncfusion.Blazor.Grids
@using Syncfusion.Blazor.Data
@inject HttpClient Http
@inject NavigationManager Nav
@inject IJSRuntime Js
@inject IViewContextService ViewContext

<PageTitle>@RankLabel</PageTitle>

<h4 class="mb-1">@RankLabel</h4>
<p class="text-muted mb-4">@Subtitle</p>

<div class="d-flex flex-wrap gap-2 align-items-end mb-2">
    <div class="flex-grow-1" style="min-width:240px">
        <input class="form-control" placeholder="Search name, email or country..." @bind="_search" @bind:event="oninput"
               @onkeyup="OnSearchKeyAsync" />
    </div>
    <div>
        <button class="btn btn-primary" @onclick="ApplySearchAsync"><i class="ri-search-line me-1"></i>Search</button>
    </div>
    <div>
        <label class="form-label small mb-1">Rank</label>
        <select class="form-select" @bind="_rankId" @bind:after="RefreshAsync">
            <option value="">All ranks</option>
            @foreach (var r in _ranks) { <option value="@r.Id">@r.Name</option> }
        </select>
    </div>
    <div>
        <label class="form-label small mb-1">@RankLabel since — from</label>
        <input type="date" class="form-control" @bind="_sinceFrom" @bind:after="RefreshAsync" />
    </div>
    <div>
        <label class="form-label small mb-1">to</label>
        <input type="date" class="form-control" @bind="_sinceTo" @bind:after="RefreshAsync" />
    </div>
    @if (ViewContext.HasPermission(Permissions.Reports.Export))
    {
        <button class="btn btn-outline-success" @onclick="ExportAsync" disabled="@_exporting">
            <i class="ri-file-download-line me-1"></i>Export CSV
        </button>
    }
</div>

<div class="mb-3">
    <MemberStatusFilter Options="Statuses" Selected="_statuses" IdPrefix="rank-status" Changed="RefreshAsync" />
</div>

@if (_error is not null)
{
    <div class="alert alert-danger">@_error</div>
}

@if (_total is not null)
{
    <div class="text-muted small mb-2">@_total.Value.ToString("N0") results</div>
}

<SfGrid TValue="RankRow" @ref="_grid" @key="IsLifetime" AllowPaging="true">
    <SfDataManager Adaptor="Adaptors.CustomAdaptor">
        <ServerGridAdaptor TValue="RankRow" Http="Http" Endpoint="api/v1/admin/reports/ranks/grid"
                           QueryStringProvider="Query" OnCount="OnCountAsync" />
    </SfDataManager>
    <GridPageSettings PageSize="20" PageSizes="@(new int[] { 20, 50, 100 })" />
    <GridColumns>
        <GridColumn Field="@nameof(RankRow.MemberName)" HeaderText="Ambassador" />
        <GridColumn Field="@nameof(RankRow.Email)" HeaderText="Email" />
        <GridColumn Field="@nameof(RankRow.Country)" HeaderText="Country" />
        <GridColumn Field="@nameof(RankRow.Status)" HeaderText="Status" Width="120">
            <Template>
                @{ var status = (context as RankRow)!.Status; }
                <span class="badge @(status == "Active" ? "bg-success" : "bg-secondary")">@status</span>
            </Template>
        </GridColumn>
        <GridColumn Field="@nameof(RankRow.RankName)" HeaderText="@RankLabel" />
        <GridColumn Field="@nameof(RankRow.Since)" HeaderText="@($"{RankLabel} Since")" Width="170">
            <Template>@((context as RankRow)!.Since.ToString("yyyy-MM-dd"))</Template>
        </GridColumn>
    </GridColumns>
</SfGrid>

@code {
    // Terminated no se ofrece: nunca aparece en estos reportes.
    private static readonly IReadOnlyList<string> Statuses = new[] { "Active", "Inactive", "Suspended", "Pending" };

    private SfGrid<RankRow>? _grid;
    private readonly HashSet<string> _statuses = new(Statuses);
    private List<RankOption> _ranks = new();
    private string    _search = string.Empty;
    private string    _appliedSearch = string.Empty;   // el termino que la rejilla ya aplico: el que usa la exportacion
    private int?      _rankId;
    private DateTime? _sinceFrom;
    private DateTime? _sinceTo;
    private int?      _total;
    private bool?     _lastIsLifetime;
    private bool      _exporting;
    private string?   _error;

    private bool   IsLifetime => Nav.Uri.Contains("/lifetime-rank", StringComparison.OrdinalIgnoreCase);
    private string Mode       => IsLifetime ? "Lifetime" : "Current";
    private string RankLabel  => IsLifetime ? "Lifetime Rank" : "Current Rank";
    private string Subtitle   => IsLifetime
        ? "Highest rank each Ambassador has reached, and the first day they reached it."
        : "Rank each Ambassador holds today, and since when.";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiResp<List<RankOption>>>("api/v1/admin/rank-definitions");
            _ranks = (resp?.Data ?? new())
                .Where(r => r.SortOrder > 0 && r.Status == "Active")
                .OrderBy(r => r.SortOrder)
                .ToList();
        }
        catch { /* sin la lista, el filtro queda en «All ranks» */ }
    }

    protected override void OnParametersSet()
    {
        var isLifetime = IsLifetime;
        if (_lastIsLifetime is not null && _lastIsLifetime != isLifetime)
        {
            _search = _appliedSearch = string.Empty;
            _rankId = null;
            _sinceFrom = _sinceTo = null;
            _statuses.Clear();
            foreach (var s in Statuses) _statuses.Add(s);
            _total = null;
            _error = null;
        }
        _lastIsLifetime = isLifetime;
    }

    private async Task OnCountAsync(int count)
    {
        _total = count;
        await InvokeAsync(StateHasChanged);
    }

    private string Query()
    {
        var q = new List<string> { $"mode={Mode}" };
        if (_rankId is not null)    q.Add($"rankId={_rankId}");
        if (_sinceFrom is not null) q.Add($"sinceFrom={_sinceFrom:yyyy-MM-dd}");
        if (_sinceTo is not null)   q.Add($"sinceTo={_sinceTo:yyyy-MM-dd}");
        q.AddRange(_statuses.Select(s => $"statuses={s}"));
        return string.Join("&", q);
    }

    private async Task RefreshAsync()
    {
        if (_grid is not null) await _grid.Refresh();
    }

    // Como Members y Earnings: la busqueda se lanza con Enter o con el boton, no en cada tecla.
    private async Task OnSearchKeyAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await ApplySearchAsync();
    }

    private async Task ApplySearchAsync()
    {
        _appliedSearch = (_search ?? string.Empty).Trim();
        if (_grid is not null) await _grid.SearchAsync(_appliedSearch);
    }

    private async Task ExportAsync()
    {
        _exporting = true;
        _error = null;
        try
        {
            var url   = $"api/v1/admin/reports/ranks/export?{Query()}&search={Uri.EscapeDataString(_appliedSearch)}";
            var bytes = await Http.GetByteArrayAsync(url);
            var file  = $"{(IsLifetime ? "lifetime" : "current")}-rank-{DateTime.Today:yyyy-MM-dd}.csv";
            await Js.InvokeVoidAsync("mlmDownloadFile", file, "text/csv", Convert.ToBase64String(bytes));
        }
        catch (Exception ex)
        {
            _error = $"Export failed: {ex.Message}";
        }
        finally { _exporting = false; }
    }

    private sealed record RankOption(int Id, string Name, int SortOrder, string Status);
    private sealed record ApiResp<T>(bool Success, T? Data, IEnumerable<string>? Errors);

    private sealed class RankRow
    {
        public string   MemberId   { get; set; } = string.Empty;
        public string   MemberName { get; set; } = string.Empty;
        public string   Email      { get; set; } = string.Empty;
        public string   Country    { get; set; } = string.Empty;
        public string   Status     { get; set; } = string.Empty;
        public string   RankName   { get; set; } = string.Empty;
        public DateTime Since      { get; set; }
    }
}
```

`DateTime.Today` en la página es la hora del servidor de AdminWeb (Blazor Server), igual que en `AdminEarningsReport.razor`; el nombre del archivo que vale es el que manda el servidor.

- [ ] **Step 3: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/Components/Shared/MemberStatusFilter.razor MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRankReport.razor
git commit -m "Admin: pagina de Lifetime Rank y Current Rank con busqueda, rango, fechas, filtro de estado, conteo y exportacion solo con permiso"
```

---

### Task 6: Tabla de la foto diaria de miembros por país

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberCountrySnapshot.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/MemberCountrySnapshotConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs` (junto a `MemberStatisticHistories`, línea ~62)
- Create (EF): `MLMConquerorGlobalEdition.Repository/Migrations/<timestamp>_FotoDiariaDeMiembrosPorPais.cs`
- Regenerate: `DeploymentScripts/migrate-rds.sql`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/MemberCountrySnapshotModelTests.cs`

- [ ] **Step 1: Prueba que falla**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>Una fila por día, país, tipo de miembro y estado (especificación: clave única de la foto diaria).</summary>
public class MemberCountrySnapshotModelTests
{
    [Fact]
    public void Clave_unica_por_dia_pais_tipo_y_estado()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Model.FindEntityType(typeof(MemberCountrySnapshot))!.GetIndexes()
            .Should().ContainSingle(i => i.IsUnique
                && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "SnapshotDate", "Country", "MemberType", "Status" }));
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~MemberCountrySnapshotModelTests"`
Expected: FAIL de compilación («MemberCountrySnapshot no existe»).

- [ ] **Step 3: Entidad**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>
/// Foto diaria de miembros por país (reporte Top Countries): cuántos miembros había al cierre de <see cref="SnapshotDate"/>
/// por país, tipo y estado. La escribe MemberCountrySnapshotWriter (job nocturno del BizCenter o foto manual del admin);
/// recalcular un día reemplaza sus filas. No se reconstruye el pasado: la primera foto es la del día en que se activa.
/// </summary>
public class MemberCountrySnapshot : AuditChangesLongKey
{
    /// <summary>El día de la foto (columna date, hora del servidor).</summary>
    public DateTime SnapshotDate { get; set; }

    /// <summary>El mismo valor que MemberProfile.Country.</summary>
    public string Country { get; set; } = string.Empty;

    public MemberType MemberType { get; set; }
    public MemberAccountStatus Status { get; set; }
    public int MemberCount { get; set; }
}
```

- [ ] **Step 4: Configuración**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MemberCountrySnapshotConfiguration : IEntityTypeConfiguration<MemberCountrySnapshot>
{
    public void Configure(EntityTypeBuilder<MemberCountrySnapshot> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SnapshotDate).HasColumnType("date");
        builder.Property(x => x.Country).IsRequired().HasMaxLength(100);   // como MemberProfiles.Country
        builder.Property(x => x.CreatedBy).HasMaxLength(100);

        // Una fila por dia, pais, tipo y estado. Tambien sirve a todas las lecturas del reporte (por dia o rango de dias).
        builder.HasIndex(x => new { x.SnapshotDate, x.Country, x.MemberType, x.Status })
               .IsUnique()
               .HasDatabaseName("UX_MemberCountrySnapshots_Date_Country_Type_Status");
    }
}
```

En `AppDbContext.cs`, después de `public DbSet<MemberStatisticHistoryEntity> MemberStatisticHistories => Set<MemberStatisticHistoryEntity>();`:

```csharp
    public DbSet<MemberCountrySnapshot> MemberCountrySnapshots => Set<MemberCountrySnapshot>();
```

- [ ] **Step 5: Correr la prueba**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~MemberCountrySnapshotModelTests"`
Expected: PASS (1).

- [ ] **Step 6: Migración, script y base local**

Run: `dotnet ef migrations add FotoDiariaDeMiembrosPorPais --project MLMConquerorGlobalEdition.Repository`
Expected: se crea `Migrations/<timestamp>_FotoDiariaDeMiembrosPorPais.cs` con un único `CreateTable("MemberCountrySnapshots", ...)` (Id bigint identity, SnapshotDate `date`, Country nvarchar(100), MemberType int, Status int, MemberCount int, CreationDate datetime2, CreatedBy nvarchar(100)) y el `CreateIndex` único. Si trae otros cambios, detenerse: el modelo tenía deriva.
Run: `dotnet ef migrations script --idempotent --project MLMConquerorGlobalEdition.Repository -o DeploymentScripts/migrate-rds.sql`
Expected: el diff de `migrate-rds.sql` solo añade el bloque de `FotoDiariaDeMiembrosPorPais`.
Run: `dotnet ef database update --project MLMConquerorGlobalEdition.Repository`
Expected: Done (tabla nueva y vacía; no hace falta `Command Timeout`). **No** se aplica a `MLMConquerorGlobalEditionDb_Escala`.
Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~Migraciones"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/Entities/Member/MemberCountrySnapshot.cs MLMConquerorGlobalEdition.Repository/Configurations/MemberCountrySnapshotConfiguration.cs MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs MLMConquerorGlobalEdition.Repository/Migrations DeploymentScripts/migrate-rds.sql MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/MemberCountrySnapshotModelTests.cs
git commit -m "Reportes: tabla de la foto diaria de miembros por pais, tipo y estado con clave unica y script de despliegue regenerado"
```

---

### Task 7: Escritor de la foto diaria

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/MemberCountrySnapshotWriter.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/MemberCountrySnapshotWriterTests.cs`

Reglas (especificación, Base común):
- Cuenta `MemberProfiles` (sin borrados: el filtro global ya quita `IsDeleted`) por `Country`, `MemberType`, `Status`.
- Idempotente: recalcular un día actualiza sus filas, añade las nuevas y borra las que ya no existen, en un solo `SaveChanges` (sin borrar e insertar la misma clave, que chocaría con el índice único). Otros días no se tocan.
- `CreationDate = IDateTimeProvider.Now`, `CreatedBy = "country-snapshot"`.

- [ ] **Step 1: Pruebas que fallan**

```csharp
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

        var n = await Escritor(db).WriteAsync(Dia.AddHours(23));

        n.Should().Be(4);
        (await Filas(db, Dia)).Should().BeEquivalentTo(new[]
        {
            ("US", Amb, MemberAccountStatus.Active, 2),
            ("US", Cli, MemberAccountStatus.Active, 1),
            ("US", Amb, MemberAccountStatus.Suspended, 1),
            ("Peru", Amb, MemberAccountStatus.Terminated, 1)
        });
        var fila = await db.MemberCountrySnapshots.FirstAsync();
        (fila.CreationDate, fila.CreatedBy).Should().Be((Ahora, MemberCountrySnapshotWriter.Author));
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
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~MemberCountrySnapshotWriterTests"`
Expected: FAIL de compilación («MemberCountrySnapshotWriter no existe»).

- [ ] **Step 3: Implementar**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IMemberCountrySnapshotWriter
{
    /// <summary>Escribe (o reescribe) la foto del día con el estado actual de los miembros. Devuelve las filas del día.</summary>
    Task<int> WriteAsync(DateTime snapshotDate, CancellationToken ct = default);
}

/// <summary>
/// Foto diaria de miembros por país, tipo y estado (Top Countries). Un GROUP BY sobre MemberProfiles (sin borrados) y
/// un solo SaveChanges que actualiza las filas del día que ya existen, añade las nuevas y borra las que sobran: es
/// idempotente y nunca borra e inserta la misma clave. Otros días no se tocan. Lo llaman el job nocturno del BizCenter
/// (el día que cerró) y la foto manual del admin (hoy).
/// </summary>
public class MemberCountrySnapshotWriter : IMemberCountrySnapshotWriter
{
    public const string Author = "country-snapshot";

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public MemberCountrySnapshotWriter(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<int> WriteAsync(DateTime snapshotDate, CancellationToken ct = default)
    {
        var day = snapshotDate.Date;

        var counts = await _db.MemberProfiles.AsNoTracking()
            .GroupBy(m => new { m.Country, m.MemberType, m.Status })
            .Select(g => new { g.Key.Country, g.Key.MemberType, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var existing = (await _db.MemberCountrySnapshots.Where(s => s.SnapshotDate == day).ToListAsync(ct))
            .ToDictionary(s => Key(s.Country, s.MemberType, s.Status));

        var now = _clock.Now;
        foreach (var c in counts)
        {
            if (existing.Remove(Key(c.Country, c.MemberType, c.Status), out var row))
            {
                row.MemberCount = c.Count;
                continue;
            }
            _db.MemberCountrySnapshots.Add(new MemberCountrySnapshot
            {
                SnapshotDate = day,
                Country      = c.Country,
                MemberType   = c.MemberType,
                Status       = c.Status,
                MemberCount  = c.Count,
                CreationDate = now,
                CreatedBy    = Author
            });
        }
        _db.MemberCountrySnapshots.RemoveRange(existing.Values);   // combinaciones que ese dia ya no tienen miembros

        await _db.SaveChangesAsync(ct);
        return counts.Count;
    }

    // SQL Server compara el pais sin mayusculas ni espacios finales (intercalacion CI): la clave en memoria hace lo mismo
    // para no insertar una fila que el indice unico consideraria repetida.
    private static string Key(string country, MemberType type, MemberAccountStatus status) =>
        $"{country.TrimEnd().ToUpperInvariant()}|{(int)type}|{(int)status}";
}
```

- [ ] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~MemberCountrySnapshotWriterTests"`
Expected: PASS (2).

- [ ] **Step 5: Medir el GROUP BY contra la base de escala (solo lectura)**

Run: `sqlcmd -S . -E -C -I -d MLMConquerorGlobalEditionDb_Escala -Q "SET NOCOUNT ON; SET STATISTICS TIME, IO ON; SELECT Country, MemberType, Status, COUNT(*) FROM MemberProfiles WHERE IsDeleted = 0 GROUP BY Country, MemberType, Status;"` (tres veces)
Expected: < 2 s (es un proceso nocturno; ya medido en la preparación del plan: 0,2–1,0 s). Anotar en «Medidas».

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Reports/MemberCountrySnapshotWriter.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/MemberCountrySnapshotWriterTests.cs
git commit -m "Reportes: escritor idempotente de la foto diaria de miembros por pais, tipo y estado"
```

---

### Task 8: Job nocturno de la foto en el BizCenter

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter/Jobs/MemberCountrySnapshotJob.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs` (registros, línea ~266, y `RecurringJob`, después de `member-statistic-snapshot`, línea ~444)
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/MemberCountrySnapshotJobTests.cs`

- [ ] **Step 1: Pruebas que fallan**

```csharp
using System.Globalization;
using System.Reflection;
using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Jobs;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>El job escribe el día que acaba de cerrar en hora del servidor, sea cual sea la hora a la que corre.</summary>
public class MemberCountrySnapshotJobTests
{
    [Theory]
    [InlineData("2026-11-14T00:30:00", "2026-11-13")]   // invierno: 05:30 UTC = 00:30 local
    [InlineData("2026-07-14T01:30:00", "2026-07-13")]   // verano: 05:30 UTC = 01:30 local
    [InlineData("2026-12-01T00:30:00", "2026-11-30")]   // cambio de mes
    public async Task Escribe_el_dia_que_cerro(string ahora, string esperado)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(DateTime.Parse(ahora, CultureInfo.InvariantCulture));
        var escritor = new Mock<IMemberCountrySnapshotWriter>();
        escritor.Setup(e => e.WriteAsync(DateTime.Parse(esperado, CultureInfo.InvariantCulture), It.IsAny<CancellationToken>()))
                .ReturnsAsync(12);

        await new MemberCountrySnapshotJob(escritor.Object, reloj.Object, NullLogger<MemberCountrySnapshotJob>.Instance)
            .ExecuteAsync(CancellationToken.None);

        escritor.VerifyAll();
    }

    [Fact]
    public void Va_en_la_cola_del_bizcenter()
        => typeof(MemberCountrySnapshotJob).GetCustomAttribute<QueueAttribute>()!.Queue.Should().Be("bizcenter");
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~MemberCountrySnapshotJobTests"`
Expected: FAIL de compilación («MemberCountrySnapshotJob no existe»).

- [ ] **Step 3: Implementar el job**

```csharp
using Hangfire;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// Foto diaria de miembros por pais (reporte Top Countries del admin). Cada noche escribe el dia que acaba de cerrar
/// (ayer en hora del servidor, IDateTimeProvider.Now) con el estado de ese momento; repetirlo reemplaza las filas del dia.
/// Se programa a las 05:30 UTC: con el servidor en UTC-4 (verano) son las 01:30 y en UTC-5 (invierno) las 00:30, siempre
/// despues de la medianoche local. El dia no sale de la hora del cron sino del reloj del servidor. Para relanzarlo:
/// panel de Hangfire del BizCenter (/hangfire) -> Recurring jobs -> member-country-snapshot -> Trigger now.
/// </summary>
[Queue("bizcenter")]
public class MemberCountrySnapshotJob
{
    private readonly IMemberCountrySnapshotWriter      _writer;
    private readonly IDateTimeProvider                 _clock;
    private readonly ILogger<MemberCountrySnapshotJob> _logger;

    public MemberCountrySnapshotJob(IMemberCountrySnapshotWriter writer, IDateTimeProvider clock,
        ILogger<MemberCountrySnapshotJob> logger)
    {
        _writer = writer;
        _clock  = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var day  = _clock.Now.Date.AddDays(-1);
        var rows = await _writer.WriteAsync(day, ct);
        _logger.LogInformation("MemberCountrySnapshotJob: {Rows} filas para {Day:yyyy-MM-dd}.", rows, day);
    }
}
```

- [ ] **Step 4: Registro y programación en Program.cs**

Después de `builder.Services.AddScoped<ApplyDualLegDeltasJob>();`:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Reports.IMemberCountrySnapshotWriter,
                            MLMConquerorGlobalEdition.Repository.Services.Reports.MemberCountrySnapshotWriter>();
builder.Services.AddScoped<MemberCountrySnapshotJob>();
```

Después del bloque `RecurringJob.AddOrUpdate<MemberStatisticSnapshotJob>(...)`:

```csharp
// Reportes (Top Countries): foto del dia que cerro. 05:30 UTC = 01:30 en verano (UTC-4) y 00:30 en invierno (UTC-5):
// siempre despues de la medianoche del servidor; el dia lo decide IDateTimeProvider.Now, no esta hora.
RecurringJob.AddOrUpdate<MemberCountrySnapshotJob>(
    "member-country-snapshot",
    job => job.ExecuteAsync(CancellationToken.None),
    "30 5 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "bizcenter" });
```

- [ ] **Step 5: Correr las pruebas y compilar**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~MemberCountrySnapshotJobTests"`
Expected: PASS (4).
Run: `dotnet build MLMConquerorGlobalEdition.BizCenter`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter/Jobs/MemberCountrySnapshotJob.cs MLMConquerorGlobalEdition.BizCenter/Program.cs MLMConquerorGlobalEdition.BizCenter.Tests/MemberCountrySnapshotJobTests.cs
git commit -m "BizCenter: job nocturno de la foto diaria de miembros por pais (dia que cerro en hora del servidor, 05:30 UTC en la cola bizcenter)"
```

---

### Task 9: Períodos de la gráfica (meses, semanas cortadas por el mes, días de una semana)

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/ReportPeriods.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ReportPeriodsTests.cs`

Reglas (especificación, 1b y Decisiones): las semanas empiezan el lunes y se cortan en los límites del mes (la primera va del día 1 al primer domingo; la última, del último lunes al cierre del mes). Noviembre de 2026 empieza en domingo: sus semanas son 1, 2–8, 9–15, 16–22, 23–29 y 30. Etiquetas en cultura invariante.

- [ ] **Step 1: Pruebas que fallan**

```csharp
using MLMConquerorGlobalEdition.Repository.Services.Reports;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class ReportPeriodsTests
{
    [Fact]
    public void Meses_completos_del_mes_inicial_al_final()
    {
        var meses = ReportPeriods.Months(new DateTime(2026, 9, 15), new DateTime(2026, 11, 14));

        meses.Select(m => m.Label).Should().Equal("Sep 2026", "Oct 2026", "Nov 2026");
        (meses[0].Start, meses[0].End).Should().Be((new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)));
        meses[^1].End.Should().Be(new DateTime(2026, 11, 30));
    }

    [Fact]
    public void Las_semanas_empiezan_el_lunes_y_se_cortan_en_el_mes()
    {
        var semanas = ReportPeriods.WeeksOfMonth(2026, 11);

        semanas.Select(s => (s.Start.Day, s.End.Day)).Should().Equal((1, 1), (2, 8), (9, 15), (16, 22), (23, 29), (30, 30));
        semanas.Select(s => s.Label).Should().Equal("Nov 1", "Nov 2-8", "Nov 9-15", "Nov 16-22", "Nov 23-29", "Nov 30");
    }

    [Theory]
    [InlineData("2026-11-14", "2026-11-09", "2026-11-15")]
    [InlineData("2026-11-30", "2026-11-30", "2026-11-30")]   // lunes 30: la semana se corta en el fin de mes
    [InlineData("2026-12-02", "2026-12-01", "2026-12-06")]   // la misma semana del calendario, del lado de diciembre
    public void La_semana_de_una_fecha_queda_dentro_de_su_mes(string fecha, string inicio, string fin)
    {
        var semana = ReportPeriods.WeekOf(DateTime.Parse(fecha));

        (semana.Start, semana.End).Should().Be((DateTime.Parse(inicio), DateTime.Parse(fin)));
    }

    [Fact]
    public void Los_dias_son_los_de_la_semana_cortada()
    {
        var completa = ReportPeriods.DaysOfWeek(new DateTime(2026, 11, 12));
        var cortada  = ReportPeriods.DaysOfWeek(new DateTime(2026, 12, 2));

        completa.Should().HaveCount(7);
        completa[0].Label.Should().Be("Mon Nov 9");
        cortada.Select(d => d.Start.Day).Should().Equal(1, 2, 3, 4, 5, 6);
        cortada[0].Label.Should().Be("Tue Dec 1");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~ReportPeriodsTests"`
Expected: FAIL de compilación («ReportPeriods no existe»).

- [ ] **Step 3: Implementar**

```csharp
using System.Globalization;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Un período de la gráfica: días completos e inclusivos [Start, End] y su etiqueta.</summary>
public readonly record struct ReportPeriod(DateTime Start, DateTime End, string Label);

/// <summary>
/// Períodos de las vistas de Top Countries. Las semanas empiezan el lunes y se cortan en los límites del mes, para que
/// cada semana pertenezca a un solo mes (decisión del 2026-11-14). Etiquetas en cultura invariante.
/// </summary>
public static class ReportPeriods
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Los meses completos desde el mes de <paramref name="from"/> hasta el de <paramref name="to"/>.</summary>
    public static IReadOnlyList<ReportPeriod> Months(DateTime from, DateTime to)
    {
        var list = new List<ReportPeriod>();
        for (var m = new DateTime(from.Year, from.Month, 1); m <= to.Date; m = m.AddMonths(1))
            list.Add(new ReportPeriod(m, m.AddMonths(1).AddDays(-1), m.ToString("MMM yyyy", Inv)));
        return list;
    }

    /// <summary>La semana (lunes a domingo, cortada por el mes) que contiene <paramref name="date"/>.</summary>
    public static ReportPeriod WeekOf(DateTime date)
    {
        var day    = date.Date;
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var first  = new DateTime(day.Year, day.Month, 1);
        var last   = first.AddMonths(1).AddDays(-1);
        var start  = monday < first ? first : monday;
        var end    = monday.AddDays(6) > last ? last : monday.AddDays(6);
        return new ReportPeriod(start, end, Label(start, end));
    }

    /// <summary>Las semanas del mes, de la que contiene el día 1 a la que contiene el último.</summary>
    public static IReadOnlyList<ReportPeriod> WeeksOfMonth(int year, int month)
    {
        var list = new List<ReportPeriod>();
        var last = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        for (var d = new DateTime(year, month, 1); d <= last;)
        {
            var week = WeekOf(d);
            list.Add(week);
            d = week.End.AddDays(1);
        }
        return list;
    }

    /// <summary>Los días de la semana (ya cortada por el mes) que contiene <paramref name="date"/>.</summary>
    public static IReadOnlyList<ReportPeriod> DaysOfWeek(DateTime date)
    {
        var week = WeekOf(date);
        var list = new List<ReportPeriod>();
        for (var d = week.Start; d <= week.End; d = d.AddDays(1))
            list.Add(new ReportPeriod(d, d, d.ToString("ddd MMM d", Inv)));
        return list;
    }

    private static string Label(DateTime start, DateTime end) =>
        start == end ? start.ToString("MMM d", Inv) : $"{start.ToString("MMM d", Inv)}-{end.Day}";
}
```

- [ ] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~ReportPeriodsTests"`
Expected: PASS (6: 3 hechos + 3 casos de la teoría).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Reports/ReportPeriods.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ReportPeriodsTests.cs
git commit -m "Reportes: periodos de la grafica (meses, semanas de lunes cortadas en el mes y dias de una semana)"
```

---

### Task 10: Servicio de Top Countries

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/TopCountriesViews.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/TopCountriesReportService.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/TopCountriesReportServiceTests.cs`

Reglas (especificación, Reporte 1 y 1b, y Decisiones):
- Solo lee `MemberCountrySnapshots`. Estados: multiselección; sin estados = los cinco.
- Tabla: la última foto con fecha ≤ la pedida. Por país: Ambassadors (`Ambassador`), Customers (`ExternalMember`), Total = ambos. Solo países con Total > 0. Orden: Total desc, país asc.
- Change: Total − Total de la última foto con fecha ≤ (foto mostrada − 30 días); un país que no estaba cuenta 0. Sin esa foto, Change = null para todos.
- Gráfica: por cada período, la última foto dentro de él: suma con los estados elegidos (0 si la foto no tiene filas de esos estados); sin foto en el período, `SnapshotDate` y `Total` null.

- [ ] **Step 1: Vistas**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Vista de la gráfica de Top Countries (mensual por defecto).</summary>
public enum CountryChartFrequency
{
    Monthly,
    Weekly,
    Daily
}

/// <summary>Una fila de la tabla de países.</summary>
public sealed class CountryRowView
{
    public string Country     { get; init; } = string.Empty;
    public int    Total       { get; init; }
    public int    Ambassadors { get; init; }
    public int    Customers   { get; init; }

    /// <summary>Total − Total de 30 días antes; null si no hay foto de entonces.</summary>
    public int?   Change      { get; init; }
}

/// <summary>La tabla de Top Countries: la foto usada, contra cuál se compara y las filas.</summary>
public sealed class TopCountriesTableView
{
    public DateTime?                     SnapshotDate { get; init; }
    public DateTime?                     ComparedWith { get; init; }
    public IReadOnlyList<CountryRowView> Rows         { get; init; } = Array.Empty<CountryRowView>();
}

/// <summary>Una barra de la gráfica: el total de la última foto del período (null si no hay foto en él).</summary>
public sealed class CountryChartPointView
{
    public string    Label        { get; init; } = string.Empty;
    public DateTime  Start        { get; init; }
    public DateTime  End          { get; init; }
    public DateTime? SnapshotDate { get; init; }
    public int?      Total        { get; init; }
}
```

- [ ] **Step 2: Pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class TopCountriesReportServiceTests
{
    private static readonly DateTime Oct14 = new(2026, 10, 14);
    private static readonly DateTime Nov13 = new(2026, 11, 13);   // 30 días después de Oct14

    private const MemberType Amb = MemberType.Ambassador;
    private const MemberType Cli = MemberType.ExternalMember;

    private static AppDbContext Vacia() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void Foto(AppDbContext db, DateTime dia, string pais, MemberType tipo, MemberAccountStatus estado, int n) =>
        db.MemberCountrySnapshots.Add(new MemberCountrySnapshot
        {
            SnapshotDate = dia, Country = pais, MemberType = tipo, Status = estado, MemberCount = n,
            CreationDate = dia, CreatedBy = "seed"
        });

    private static AppDbContext Base()
    {
        var db = Vacia();
        Foto(db, Oct14, "US", Amb, MemberAccountStatus.Active, 10);
        Foto(db, Oct14, "US", Cli, MemberAccountStatus.Active, 4);
        Foto(db, Oct14, "Peru", Amb, MemberAccountStatus.Active, 3);
        Foto(db, Nov13, "US", Amb, MemberAccountStatus.Active, 12);
        Foto(db, Nov13, "US", Cli, MemberAccountStatus.Active, 5);
        Foto(db, Nov13, "US", Amb, MemberAccountStatus.Suspended, 2);
        Foto(db, Nov13, "Peru", Amb, MemberAccountStatus.Active, 1);
        Foto(db, Nov13, "Chile", Amb, MemberAccountStatus.Active, 6);
        Foto(db, Nov13, "Chile", Amb, MemberAccountStatus.Terminated, 1);
        db.SaveChanges();
        return db;
    }

    private static TopCountriesReportService Servicio(AppDbContext db) => new(db);

    [Fact]
    public async Task Tabla_por_pais_con_cambio_contra_30_dias_antes()
    {
        using var db = Base();

        var t = await Servicio(db).GetTableAsync(Nov13, null);

        (t.SnapshotDate, t.ComparedWith).Should().Be(((DateTime?)Nov13, (DateTime?)Oct14));
        t.Rows.Select(r => (r.Country, r.Total, r.Ambassadors, r.Customers, r.Change)).Should().Equal(
            ("US", 19, 14, 5, (int?)5),
            ("Chile", 7, 7, 0, (int?)7),
            ("Peru", 1, 1, 0, (int?)-2));
    }

    [Fact]
    public async Task El_filtro_de_estados_cuenta_solo_esos()
    {
        using var db = Base();

        var t = await Servicio(db).GetTableAsync(Nov13, new[] { MemberAccountStatus.Active });

        t.Rows.Select(r => (r.Country, r.Total, r.Change)).Should().Equal(
            ("US", 17, (int?)3), ("Chile", 6, (int?)6), ("Peru", 1, (int?)-2));
    }

    [Fact]
    public async Task Una_fecha_sin_foto_usa_la_ultima_anterior()
    {
        using var db = Base();

        (await Servicio(db).GetTableAsync(new DateTime(2026, 11, 14), null)).SnapshotDate.Should().Be(Nov13);
    }

    [Fact]
    public async Task Sin_foto_de_30_dias_antes_el_cambio_queda_vacio()
    {
        using var db = Vacia();
        Foto(db, Nov13, "US", Amb, MemberAccountStatus.Active, 5);
        await db.SaveChangesAsync();

        var t = await Servicio(db).GetTableAsync(Nov13, null);

        t.ComparedWith.Should().BeNull();
        t.Rows.Single().Change.Should().BeNull();
    }

    [Fact]
    public async Task Compara_con_la_ultima_foto_hasta_30_dias_antes()
    {
        using var db = Vacia();
        Foto(db, new DateTime(2026, 10, 10), "US", Amb, MemberAccountStatus.Active, 1);
        Foto(db, Oct14, "US", Amb, MemberAccountStatus.Active, 2);
        Foto(db, new DateTime(2026, 10, 20), "US", Amb, MemberAccountStatus.Active, 100);
        Foto(db, Nov13, "US", Amb, MemberAccountStatus.Active, 10);
        await db.SaveChangesAsync();

        var t = await Servicio(db).GetTableAsync(Nov13, null);

        t.ComparedWith.Should().Be(Oct14);
        t.Rows.Single().Change.Should().Be(8);
    }

    [Fact]
    public async Task Sin_ninguna_foto_la_tabla_esta_vacia()
    {
        using var db = Vacia();

        var t = await Servicio(db).GetTableAsync(Nov13, null);

        t.SnapshotDate.Should().BeNull();
        t.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Grafica_mensual_toma_la_ultima_foto_de_cada_mes()
    {
        using var db = Base();

        var puntos = await Servicio(db).GetChartAsync(
            ReportPeriods.Months(new DateTime(2026, 9, 1), new DateTime(2026, 11, 30)), null);

        puntos.Select(p => (p.Label, p.SnapshotDate, p.Total)).Should().Equal(
            ("Sep 2026", (DateTime?)null, (int?)null),
            ("Oct 2026", (DateTime?)Oct14, (int?)17),
            ("Nov 2026", (DateTime?)Nov13, (int?)27));
    }

    [Fact]
    public async Task Grafica_con_estados_sin_filas_en_la_foto_da_cero()
    {
        using var db = Base();

        var puntos = await Servicio(db).GetChartAsync(
            ReportPeriods.Months(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)), new[] { MemberAccountStatus.Pending });

        (puntos.Single().SnapshotDate, puntos.Single().Total).Should().Be(((DateTime?)Nov13, (int?)0));
    }

    [Fact]
    public async Task Grafica_semanal_pone_la_foto_en_su_semana()
    {
        using var db = Base();

        var puntos = await Servicio(db).GetChartAsync(ReportPeriods.WeeksOfMonth(2026, 10), null);

        puntos.Select(p => p.Label).Should().Equal("Oct 1-4", "Oct 5-11", "Oct 12-18", "Oct 19-25", "Oct 26-31");
        puntos.Single(p => p.Total is not null).Should().BeEquivalentTo(new { Label = "Oct 12-18", Total = (int?)17 });
    }
}
```

- [ ] **Step 3: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~TopCountriesReportServiceTests"`
Expected: FAIL de compilación («TopCountriesReportService no existe»).

- [ ] **Step 4: Implementar**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface ITopCountriesReportService
{
    /// <summary>Tabla de países de la última foto con fecha ≤ <paramref name="date"/>, con Change contra 30 días antes.</summary>
    Task<TopCountriesTableView> GetTableAsync(DateTime date, IReadOnlyCollection<MemberAccountStatus>? statuses,
        CancellationToken ct = default);

    /// <summary>Total de la última foto de cada período.</summary>
    Task<IReadOnlyList<CountryChartPointView>> GetChartAsync(IReadOnlyList<ReportPeriod> periods,
        IReadOnlyCollection<MemberAccountStatus>? statuses, CancellationToken ct = default);
}

/// <summary>
/// Top Countries: lee solo la foto diaria (MemberCountrySnapshots), unas filas por día, así que no necesita caché. Sin
/// estados = los cinco (Total cuenta a todos; el estado es un filtro).
/// </summary>
public class TopCountriesReportService : ITopCountriesReportService
{
    public const int CompareDays = 30;

    private static readonly MemberAccountStatus[] AllStatuses = Enum.GetValues<MemberAccountStatus>();

    private readonly AppDbContext _db;

    public TopCountriesReportService(AppDbContext db) => _db = db;

    public static MemberAccountStatus[] NormalizeStatuses(IEnumerable<MemberAccountStatus>? statuses)
    {
        var set = (statuses ?? Array.Empty<MemberAccountStatus>()).Distinct().ToArray();
        return set.Length == 0 ? AllStatuses : set;
    }

    public async Task<TopCountriesTableView> GetTableAsync(DateTime date, IReadOnlyCollection<MemberAccountStatus>? statuses,
        CancellationToken ct = default)
    {
        var st       = NormalizeStatuses(statuses);
        var snapshot = await LatestOnOrBeforeAsync(date.Date, ct);
        if (snapshot is null) return new TopCountriesTableView();
        var compared = await LatestOnOrBeforeAsync(snapshot.Value.AddDays(-CompareDays), ct);

        var current = await CountsAsync(snapshot.Value, st, ct);
        var before  = compared is null
            ? null
            : (await CountsAsync(compared.Value, st, ct))
                .GroupBy(c => c.Country, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Count), StringComparer.OrdinalIgnoreCase);

        var rows = current
            .GroupBy(c => c.Country, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var ambassadors = g.Where(c => c.MemberType == MemberType.Ambassador).Sum(c => c.Count);
                var customers   = g.Where(c => c.MemberType == MemberType.ExternalMember).Sum(c => c.Count);
                var total       = ambassadors + customers;
                return new CountryRowView
                {
                    Country     = g.Key,
                    Total       = total,
                    Ambassadors = ambassadors,
                    Customers   = customers,
                    Change      = before is null ? null : (int?)(total - before.GetValueOrDefault(g.Key))
                };
            })
            .Where(r => r.Total > 0)
            .OrderByDescending(r => r.Total).ThenBy(r => r.Country, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TopCountriesTableView { SnapshotDate = snapshot, ComparedWith = compared, Rows = rows };
    }

    public async Task<IReadOnlyList<CountryChartPointView>> GetChartAsync(IReadOnlyList<ReportPeriod> periods,
        IReadOnlyCollection<MemberAccountStatus>? statuses, CancellationToken ct = default)
    {
        if (periods.Count == 0) return Array.Empty<CountryChartPointView>();
        var st    = NormalizeStatuses(statuses);
        var first = periods.Min(p => p.Start);
        var last  = periods.Max(p => p.End);

        var inRange = _db.MemberCountrySnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate >= first && s.SnapshotDate <= last);
        // Los dias con foto (con cualquier estado) y, aparte, los totales de los estados elegidos: una foto sin filas de
        // esos estados da 0, no «sin foto».
        var days   = await inRange.Select(s => s.SnapshotDate).Distinct().ToListAsync(ct);
        var totals = await inRange.Where(s => st.Contains(s.Status))
            .GroupBy(s => s.SnapshotDate)
            .Select(g => new { Day = g.Key, Total = g.Sum(s => s.MemberCount) })
            .ToDictionaryAsync(x => x.Day, x => x.Total, ct);

        return periods.Select(p =>
        {
            var day = days.Where(d => d >= p.Start && d <= p.End).Select(d => (DateTime?)d).Max();
            return new CountryChartPointView
            {
                Label        = p.Label,
                Start        = p.Start,
                End          = p.End,
                SnapshotDate = day,
                Total        = day is null ? null : (int?)totals.GetValueOrDefault(day.Value)
            };
        }).ToList();
    }

    private Task<DateTime?> LatestOnOrBeforeAsync(DateTime day, CancellationToken ct) =>
        _db.MemberCountrySnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate <= day)
            .MaxAsync(s => (DateTime?)s.SnapshotDate, ct);

    private sealed record CountryCount(string Country, MemberType MemberType, int Count);

    private async Task<List<CountryCount>> CountsAsync(DateTime day, MemberAccountStatus[] statuses, CancellationToken ct) =>
        (await _db.MemberCountrySnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate == day && statuses.Contains(s.Status))
            .GroupBy(s => new { s.Country, s.MemberType })
            .Select(g => new { g.Key.Country, g.Key.MemberType, Count = g.Sum(s => s.MemberCount) })
            .ToListAsync(ct))
        .Select(x => new CountryCount(x.Country, x.MemberType, x.Count))
        .ToList();
}
```

- [ ] **Step 5: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~TopCountriesReportServiceTests"`
Expected: PASS (9).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Reports/TopCountriesViews.cs MLMConquerorGlobalEdition.Repository/Services/Reports/TopCountriesReportService.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/TopCountriesReportServiceTests.cs
git commit -m "Reportes: servicio de Top Countries (tabla por pais con cambio contra 30 dias antes y grafica por periodo desde la foto diaria, con filtro de estados)"
```

---

### Task 11: Endpoints de Top Countries

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminTopCountriesReportController.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs` (después del registro de `IRankReportService`)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminTopCountriesReportControllerTests.cs`

Contrato:
- `GET api/v1/admin/reports/countries/table?date=yyyy-MM-dd&statuses=Active&statuses=...` → `ApiResponse<TopCountriesTableView>`. Sin `date` = hoy (hora del servidor; toma la última foto anterior).
- `GET api/v1/admin/reports/countries/chart?frequency=Monthly&from=&to=` (sin fechas: los últimos 12 meses hasta hoy) · `frequency=Weekly&date=` (semanas del mes de `date`) · `frequency=Daily&date=` (días de la semana de `date`) → `ApiResponse<IReadOnlyList<CountryChartPointView>>`. `from > to` → 400 `INVALID_RANGE`; más de 5 años → 400 `RANGE_TOO_LONG`.
- `GET api/v1/admin/reports/countries/export?date=&statuses=` → CSV `top-countries-<fecha de la foto>.csv` (Country, Total, Ambassadors, Customers, Change; Change vacío si no hay comparación). SuperAdmin y Admin.
- `POST api/v1/admin/reports/countries/snapshot` → escribe la foto de hoy. Solo SuperAdmin.

- [ ] **Step 1: Pruebas que fallan**

```csharp
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminTopCountriesReportControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 30, 0);
    private static readonly DateTime Hoy   = new(2026, 11, 14);

    private static (AdminTopCountriesReportController C, Mock<ITopCountriesReportService> S,
        Mock<IMemberCountrySnapshotWriter> W) Montar()
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        var servicio = new Mock<ITopCountriesReportService>();
        var escritor = new Mock<IMemberCountrySnapshotWriter>();
        return (new AdminTopCountriesReportController(servicio.Object, escritor.Object, new CsvReportWriter(), reloj.Object),
            servicio, escritor);
    }

    private static async Task<IReadOnlyList<ReportPeriod>> Periodos(CountryChartFrequency frecuencia,
        DateTime? desde = null, DateTime? hasta = null, DateTime? fecha = null)
    {
        var (c, s, _) = Montar();
        IReadOnlyList<ReportPeriod>? periodos = null;
        s.Setup(x => x.GetChartAsync(It.IsAny<IReadOnlyList<ReportPeriod>>(),
                It.IsAny<IReadOnlyCollection<MemberAccountStatus>?>(), It.IsAny<CancellationToken>()))
         .Callback<IReadOnlyList<ReportPeriod>, IReadOnlyCollection<MemberAccountStatus>?, CancellationToken>(
             (p, _, _) => periodos = p)
         .ReturnsAsync(Array.Empty<CountryChartPointView>());

        (await c.Chart(frecuencia, desde, hasta, fecha, null, default)).Should().BeOfType<OkObjectResult>();
        return periodos!;
    }

    [Fact]
    public async Task La_tabla_sin_fecha_usa_hoy_del_servidor()
    {
        var (c, s, _) = Montar();
        s.Setup(x => x.GetTableAsync(Hoy, It.IsAny<IReadOnlyCollection<MemberAccountStatus>?>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new TopCountriesTableView());

        (await c.Table(null, null, default)).Should().BeOfType<OkObjectResult>();
        s.VerifyAll();
    }

    [Fact]
    public async Task La_grafica_mensual_por_defecto_son_los_ultimos_12_meses()
    {
        var p = await Periodos(CountryChartFrequency.Monthly);

        p.Should().HaveCount(12);
        (p[0].Label, p[^1].Label).Should().Be(("Dec 2025", "Nov 2026"));
    }

    [Fact]
    public async Task La_grafica_semanal_son_las_semanas_del_mes_de_la_fecha()
        => (await Periodos(CountryChartFrequency.Weekly, fecha: new DateTime(2026, 11, 20))).Select(x => x.Label)
            .Should().Equal("Nov 1", "Nov 2-8", "Nov 9-15", "Nov 16-22", "Nov 23-29", "Nov 30");

    [Fact]
    public async Task La_grafica_diaria_son_los_dias_de_la_semana_cortada_por_el_mes()
        => (await Periodos(CountryChartFrequency.Daily, fecha: new DateTime(2026, 11, 30))).Select(x => x.Label)
            .Should().Equal("Mon Nov 30");

    [Fact]
    public async Task Un_rango_invertido_da_400()
    {
        var (c, _, _) = Montar();

        var r = await c.Chart(CountryChartFrequency.Monthly, new DateTime(2026, 11, 1), new DateTime(2026, 10, 1), null, null, default);

        r.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Exportar_da_un_csv_de_la_tabla_con_la_fecha_de_la_foto()
    {
        var (c, s, _) = Montar();
        s.Setup(x => x.GetTableAsync(Hoy, It.IsAny<IReadOnlyCollection<MemberAccountStatus>?>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new TopCountriesTableView
         {
             SnapshotDate = new DateTime(2026, 11, 13),
             Rows = new[]
             {
                 new CountryRowView { Country = "US", Total = 19, Ambassadors = 14, Customers = 5, Change = 5 },
                 new CountryRowView { Country = "Peru", Total = 1, Ambassadors = 1, Customers = 0, Change = null }
             }
         });

        var archivo = (await c.Export(null, null, default)).Should().BeOfType<FileContentResult>().Subject;

        archivo.FileDownloadName.Should().Be("top-countries-2026-11-13.csv");
        Encoding.UTF8.GetString(archivo.FileContents, 3, archivo.FileContents.Length - 3).Should().Be(
            "Country,Total,Ambassadors,Customers,Change\r\nUS,19,14,5,5\r\nPeru,1,1,0,\r\n");
    }

    [Fact]
    public async Task La_foto_manual_escribe_hoy()
    {
        var (c, _, w) = Montar();
        w.Setup(x => x.WriteAsync(Hoy, It.IsAny<CancellationToken>())).ReturnsAsync(8);

        (await c.TakeSnapshot(default)).Should().BeOfType<OkObjectResult>();
        w.VerifyAll();
    }

    [Fact]
    public void Leer_y_exportar_es_de_SuperAdmin_y_Admin_y_la_foto_manual_solo_de_SuperAdmin()
    {
        var tipo = typeof(AdminTopCountriesReportController);
        tipo.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
        tipo.GetMethod(nameof(AdminTopCountriesReportController.Export))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
        tipo.GetMethod(nameof(AdminTopCountriesReportController.TakeSnapshot))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminTopCountriesReportControllerTests"`
Expected: FAIL de compilación.

- [ ] **Step 3: Implementar el controlador**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Top Countries: tabla por país, gráfica por período y exportación a CSV (SuperAdmin y Admin) sobre la foto diaria; y
/// la foto manual de hoy (solo SuperAdmin), que el job nocturno del BizCenter reemplaza con el cierre del día.
/// </summary>
[ApiController]
[Route("api/v1/admin/reports/countries")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminTopCountriesReportController : ControllerBase
{
    private readonly ITopCountriesReportService   _report;
    private readonly IMemberCountrySnapshotWriter _writer;
    private readonly CsvReportWriter              _csv;
    private readonly IDateTimeProvider            _clock;

    public AdminTopCountriesReportController(ITopCountriesReportService report, IMemberCountrySnapshotWriter writer,
        CsvReportWriter csv, IDateTimeProvider clock)
    {
        _report = report;
        _writer = writer;
        _csv    = csv;
        _clock  = clock;
    }

    private DateTime Day(DateTime? date) => (date ?? _clock.Now).Date;

    /// <summary>GET reports/countries/table?date=&amp;statuses= — sin fecha: hoy (la última foto anterior).</summary>
    [HttpGet("table")]
    public async Task<IActionResult> Table([FromQuery] DateTime? date, [FromQuery] MemberAccountStatus[]? statuses,
        CancellationToken ct) =>
        Ok(ApiResponse<TopCountriesTableView>.Ok(await _report.GetTableAsync(Day(date), statuses, ct)));

    /// <summary>GET reports/countries/chart?frequency=Monthly&amp;from=&amp;to= | Weekly&amp;date= | Daily&amp;date=.</summary>
    [HttpGet("chart")]
    public async Task<IActionResult> Chart([FromQuery] CountryChartFrequency frequency, [FromQuery] DateTime? from,
        [FromQuery] DateTime? to, [FromQuery] DateTime? date, [FromQuery] MemberAccountStatus[]? statuses,
        CancellationToken ct)
    {
        IReadOnlyList<ReportPeriod> periods;
        switch (frequency)
        {
            case CountryChartFrequency.Weekly:
                var month = Day(date);
                periods = ReportPeriods.WeeksOfMonth(month.Year, month.Month);
                break;
            case CountryChartFrequency.Daily:
                periods = ReportPeriods.DaysOfWeek(Day(date));
                break;
            default:
                var end   = Day(to);
                var start = (from ?? new DateTime(end.Year, end.Month, 1).AddMonths(-11)).Date;
                if (start > end)
                    return BadRequest(ApiResponse<object>.Fail("INVALID_RANGE", "From must be on or before To."));
                if (start < end.AddYears(-5))
                    return BadRequest(ApiResponse<object>.Fail("RANGE_TOO_LONG", "The range can cover at most five years."));
                periods = ReportPeriods.Months(start, end);
                break;
        }
        return Ok(ApiResponse<IReadOnlyList<CountryChartPointView>>.Ok(await _report.GetChartAsync(periods, statuses, ct)));
    }

    /// <summary>GET reports/countries/export?date=&amp;statuses= — CSV de la tabla.</summary>
    [HttpGet("export")]
    // Punto de enganche para acotar la exportacion a un rol propio mas adelante (permiso reports.export).
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Export([FromQuery] DateTime? date, [FromQuery] MemberAccountStatus[]? statuses,
        CancellationToken ct)
    {
        var table = await _report.GetTableAsync(Day(date), statuses, ct);
        var bytes = _csv.Write(table.Rows,
            ("Country", r => r.Country), ("Total", r => r.Total), ("Ambassadors", r => r.Ambassadors),
            ("Customers", r => r.Customers), ("Change", r => r.Change));
        var day = table.SnapshotDate ?? Day(date);
        return File(bytes, CsvReportWriter.ContentType, $"top-countries-{day:yyyy-MM-dd}.csv");
    }

    /// <summary>POST reports/countries/snapshot — foto de hoy con el estado actual (provisional hasta el cierre).</summary>
    [HttpPost("snapshot")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> TakeSnapshot(CancellationToken ct)
    {
        var day  = _clock.Now.Date;
        var rows = await _writer.WriteAsync(day, ct);
        return Ok(ApiResponse<object>.Ok(new { SnapshotDate = day, Rows = rows }));
    }
}
```

- [ ] **Step 4: Registro en Program.cs**

Después del registro de `IRankReportService` (Tarea 4):

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Reports.ITopCountriesReportService,
                            MLMConquerorGlobalEdition.Repository.Services.Reports.TopCountriesReportService>();
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Reports.IMemberCountrySnapshotWriter,
                            MLMConquerorGlobalEdition.Repository.Services.Reports.MemberCountrySnapshotWriter>();
```

- [ ] **Step 5: Correr las pruebas y compilar**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~Reports"`
Expected: PASS.
Run: `dotnet build MLMConquerorGlobalEdition.AdminAPI`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminTopCountriesReportControllerTests.cs
git commit -m "Admin: endpoints de Top Countries (tabla, grafica mensual, semanal y diaria, exportacion a CSV y foto manual de hoy solo para SuperAdmin)"
```

---

### Task 12: Página de Top Countries con la gráfica (Syncfusion)

**Files:**
- Modify: `MLMConquerorGlobalEdition.AdminWeb/MLMConquerorGlobalEdition.AdminWeb.csproj`
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminTopCountriesReport.razor`

Las gráficas del admin se hacen con Syncfusion (decisión de Nick). AdminWeb referencia Syncfusion 33.1.44 (Core, Themes, Maps, RichTextEditor) y, por SharedComponents, Grid y otros; no referencia `Syncfusion.Blazor.Charts`: se añade a la misma versión. No hace falta tocar `App.razor`: ya carga `_content/Syncfusion.Blazor.Core/scripts/syncfusion-blazor.min.js` (con los scripts de todos los componentes) y `Program.cs` ya llama a `AddSyncfusionBlazor()`.

- [ ] **Step 1: Paquete de gráficas**

En `MLMConquerorGlobalEdition.AdminWeb.csproj`, después de `<PackageReference Include="Syncfusion.Blazor.RichTextEditor" Version="33.1.44" />`:

```xml
    <PackageReference Include="Syncfusion.Blazor.Charts" Version="33.1.44" />
```

Run: `dotnet restore MLMConquerorGlobalEdition.AdminWeb`
Expected: Restore succeeded, sin avisos de versiones distintas de Syncfusion (todas 33.1.44).

- [ ] **Step 2: Escribir la página**

```razor
@page "/admin/reports/top-countries"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin,Admin")]
@using MLMConquerorGlobalEdition.SharedComponents.Constants
@using MLMConquerorGlobalEdition.SharedComponents.Services
@using Syncfusion.Blazor.Charts
@inject HttpClient Http
@inject IJSRuntime Js
@inject IViewContextService ViewContext

<PageTitle>Top Countries</PageTitle>

<h4 class="mb-1">Top Countries</h4>
<p class="text-muted mb-4">Members by country from the daily snapshot, taken every night since this report was turned on.</p>

<div class="mb-3">
    <MemberStatusFilter Options="AllStatuses" Selected="_statuses" IdPrefix="tc-status" Changed="ReloadAllAsync" />
</div>

@if (_error is not null)
{
    <div class="alert alert-danger">@_error</div>
}

<div class="card mb-4">
    <div class="card-body">
        <div class="d-flex flex-wrap gap-2 align-items-end mb-3">
            <div>
                <label class="form-label small mb-1">View</label>
                <select class="form-select" @bind="_frequency" @bind:after="LoadChartAsync">
                    <option value="Monthly">Monthly</option>
                    <option value="Weekly">Weeks of a month</option>
                    <option value="Daily">Days of a week</option>
                </select>
            </div>
            @if (_frequency == "Monthly")
            {
                <div>
                    <label class="form-label small mb-1">From</label>
                    <input type="month" class="form-control" @bind="_fromMonth" @bind:format="yyyy-MM" @bind:after="LoadChartAsync" />
                </div>
                <div>
                    <label class="form-label small mb-1">To</label>
                    <input type="month" class="form-control" @bind="_toMonth" @bind:format="yyyy-MM" @bind:after="LoadChartAsync" />
                </div>
            }
            else if (_frequency == "Weekly")
            {
                <div>
                    <label class="form-label small mb-1">Month</label>
                    <input type="month" class="form-control" @bind="_weekMonth" @bind:format="yyyy-MM" @bind:after="LoadChartAsync" />
                </div>
            }
            else
            {
                <div>
                    <label class="form-label small mb-1">Any day of the week</label>
                    <input type="date" class="form-control" @bind="_weekDay" @bind:after="LoadChartAsync" />
                </div>
            }
        </div>

        @* Una columna por periodo; Total null (periodo sin foto) se dibuja como hueco. *@
        <SfChart Height="300px" Width="100%">
            <ChartPrimaryXAxis ValueType="Syncfusion.Blazor.Charts.ValueType.Category" />
            <ChartPrimaryYAxis LabelFormat="N0" Minimum="0" />
            <ChartTooltipSettings Enable="true" />
            <ChartLegendSettings Visible="false" />
            <ChartSeriesCollection>
                <ChartSeries DataSource="@_points" XName="@nameof(ChartPoint.Label)" YName="@nameof(ChartPoint.Total)"
                             Name="Members" Type="ChartSeriesType.Column" Fill="#3478F6">
                    <ChartEmptyPointSettings Mode="EmptyPointMode.Gap" />
                </ChartSeries>
            </ChartSeriesCollection>
        </SfChart>
        @if (_points.Count > 0 && _points.All(p => p.Total is null))
        {
            <p class="small text-muted mb-0">No snapshot in this range.</p>
        }
    </div>
</div>

<div class="card">
    <div class="card-body">
        <div class="d-flex flex-wrap gap-2 align-items-end mb-3">
            <div>
                <label class="form-label small mb-1">Date</label>
                <input type="date" class="form-control" max="@DateTime.Today.ToString("yyyy-MM-dd")"
                       @bind="_tableDate" @bind:after="LoadTableAsync" />
            </div>
            @if (ViewContext.HasPermission(Permissions.Reports.Export))
            {
                <button class="btn btn-outline-success" @onclick="ExportAsync" disabled="@_exporting">
                    <i class="ri-file-download-line me-1"></i>Export CSV
                </button>
            }
            <AuthorizeView Roles="SuperAdmin">
                <Authorized>
                    <button class="btn btn-outline-secondary" @onclick="TakeSnapshotAsync" disabled="@_snapshotting"
                            title="Counts today's members now; tonight's run replaces it with the end-of-day count">
                        <i class="ri-camera-line me-1"></i>Take snapshot now
                    </button>
                </Authorized>
            </AuthorizeView>
        </div>

        @if (_table?.SnapshotDate is null)
        {
            <p class="text-muted mb-0">No snapshot yet. The first one is taken tonight.</p>
        }
        else
        {
            <p class="small text-muted">
                Snapshot of @_table.SnapshotDate.Value.ToString("yyyy-MM-dd") ·
                Change vs @(_table.ComparedWith?.ToString("yyyy-MM-dd") ?? "— (no snapshot from 30 days before)")
            </p>
            <div class="table-responsive">
                <table class="table table-sm align-middle">
                    <thead>
                        <tr>
                            <th>Country</th>
                            <th class="text-end">Total</th>
                            <th class="text-end">Ambassadors</th>
                            <th class="text-end">Customers</th>
                            <th class="text-end">Change</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var row in _table.Rows)
                        {
                            <tr>
                                <td>@row.Country</td>
                                <td class="text-end">@row.Total.ToString("N0")</td>
                                <td class="text-end">@row.Ambassadors.ToString("N0")</td>
                                <td class="text-end">@row.Customers.ToString("N0")</td>
                                <td class="text-end">
                                    @if (row.Change is null)
                                    {
                                        <span class="text-muted">—</span>
                                    }
                                    else if (row.Change > 0)
                                    {
                                        <span class="text-success"><i class="ri-arrow-up-line"></i>@row.Change.Value.ToString("N0")</span>
                                    }
                                    else if (row.Change < 0)
                                    {
                                        <span class="text-danger"><i class="ri-arrow-down-line"></i>@Math.Abs(row.Change.Value).ToString("N0")</span>
                                    }
                                    else
                                    {
                                        <span class="text-muted">0</span>
                                    }
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </div>
</div>

@code {
    // Aqui si se ofrece Terminated: Total cuenta a todos los miembros y el estado es un filtro.
    private static readonly IReadOnlyList<string> AllStatuses =
        new[] { "Active", "Inactive", "Suspended", "Terminated", "Pending" };

    private readonly HashSet<string> _statuses = new(AllStatuses);
    private string   _frequency = "Monthly";
    private DateTime _toMonth   = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _fromMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
    private DateTime _weekMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _weekDay   = DateTime.Today;
    private DateTime _tableDate = DateTime.Today;
    private TableResp? _table;
    private List<ChartPoint> _points = new();
    private bool    _exporting;
    private bool    _snapshotting;
    private string? _error;

    private string StatusQuery => string.Join("&", _statuses.Select(s => $"statuses={s}"));

    protected override async Task OnInitializedAsync() => await ReloadAllAsync();

    private async Task ReloadAllAsync()
    {
        await LoadTableAsync();
        await LoadChartAsync();
    }

    private async Task LoadTableAsync()
    {
        _error = null;
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiResp<TableResp>>(
                $"api/v1/admin/reports/countries/table?date={_tableDate:yyyy-MM-dd}&{StatusQuery}");
            _table = resp?.Data;
        }
        catch (Exception ex) { _error = $"Could not load the table: {ex.Message}"; }
    }

    private async Task LoadChartAsync()
    {
        _error = null;
        var q = _frequency switch
        {
            "Weekly" => $"frequency=Weekly&date={_weekMonth:yyyy-MM-dd}",
            "Daily"  => $"frequency=Daily&date={_weekDay:yyyy-MM-dd}",
            _        => $"frequency=Monthly&from={_fromMonth:yyyy-MM-dd}&to={_toMonth.AddMonths(1).AddDays(-1):yyyy-MM-dd}"
        };
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiResp<List<ChartPoint>>>(
                $"api/v1/admin/reports/countries/chart?{q}&{StatusQuery}");
            _points = resp?.Data ?? new();   // lista nueva: SfChart redibuja al cambiar la referencia de DataSource
        }
        catch (Exception ex) { _error = $"Could not load the chart: {ex.Message}"; }
    }

    private async Task ExportAsync()
    {
        _exporting = true;
        _error = null;
        try
        {
            var bytes = await Http.GetByteArrayAsync(
                $"api/v1/admin/reports/countries/export?date={_tableDate:yyyy-MM-dd}&{StatusQuery}");
            var day = _table?.SnapshotDate ?? _tableDate;
            await Js.InvokeVoidAsync("mlmDownloadFile", $"top-countries-{day:yyyy-MM-dd}.csv", "text/csv",
                Convert.ToBase64String(bytes));
        }
        catch (Exception ex) { _error = $"Export failed: {ex.Message}"; }
        finally { _exporting = false; }
    }

    private async Task TakeSnapshotAsync()
    {
        _snapshotting = true;
        _error = null;
        try
        {
            var resp = await Http.PostAsync("api/v1/admin/reports/countries/snapshot", null);
            if (resp.IsSuccessStatusCode) await ReloadAllAsync();
            else _error = $"Snapshot failed ({(int)resp.StatusCode}).";
        }
        catch (Exception ex) { _error = $"Snapshot failed: {ex.Message}"; }
        finally { _snapshotting = false; }
    }

    private sealed record ApiResp<T>(bool Success, T? Data, IEnumerable<string>? Errors);
    private sealed record TableResp(DateTime? SnapshotDate, DateTime? ComparedWith, List<CountryRow> Rows);
    private sealed record CountryRow(string Country, int Total, int Ambassadors, int Customers, int? Change);

    // Clase con propiedades publicas: SfChart lee XName/YName por reflexion.
    private sealed class ChartPoint
    {
        public string    Label        { get; set; } = string.Empty;
        public DateTime  Start        { get; set; }
        public DateTime  End          { get; set; }
        public DateTime? SnapshotDate { get; set; }
        public int?      Total        { get; set; }
    }
}
```

`ValueType` va calificado (`Syncfusion.Blazor.Charts.ValueType.Category`) porque choca con `System.ValueType`. Si en 33.1.44 `ChartEmptyPointSettings`/`EmptyPointMode` no compila, quitar esa línea: el modo por defecto de Syncfusion para puntos nulos ya es `Gap`.

- [ ] **Step 3: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/MLMConquerorGlobalEdition.AdminWeb.csproj MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminTopCountriesReport.razor
git commit -m "Admin: pagina de Top Countries con grafica de Syncfusion mensual, semanal y diaria, tabla por pais con cambio contra 30 dias, exportacion y foto manual"
```

---

### Task 13: Entradas del menú Reports

**Files:**
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor`

Orden de la especificación: Top Countries · Earnings 180 Days · Earnings Lifetime · Lifetime Rank · Current Rank.

- [ ] **Step 1: Top Countries, primera del grupo**

Dentro de `<ul class="menu-sub">` del grupo Reports, justo antes de `<li class="menu-item @IsActive("/admin/reports/earnings-window")">`:

```razor
                        <li class="menu-item @IsActive("/admin/reports/top-countries")">
                            <a href="/admin/reports/top-countries" class="menu-link @IsActive("/admin/reports/top-countries")">
                                <span class="title">Top Countries</span>
                            </a>
                        </li>
```

- [ ] **Step 2: Lifetime Rank y Current Rank, al final del grupo**

Justo después del `</li>` de la entrada `Earnings Lifetime` (antes del `</ul>` del grupo Reports):

```razor
                        <li class="menu-item @IsActive("/admin/reports/lifetime-rank")">
                            <a href="/admin/reports/lifetime-rank" class="menu-link @IsActive("/admin/reports/lifetime-rank")">
                                <span class="title">Lifetime Rank</span>
                            </a>
                        </li>
                        <li class="menu-item @IsActive("/admin/reports/current-rank")">
                            <a href="/admin/reports/current-rank" class="menu-link @IsActive("/admin/reports/current-rank")">
                                <span class="title">Current Rank</span>
                            </a>
                        </li>
```

- [ ] **Step 3: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor
git commit -m "Admin: Top Countries, Lifetime Rank y Current Rank en el grupo Reports del menu"
```

---

### Task 14: Verificación final del plan B

- [ ] **Step 1: Suites completas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests`
Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests`
Expected: 0 fallos en las dos (las `[SqlServerFact]` pasan o se saltan; las reglas de escritores —`EscritoresDelBinarioTests`, `EscritoresDeSuscripcionesTests`— siguen pasando: este plan no escribe en `DualTeamTree` ni en `MembershipSubscriptions`).

- [ ] **Step 2: Probar en el navegador (AdminWeb + AdminAPI + BizCenter levantados contra la base local)**

1. Como SuperAdmin, Reports → Top Countries: «No snapshot yet». Pulsar «Take snapshot now»: aparece la tabla de hoy con Change «—» y la gráfica mensual con una barra en el mes actual.
2. Desmarcar estados: la tabla y la gráfica se recalculan; al desmarcar el último, vuelven todos.
3. Cambiar a «Weeks of a month» y a «Days of a week»: las etiquetas respetan el corte por mes (p. ej. noviembre de 2026: «Nov 1», «Nov 2-8», …, «Nov 30»).
4. En el panel de Hangfire del BizCenter (`/hangfire`), `member-country-snapshot` aparece en Recurring jobs con «30 5 * * *» UTC; «Trigger now» escribe la foto de ayer.
5. Reports → Lifetime Rank y Current Rank: el conteo aparece; filtrar por rango, fechas, estado y buscar por país; los Active salen en verde y el resto en gris; ningún Terminated ni cuenta corporativa. Marcar una cuenta como corporativa en su perfil: desaparece del reporte al recargar.
6. Exportar en las tres páginas: el CSV trae las mismas filas filtradas. Con un usuario IT no se ve el grupo Reports ni el botón Export.

- [ ] **Step 3: Medidas**

Completar la sección «Medidas» con los números de las Tareas 3 y 7. Si Lifetime o Current Rank pasan de 2 s en frío con la máquina libre, avisar a Nick con los números antes de dar el plan por cerrado.

- [ ] **Step 4: Revisión de código de la rama** con `superpowers:requesting-code-review` y corregir lo que encuentre.

## Preguntas abiertas (con el valor por defecto que aplica este plan)

1. **Rendimiento de los reportes de rango.** Por defecto: consulta agregada en vivo + caché de 10 minutos por modo. En la preparación del plan (14-nov-2026, máquina cargada, SQL escrito a mano equivalente al de EF) la de por vida con la unión a perfiles tardó 4,4 / 7,3 / 16,1 s en frío en la base de escala. Si la Tarea 3 lo confirma, Nick decide entre aceptarlo (solo el primer acceso cada 10 minutos lo paga) o la alternativa de la especificación: guardar rango vigente y de por vida (con su fecha) en `MemberStatistics`, mantenidos por el motor de rangos (cambia cada escritor de `MemberRankHistories`).
2. **Desempate de Lifetime Rank dentro de un mismo rango.** Por defecto: «Since» de la más reciente a la más antigua, como Current Rank. ¿Prefiere Nick la más antigua primero (quien lleva más tiempo en el rango)?
3. **Rellenar un día perdido de la foto.** Por defecto: no se puede (el pasado no se reconstruye); la foto manual solo escribe hoy y el «Trigger now» de Hangfire, ayer. La gráfica tolera huecos (toma la última foto del período). ¿Quiere Nick poder escribir un día pasado con los conteos actuales, sabiendo que no serían los de ese día?
4. **Cuentas corporativas en Top Countries.** Por defecto: cuentan (la especificación dice que Total cuenta a todos). ¿Se excluyen como en los rankings?
5. **Base de Change con huecos.** Por defecto: la última foto con fecha ≤ 30 días antes (si falta ese día exacto, puede ser más antigua); la tabla muestra la fecha usada.
6. **Hora de la foto.** Por defecto: 05:30 UTC (00:30/01:30 local): lo que pase entre la medianoche y esa hora cuenta en el día que cerró. ¿Basta, o se prefiere correrla a las 23:55 local escribiendo «hoy»?
7. **Países sin normalizar.** `MemberProfile.Country` tiene valores mixtos según el origen (códigos como «US» en la base de escala, nombres en otras pruebas). El reporte los muestra tal cual; normalizarlos queda fuera.

## Medidas

_(Tarea 3, Step 3: Lifetime y Current Rank; Tarea 7, Step 5: GROUP BY de la foto. Base `MLMConquerorGlobalEditionDb_Escala` en `Server=.`, solo lectura, `sqlcmd -I`.)_

Preparación del plan (14-nov-2026), para orientar:
- Base de escala: `MemberProfiles` 802.003 (todos Ambassador y Active; `Country` con 2 valores, casi todo «US»); `MemberRankHistories` 173.124 filas de 119.835 miembros; ninguna fila de rango 0 (SortOrder 1: 119.835, 2: 34.175, 3: 15.076, 4: 3.195, 5: 805, 6+: 39). Índices de `MemberRankHistories`: PK, `IX_..._RankDefinitionId`, `IX_..._MemberId_AchievedAt`. No tiene `IsCorporateAccount`.
- SQL escrito a mano equivalente (no el de EF), resultado contado en el servidor, máquina con otras cargas (12 núcleos):
  - Lifetime con unión a perfiles: 16,1 / 7,3 / 4,4 s (CPU 11,0 / 6,3 / 4,1 s); 119.835 filas.
  - Solo la agregación de por vida (sin perfiles): 3,3 / 3,8 s.
  - Antijoin del vigente (sin perfiles): 1,5 / 0,7 s.
  - GROUP BY de la foto (país, tipo, estado): 1,0 / 0,3 / 0,2 s.

Tarea 3 (SQL de EF):

| Consulta | Filas | Lecturas lógicas | Servidor (3 corridas) | Con envío de filas |
|---|---|---|---|---|
| Lifetime | 119.835 | MemberRankHistories 491.766 (119.836 búsquedas), MemberProfiles 838.845, RankDefinitions 9 | 4,8 / 4,4 / 4,5 s (CPU 4,4 / 3,9 / 4,3 s) | 5,6 / 5,7 / 8,3 s (CPU 4,6 / 4,8 / 6,9 s) |
| Current | 119.835 | MemberRankHistories 10.232, MemberProfiles 88.745 (84.808 con envío), RankDefinitions 4 | 0,58 / 0,53 / 0,50 s (CPU 2,7 / 2,9 / 2,8 s, en paralelo) | 3,4 / 3,1 / 3,4 s (CPU 6,6 / 7,9 / 10,8 s) |

Notas de la Tarea 3 (14-nov-2026, máquina con otras cargas: varios procesos dotnet de servicios levantados):
- SQL tal como lo genera EF (salida de `Las_consultas_se_traducen_a_SQL`), quitando solo `IsCorporateAccount` (de la condición y de la lista de columnas de la subconsulta de perfiles), que la base de escala no tiene. Las dos consultas se traducen y la prueba con datos elige bien en SQL Server (2/2 PASS).
- «Servidor» = `SELECT COUNT(*) FROM (...) q`; «Con envío de filas» = el SELECT sin envolver, con `sqlcmd` como cliente (que formatea 119.835 filas en consola: es una cota alta del coste de envío, EF las materializa más rápido).
- Lifetime no cumple los 2 s: el plan hace bucles anidados (una búsqueda en `IX_MemberRankHistories_MemberId_AchievedAt` por miembro para el MIN del rango máximo) y una búsqueda por fila en `MemberProfiles` (838.845 lecturas). Current sí cumple en el servidor (~0,5 s, plan paralelo con exploraciones).
- Base de escala: sin filas de rango 0 y todos los perfiles Ambassador/Active, así que los filtros no descartan nada (119.835 filas en los dos modos).
- Queda para la decisión de Nick («Preguntas abiertas», 1): aceptar ~4,5 s en frío de Lifetime cada 10 minutos (caché por modo) o guardar el rango de por vida con su fecha en `MemberStatistics`.

Tarea 7 (GROUP BY de la foto): | | | |
