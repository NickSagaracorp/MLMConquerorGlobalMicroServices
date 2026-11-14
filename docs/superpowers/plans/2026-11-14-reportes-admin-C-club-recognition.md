# Reportes del Admin — Plan C: Club Recognition

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar el módulo Club Recognition del admin: catálogo parametrizable de niveles y de opciones de reconocimiento, detección automática de quién alcanza cada nivel (carga histórica con la fecha real en que se cruzó el umbral y proceso nocturno), captura del reconocimiento (CEO Call, evento, talla del premio y notas, todo auditado) en una pestaña «Recognition» del perfil del miembro, y las cinco vistas del menú Club Recognition con búsqueda, filtro por nivel, conteo y exportación a CSV.

**Architecture:** Tres tablas nuevas (`RecognitionLevels` y `RecognitionOptions` con semilla por `HasData`; `MemberRecognitions` con clave única Ambassador+nivel). En Repository: `EarningsQueries` (la regla de ganancias de los reportes Earnings, extraída de `EarningsRankingService` sin cambiar su comportamiento), `ClubRecognitionDetector` (nocturno: un agregado por ventana distinta, en LINQ; histórico: un `INSERT ... SELECT` con funciones de ventana de SQL Server, una sentencia por ventana distinta) y `ClubRecognitionReportService` (lectura de vistas y de los registros de un miembro). El job nocturno vive en el BizCenter (Hangfire, cola `bizcenter`, 05:45 UTC) y hace la carga histórica la primera vez. AdminAPI: controladores de parámetros, de captura (con `GeneralAuditTracking`), de vistas/CSV y de carga histórica manual (solo SuperAdmin). AdminWeb: dos páginas de parámetros, la página de las vistas (cinco rutas), un panel para la pestaña del perfil y dos grupos nuevos del menú.

**Tech Stack:** .NET 10, EF Core 10 (SQL Server), ASP.NET Core Web API, Hangfire, Blazor Server + Syncfusion Grid, xUnit + FluentAssertions + Moq + EF InMemory.

**Especificación:** `docs/superpowers/specs/2026-11-14-reportes-admin-design.md` (Principios, Sección 6 — Club Recognition, Estructura del menú, Decisiones tomadas).

**Planes anteriores (hechos):** A (`2026-11-14-reportes-admin-A-base-y-earnings.md`) y B (`2026-11-14-reportes-admin-B-rangos-y-paises.md`). Se reutilizan: `CsvReportWriter`, `Permissions.Reports.Export`, `IsCorporateAccount`, `ServerGridAdaptor.OnCount`, el patrón de auditoría de `AdminMemberCorporateAccountController`, el patrón de job nocturno de `MemberCountrySnapshotJob` y la infraestructura de pruebas contra SQL Server (`SqlServerReportes`, `ReportesDb`).

**Fuera de este plan:** importar desde legacy los reconocimientos ya capturados por el cliente (CEO Call y evento): lo hace el proyecto de migración (sección «Historial de rangos y migración» de la especificación).

---

## Reglas del repo que este plan respeta

- Hora del servidor (`IDateTimeProvider.Now`), nunca `UtcNow`. El cron de Hangfire se escribe en UTC como los demás jobs; el día que se evalúa lo decide `Now`, no la hora del cron.
- Toda migración nueva regenera `DeploymentScripts/migrate-rds.sql` (lo comprueba `BizCenter.Tests/Migraciones/ScriptDeDespliegueTests.cs`) y se aplica a la base local con `Command Timeout=900`.
- `MLMConquerorGlobalEditionDb_Escala` es de **solo lectura**: se mide con `SELECT` y `sqlcmd -I`, nunca se escribe ni se migra.
- Las reglas de escritores de `BizCenter.Tests` (`EscritoresDelBinarioTests`, `EscritoresDeSuscripcionesTests`) siguen en verde: este plan no escribe en `DualTeamTree` ni en `MembershipSubscriptions`.
- Commits en español sin acentos, sin coautoría ni menciones de herramientas: solo la descripción de lo que se hizo.
- Componentes y servicios se escriben una vez; lo específico se inyecta (una página para las cinco vistas, una fila de vista para la pestaña y los reportes, una regla de ganancias para Earnings y Club Recognition).

## Decisiones de diseño (y valores por defecto elegidos)

1. **Modelo.** `RecognitionLevel` (Name único, Threshold `decimal(18,4)`, WindowDays `int?` —null = de por vida—, Reward, RewardNeedsSize, SortOrder, IsActive) y `RecognitionOption` (Name único, RequiresReason, SortOrder, IsActive), con clave `int` y semilla por `HasData`, como `DocumentTypes`, `RankDefinitions` y `MembershipLevels`. `MemberRecognition` (clave `long`): MemberId, RecognitionLevelId, ReachedDate (`date`), QualifyingTotal, Source (Historical/Nightly), CeoCallMonth (`date`, día 1 del mes) o CeoCallOptionId (+ CeoCallReason), EventId (FK a `CorporateEvents`) o EventOptionId (+ EventReason), RewardSize, Notes, LastUpdateDate/By. Índice único `UX_MemberRecognitions_Member_Level` (MemberId, RecognitionLevelId). Las FK son `Restrict`: un nivel, una opción o un evento en uso no se borra (se desactiva).
2. **Opciones del catálogo.** Semilla: «Recognized Higher Level», «Recognized Long Time Ago» y «Do not recognize» con `RequiresReason = true`. La vista muestra «Do not recognize – <motivo>» con el motivo que escribe el admin. Las opciones sirven para las dos columnas (CEO Call y evento).
3. **Regla de ganancias** = la de los reportes Earnings: `CommissionEarning` Paid + Pending, sin Cancelled, sin borrados, `EarnedDate` en [desde, hasta). La ventana de N días que cierra el día D es (D − N, D], igual que Earnings 180 Days. Se extrae a `EarningsQueries.TotalsByMember` y `EarningsRankingService` pasa a usarla (mismo SQL, sus pruebas no cambian).
4. **Quién califica:** `MemberType = Ambassador`, `Status <> Terminated`, `IsCorporateAccount = 0`, perfil no borrado; la condición se evalúa con el estado de **hoy** (también en la carga histórica).
5. **Nocturno.** Cada noche evalúa el día que cerró (`Now.Date − 1`). Agrupa los niveles activos por `WindowDays` y hace **un agregado por ventana distinta** (hoy: 180 días y de por vida = 2 consultas) con `HAVING SUM >= el menor umbral` del grupo, unido a los Ambassadors elegibles. A quien llega a un nivel y no tiene registro de ese nivel le crea uno con `ReachedDate = día que cerró`, el total con que calificó y `Source = Nightly`. Nunca borra ni cambia un registro.
6. **Carga histórica (set-based).** Una sentencia `WITH ... INSERT ... SELECT` por ventana distinta: (a) suma diaria por miembro (`GROUP BY BeneficiaryMemberId, CAST(EarnedDate AS date)`); (b) cada día genera un evento `+monto` ese día y, si hay ventana, `−monto` el día `día + WindowDays` (cuando sale de la ventana), con `CROSS APPLY (VALUES ...)` para leer `CommissionEarnings` una sola vez; (c) `SUM(delta) OVER (PARTITION BY miembro ORDER BY día ROWS UNBOUNDED PRECEDING)` da el total de la ventana (o el acumulado de por vida) en cada día en que cambia; como el total solo cambia esos días, el primer día en que llega al umbral es uno de ellos; (d) `ROW_NUMBER() OVER (PARTITION BY miembro, nivel ORDER BY día)` toma el primero; (e) se inserta con `NOT EXISTS` (idempotente; no toca lo que ya existe). `ReachedDate` = ese día; `QualifyingTotal` = el total de la ventana o el acumulado al cierre de ese día. SQL Server no admite `RANGE` con intervalo de fechas, por eso la ventana móvil se resuelve con eventos de entrada y salida.
7. **Primera ejecución.** El job nocturno consulta el marcador `GlobalParameters["ClubRecognition.HistoryLoadedThrough"]`: si no existe, hace la carga histórica hasta el día que cerró (y deja el marcador); si existe, la detección nocturna. Así el primer nocturno nunca pone la fecha de la corrida a quien cruzó el umbral hace meses. La carga histórica manual (`POST api/v1/admin/club-recognition/detection/history`, solo SuperAdmin, botón en All Records) sirve para relanzarla (p. ej. tras crear un nivel): solo añade lo que falta, con la fecha real de cruce.
8. **Job.** `ClubRecognitionJob` en `BizCenter/Jobs` (el AdminAPI no tiene Hangfire), cola `bizcenter`, cron `45 5 * * *` UTC (00:45/01:45 hora del Este; 15 minutos después de la foto de países para no solapar dos recorridos de tablas grandes). Tiempo de comando de 15 minutos dentro del detector.
9. **Vistas.** Una consulta sobre `MemberRecognitions` + `MemberProfiles` + navegaciones (nivel, opciones, evento) filtrada en SQL por vista y nivel; búsqueda (nombre, email, país, sin mayúsculas), orden y paginación en memoria (la tabla tiene una fila por Ambassador y nivel alcanzado: miles, no millones). Sin caché: lo que el admin captura se ve al instante. Las vistas excluyen las cuentas corporativas; la pestaña del perfil muestra los registros del miembro aunque lo sean. Todas las vistas llevan las diez columnas de la especificación, así que las «Not Recognized…» muestran también la otra columna. Orden: Reached de la más reciente a la más antigua, luego nivel (SortOrder) de mayor a menor, luego nombre.
10. **Captura.** `PUT api/v1/admin/members/{memberId}/recognitions/{id}`. CEO Call = mes (se guarda el día 1) **u** opción; evento = `CorporateEvent` **u** opción; las dos cosas a la vez dan 400. Una opción con `RequiresReason` exige motivo; sin él, el motivo se descarta. Una opción inactiva no se puede elegir, salvo la que el registro ya tiene. `RewardSize` solo se guarda si el nivel lleva talla. Cada cambio real deja una fila en `GeneralAuditTracking` (`EntityName = "MemberRecognition"`, `Action = "RecognitionCaptured"`, valores antes/después en JSON); sin cambios no se audita.
11. **Rutas.** Vistas: `/admin/club-recognition/{all|not-recognized-online|not-recognized-events|recognized-online|recognized-events}`. Parámetros: `/admin/parameters/recognition-levels` y `/admin/parameters/recognition-options`. Menú: grupo «Club Recognition» (cinco vistas) y grupo «Parameters» (dos entradas), ambos para SuperAdmin y Admin. Exportar: botón visible solo con `Permissions.Reports.Export`; el endpoint, SuperAdmin y Admin (como A y B).
12. **Orden de las tareas.** El servicio de lectura (Tarea 8) va antes de la captura (Tarea 9) porque el endpoint de captura devuelve la fila ya formateada con él; así ninguna tarea reescribe código de otra.

## Mapa de archivos

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `MLMConquerorGlobalEdition.Domain/Entities/Recognition/RecognitionLevel.cs` | Crear | nivel parametrizable |
| `MLMConquerorGlobalEdition.Domain/Entities/Recognition/RecognitionOption.cs` | Crear | opción del catálogo |
| `MLMConquerorGlobalEdition.Domain/Entities/Recognition/RecognitionSource.cs` | Crear | Historical / Nightly |
| `MLMConquerorGlobalEdition.Domain/Entities/Recognition/MemberRecognition.cs` | Crear | registro por Ambassador y nivel |
| `MLMConquerorGlobalEdition.Repository/Configurations/RecognitionLevelConfiguration.cs` | Crear | longitudes, índice único, semilla de 6 niveles |
| `MLMConquerorGlobalEdition.Repository/Configurations/RecognitionOptionConfiguration.cs` | Crear | longitudes, índice único, semilla de 3 opciones |
| `MLMConquerorGlobalEdition.Repository/Configurations/MemberRecognitionConfiguration.cs` | Crear | `date`, precisión, clave única, FK |
| `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs` | Modificar | tres `DbSet` |
| `MLMConquerorGlobalEdition.Repository/Migrations/<ts>_ReconocimientoDeClubes.cs` | Crear (EF) | tablas, índices y semilla |
| `DeploymentScripts/migrate-rds.sql` | Regenerar | script idempotente |
| `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionParametersController.cs` | Crear | niveles, opciones y `lookups` |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecognitionLevels.razor` | Crear | pantalla de niveles |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecognitionOptions.razor` | Crear | pantalla de opciones |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsQueries.cs` | Crear | regla compartida de ganancias por miembro |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsRankingService.cs` | Modificar | usa `EarningsQueries` |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.cs` | Crear | nocturno y consulta del marcador |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.History.cs` | Crear | carga histórica (SQL) y escritura del marcador |
| `MLMConquerorGlobalEdition.BizCenter/Jobs/ClubRecognitionJob.cs` | Crear | job nocturno |
| `MLMConquerorGlobalEdition.BizCenter/Program.cs` | Modificar | registro y `RecurringJob` |
| `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionDetectionController.cs` | Crear | carga histórica manual (SuperAdmin) |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionViews.cs` | Crear | vista, filtro, fila, textos y slugs |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionReportService.cs` | Crear | vistas y registros de un miembro |
| `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberRecognitionController.cs` | Crear | lista y captura con auditoría |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Shared/MemberRecognitionPanel.razor` | Crear | contenido de la pestaña |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminMemberProfile.razor` | Modificar | pestaña «Recognition» |
| `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionReportController.cs` | Crear | rejilla y CSV de las vistas |
| `MLMConquerorGlobalEdition.AdminAPI/Program.cs` | Modificar | registro de detector y servicio |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminClubRecognitionReport.razor` | Crear | página con cinco rutas |
| `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor` | Modificar | grupos Club Recognition y Parameters |
| `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionTestData.cs` | Crear | siembra compartida de las pruebas |
| `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognition*Tests.cs`, `EarningsQueriesTests.cs`, `Admin*RecognitionTests.cs` | Crear | modelo, parámetros, detector, servicio, controladores |
| `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerClubRecognitionTests.cs` | Crear | histórico y nocturno contra SQL Server + SQL para medir |
| `MLMConquerorGlobalEdition.BizCenter.Tests/ClubRecognitionJobTests.cs` | Crear | el job elige histórico o nocturno y va en `bizcenter` |

---

### Task 1: Modelo, semilla y migración

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Recognition/RecognitionLevel.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Recognition/RecognitionOption.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Recognition/RecognitionSource.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Recognition/MemberRecognition.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/RecognitionLevelConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/RecognitionOptionConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/MemberRecognitionConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs`
- Create (EF): `MLMConquerorGlobalEdition.Repository/Migrations/<timestamp>_ReconocimientoDeClubes.cs`
- Regenerate: `DeploymentScripts/migrate-rds.sql`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionModelTests.cs`

- [ ] **Step 1: Pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Esquema de Club Recognition en el modelo de diseño de SQL Server (sin conectarse): una fila por Ambassador y nivel,
/// fechas sin hora y la semilla de niveles y opciones de la especificación.
/// </summary>
public class ClubRecognitionModelTests
{
    private static readonly IModel Modelo = CrearModelo();

    private static IModel CrearModelo()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=modelo-sin-conexion;Integrated Security=true;TrustServerCertificate=True")
            .Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entidad<T>() => Modelo.FindEntityType(typeof(T))!;

    [Fact]
    public void Una_fila_por_Ambassador_y_nivel()
        => Entidad<MemberRecognition>().GetIndexes().Should().ContainSingle(i => i.IsUnique
            && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "MemberId", "RecognitionLevelId" }));

    [Fact]
    public void Reached_y_el_mes_de_la_llamada_son_fechas_sin_hora()
    {
        Entidad<MemberRecognition>().FindProperty(nameof(MemberRecognition.ReachedDate))!.GetColumnType()
            .Should().Be("date");
        Entidad<MemberRecognition>().FindProperty(nameof(MemberRecognition.CeoCallMonth))!.GetColumnType()
            .Should().Be("date");
    }

    [Fact]
    public void Semilla_de_los_seis_niveles()
    {
        var niveles = Entidad<RecognitionLevel>().GetSeedData()
            .Select(d => ((int)d["Id"]!, (string)d["Name"]!, (decimal)d["Threshold"]!,
                          d.TryGetValue("WindowDays", out var ventana) ? (int?)ventana : null,
                          (int)d["SortOrder"]!, (bool)d["IsActive"]!))
            .OrderBy(n => n.Item1)
            .ToList();

        niveles.Should().Equal(
            (1, "100k Earner",                    50_000m,    (int?)180, 1, true),
            (2, "500k Earner",                    250_000m,   (int?)180, 2, true),
            (3, "Million Dollar Earner",          500_000m,   (int?)180, 3, true),
            (4, "100k Lifetime Earner",           100_000m,   (int?)null, 4, true),
            (5, "500k Lifetime Earner",           500_000m,   (int?)null, 5, true),
            (6, "Lifetime Million Dollar Earner", 1_000_000m, (int?)null, 6, true));
    }

    [Fact]
    public void Semilla_de_las_opciones()
    {
        var opciones = Entidad<RecognitionOption>().GetSeedData()
            .Select(d => ((int)d["Id"]!, (string)d["Name"]!,
                          d.TryGetValue("RequiresReason", out var motivo) && (bool)motivo!))
            .OrderBy(o => o.Item1)
            .ToList();

        opciones.Should().Equal(
            (1, "Recognized Higher Level", false),
            (2, "Recognized Long Time Ago", false),
            (3, "Do not recognize", true));
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~ClubRecognitionModelTests"`
Expected: FAIL de compilación («El nombre de espacio Recognition no existe»).

- [ ] **Step 3: Entidades**

`MLMConquerorGlobalEdition.Domain/Entities/Recognition/RecognitionLevel.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Recognition;

/// <summary>
/// Nivel de Club Recognition (parámetro del admin): quien gana <see cref="Threshold"/> o más en la ventana de
/// <see cref="WindowDays"/> días (null = de por vida) lo alcanza. Las ganancias siguen la regla de los reportes Earnings.
/// Se desactiva, no se borra: los registros que ya lo alcanzaron lo referencian.
/// </summary>
public class RecognitionLevel : AuditChangesIntKey
{
    public string  Name            { get; set; } = string.Empty;
    public decimal Threshold       { get; set; }
    public int?    WindowDays      { get; set; }
    public string? Reward          { get; set; }
    public bool    RewardNeedsSize { get; set; }
    public int     SortOrder       { get; set; }
    public bool    IsActive        { get; set; } = true;
}
```

`MLMConquerorGlobalEdition.Domain/Entities/Recognition/RecognitionOption.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Recognition;

/// <summary>
/// Resultado de reconocimiento que no es un mes de CEO Call ni un evento (p. ej. «Recognized Higher Level»,
/// «Do not recognize»). Sirve para las dos columnas. Con <see cref="RequiresReason"/>, el admin escribe el motivo y la
/// vista muestra «Nombre – motivo».
/// </summary>
public class RecognitionOption : AuditChangesIntKey
{
    public string Name           { get; set; } = string.Empty;
    public bool   RequiresReason { get; set; }
    public int    SortOrder      { get; set; }
    public bool   IsActive       { get; set; } = true;
}
```

`MLMConquerorGlobalEdition.Domain/Entities/Recognition/RecognitionSource.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Domain.Entities.Recognition;

/// <summary>Quién creó el registro: la carga histórica (Reached = día en que se cruzó el umbral) o el proceso nocturno.</summary>
public enum RecognitionSource
{
    Historical = 1,
    Nightly    = 2
}
```

`MLMConquerorGlobalEdition.Domain/Entities/Recognition/MemberRecognition.cs`:

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Recognition;

/// <summary>
/// Un Ambassador alcanzó un nivel de Club Recognition. Una fila por Ambassador y nivel; una vez creada no se borra aunque
/// después gane menos. El admin captura el reconocimiento: CEO Call = un mes (<see cref="CeoCallMonth"/>, día 1) o una
/// opción; evento = un <see cref="CorporateEvent"/> o una opción; talla del premio y notas. Cada cambio se audita.
/// </summary>
public class MemberRecognition : AuditChangesLongKey
{
    public string            MemberId           { get; set; } = string.Empty;
    public int               RecognitionLevelId { get; set; }
    public DateTime          ReachedDate        { get; set; }
    public decimal           QualifyingTotal    { get; set; }
    public RecognitionSource Source             { get; set; }

    public DateTime? CeoCallMonth    { get; set; }
    public int?      CeoCallOptionId { get; set; }
    public string?   CeoCallReason   { get; set; }

    public string? EventId       { get; set; }
    public int?    EventOptionId { get; set; }
    public string? EventReason   { get; set; }

    public string? RewardSize { get; set; }
    public string? Notes      { get; set; }

    public DateTime? LastUpdateDate { get; set; }
    public string?   LastUpdateBy   { get; set; }

    public RecognitionLevel?  Level         { get; set; }
    public RecognitionOption? CeoCallOption { get; set; }
    public RecognitionOption? EventOption   { get; set; }
    public CorporateEvent?    Event         { get; set; }
}
```

- [ ] **Step 4: Configuraciones con semilla**

`MLMConquerorGlobalEdition.Repository/Configurations/RecognitionLevelConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class RecognitionLevelConfiguration : IEntityTypeConfiguration<RecognitionLevel>
{
    private static readonly DateTime SeedDate = new(2026, 11, 14);

    public void Configure(EntityTypeBuilder<RecognitionLevel> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Threshold).HasPrecision(18, 4);   // como CommissionEarning.Amount
        builder.Property(x => x.Reward).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Valores iniciales de la especificación (decisión del 2026-11-14); el admin los edita en Parameters.
        builder.HasData(
            Level(1, "100k Earner",                    50_000m,    180,  1),
            Level(2, "500k Earner",                    250_000m,   180,  2),
            Level(3, "Million Dollar Earner",          500_000m,   180,  3),
            Level(4, "100k Lifetime Earner",           100_000m,   null, 4),
            Level(5, "500k Lifetime Earner",           500_000m,   null, 5),
            Level(6, "Lifetime Million Dollar Earner", 1_000_000m, null, 6));
    }

    private static RecognitionLevel Level(int id, string name, decimal threshold, int? windowDays, int sortOrder) => new()
    {
        Id = id, Name = name, Threshold = threshold, WindowDays = windowDays, SortOrder = sortOrder, IsActive = true,
        CreationDate = SeedDate, CreatedBy = "system"
    };
}
```

`MLMConquerorGlobalEdition.Repository/Configurations/RecognitionOptionConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class RecognitionOptionConfiguration : IEntityTypeConfiguration<RecognitionOption>
{
    private static readonly DateTime SeedDate = new(2026, 11, 14);

    public void Configure(EntityTypeBuilder<RecognitionOption> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasData(
            new RecognitionOption { Id = 1, Name = "Recognized Higher Level",  SortOrder = 1, IsActive = true, CreationDate = SeedDate, CreatedBy = "system" },
            new RecognitionOption { Id = 2, Name = "Recognized Long Time Ago", SortOrder = 2, IsActive = true, CreationDate = SeedDate, CreatedBy = "system" },
            new RecognitionOption { Id = 3, Name = "Do not recognize", RequiresReason = true, SortOrder = 3, IsActive = true, CreationDate = SeedDate, CreatedBy = "system" });
    }
}
```

`MLMConquerorGlobalEdition.Repository/Configurations/MemberRecognitionConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MemberRecognitionConfiguration : IEntityTypeConfiguration<MemberRecognition>
{
    public void Configure(EntityTypeBuilder<MemberRecognition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);   // como MemberProfiles.MemberId
        builder.Property(x => x.ReachedDate).HasColumnType("date");
        builder.Property(x => x.QualifyingTotal).HasPrecision(18, 4);
        builder.Property(x => x.CeoCallMonth).HasColumnType("date");
        builder.Property(x => x.CeoCallReason).HasMaxLength(300);
        builder.Property(x => x.EventReason).HasMaxLength(300);
        builder.Property(x => x.RewardSize).HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Una fila por Ambassador y nivel: la detección (nocturna o histórica) es idempotente contra este índice.
        builder.HasIndex(x => new { x.MemberId, x.RecognitionLevelId })
               .IsUnique()
               .HasDatabaseName("UX_MemberRecognitions_Member_Level");
        builder.HasIndex(x => new { x.RecognitionLevelId, x.ReachedDate });

        builder.HasOne(x => x.Level).WithMany().HasForeignKey(x => x.RecognitionLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CeoCallOption).WithMany().HasForeignKey(x => x.CeoCallOptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EventOption).WithMany().HasForeignKey(x => x.EventOptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 5: DbSets**

En `AppDbContext.cs`, añadir `using MLMConquerorGlobalEdition.Domain.Entities.Recognition;` con los demás `using` y, justo después de `public DbSet<MemberCountrySnapshot> MemberCountrySnapshots => Set<MemberCountrySnapshot>();`:

```csharp
    public DbSet<RecognitionLevel>  RecognitionLevels  => Set<RecognitionLevel>();
    public DbSet<RecognitionOption> RecognitionOptions => Set<RecognitionOption>();
    public DbSet<MemberRecognition> MemberRecognitions => Set<MemberRecognition>();
```

- [ ] **Step 6: Correr las pruebas del modelo**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~ClubRecognitionModelTests"`
Expected: PASS (4).

- [ ] **Step 7: Migración, script y base local**

Run: `dotnet ef migrations add ReconocimientoDeClubes --project MLMConquerorGlobalEdition.Repository`
Expected: `Migrations/<timestamp>_ReconocimientoDeClubes.cs` con tres `CreateTable` (`RecognitionLevels`, `RecognitionOptions`, `MemberRecognitions`), sus índices (`IX_RecognitionLevels_Name` y `IX_RecognitionOptions_Name` únicos, `UX_MemberRecognitions_Member_Level`, `IX_MemberRecognitions_RecognitionLevelId_ReachedDate`, los de `CeoCallOptionId`, `EventOptionId` y `EventId`), cuatro FK `ReferentialAction.Restrict` y dos `InsertData` (6 niveles, 3 opciones). Si trae cualquier otro cambio, detenerse: el modelo tenía deriva (p. ej. trabajo sin migrar de otra rama) y hay que revisarla antes de seguir.
Run: `dotnet ef migrations script --idempotent --project MLMConquerorGlobalEdition.Repository -o DeploymentScripts/migrate-rds.sql`
Expected: el diff de `migrate-rds.sql` solo añade el bloque de `ReconocimientoDeClubes`.
Run: `dotnet ef database update --project MLMConquerorGlobalEdition.Repository --connection "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True;Command Timeout=900"`
Expected: Done. **No** se aplica a `MLMConquerorGlobalEditionDb_Escala`.
Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~Migraciones"`
Expected: PASS (las `[DesdeCeroFact]` se saltan si no hay SQL Server; si corren, pasan).

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/Entities/Recognition MLMConquerorGlobalEdition.Repository/Configurations/RecognitionLevelConfiguration.cs MLMConquerorGlobalEdition.Repository/Configurations/RecognitionOptionConfiguration.cs MLMConquerorGlobalEdition.Repository/Configurations/MemberRecognitionConfiguration.cs MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs MLMConquerorGlobalEdition.Repository/Migrations DeploymentScripts/migrate-rds.sql MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionModelTests.cs
git commit -m "Reportes: tablas de Club Recognition (niveles y opciones con semilla, registro unico por Ambassador y nivel) y script de despliegue regenerado"
```

---

### Task 2: Endpoints de parámetros (niveles, opciones y listas para la captura)

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionTestData.cs`
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionParametersController.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminClubRecognitionParametersControllerTests.cs`

Contrato (todo SuperAdmin y Admin):
- `GET api/v1/admin/club-recognition/levels` → `ApiResponse<List<LevelDto>>` por SortOrder.
- `POST api/v1/admin/club-recognition/levels`, `PUT .../levels/{id}` con `LevelRequest` → `LevelDto`; 400 con `INVALID_NAME`, `INVALID_THRESHOLD`, `INVALID_WINDOW`, `INVALID_REWARD`, `LEVEL_NAME_TAKEN`; 404 `LEVEL_NOT_FOUND`.
- `GET/POST api/v1/admin/club-recognition/options`, `PUT .../options/{id}` con `OptionRequest` → `OptionDto`; 400 `INVALID_NAME`, `OPTION_NAME_TAKEN`; 404 `OPTION_NOT_FOUND`.
- `GET api/v1/admin/club-recognition/lookups` → `{ levels, options, events }`: todos los niveles y opciones (con `IsActive`) y los eventos corporativos no borrados, del más reciente al más antiguo.
- No hay DELETE: se desactiva.

- [ ] **Step 1: Siembra compartida de las pruebas**

`MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionTestData.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>Base InMemory y siembra de las pruebas de Club Recognition. Los métodos añaden al contexto, no guardan.</summary>
internal static class ClubRecognitionTestData
{
    public static readonly DateTime Semilla = new(2026, 11, 14);

    public static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Los seis niveles de la semilla (mismos ids, montos y ventanas). <paramref name="conTalla"/>: ids con talla.</summary>
    public static void Niveles(AppDbContext db, params int[] conTalla) => db.RecognitionLevels.AddRange(
        Nivel(1, "100k Earner",                    50_000m,    180,  1, conTalla),
        Nivel(2, "500k Earner",                    250_000m,   180,  2, conTalla),
        Nivel(3, "Million Dollar Earner",          500_000m,   180,  3, conTalla),
        Nivel(4, "100k Lifetime Earner",           100_000m,   null, 4, conTalla),
        Nivel(5, "500k Lifetime Earner",           500_000m,   null, 5, conTalla),
        Nivel(6, "Lifetime Million Dollar Earner", 1_000_000m, null, 6, conTalla));

    private static RecognitionLevel Nivel(int id, string nombre, decimal umbral, int? ventana, int orden, int[] conTalla) => new()
    {
        Id = id, Name = nombre, Threshold = umbral, WindowDays = ventana, SortOrder = orden, IsActive = true,
        RewardNeedsSize = conTalla.Contains(id), CreationDate = Semilla, CreatedBy = "seed"
    };

    /// <summary>Las tres opciones de la semilla (ids 1 a 3; la 3 pide motivo).</summary>
    public static void Opciones(AppDbContext db) => db.RecognitionOptions.AddRange(
        new RecognitionOption { Id = 1, Name = "Recognized Higher Level",  SortOrder = 1, CreationDate = Semilla, CreatedBy = "seed" },
        new RecognitionOption { Id = 2, Name = "Recognized Long Time Ago", SortOrder = 2, CreationDate = Semilla, CreatedBy = "seed" },
        new RecognitionOption { Id = 3, Name = "Do not recognize", RequiresReason = true, SortOrder = 3, CreationDate = Semilla, CreatedBy = "seed" });

    public static MemberProfile Miembro(AppDbContext db, string memberId, string nombre = "Nombre", string? apellido = null,
        string? email = null, string pais = "US", string? slug = null, MemberType tipo = MemberType.Ambassador,
        MemberAccountStatus estado = MemberAccountStatus.Active, bool corporativa = false)
    {
        var perfil = new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = memberId, FirstName = nombre, LastName = apellido ?? memberId,
            Email = email ?? $"{memberId.ToLowerInvariant()}@example.com", Country = pais, ReplicateSiteSlug = slug,
            MemberType = tipo, Status = estado, IsCorporateAccount = corporativa,
            CreatedBy = "seed", CreationDate = Semilla, LastUpdateDate = Semilla
        };
        db.MemberProfiles.Add(perfil);
        return perfil;
    }

    public static void Comision(AppDbContext db, string beneficiario, decimal monto, DateTime fecha,
        CommissionEarningStatus estado = CommissionEarningStatus.Paid) =>
        db.CommissionEarnings.Add(new CommissionEarning
        {
            Id = Guid.NewGuid().ToString(), BeneficiaryMemberId = beneficiario, Amount = monto, EarnedDate = fecha,
            PaymentDate = fecha, Status = estado, CommissionTypeId = 1,
            CreatedBy = "seed", CreationDate = fecha, LastUpdateDate = fecha
        });

    public static MemberRecognition Registro(AppDbContext db, long id, string memberId, int nivel, DateTime alcanzado,
        decimal total, RecognitionSource origen = RecognitionSource.Historical)
    {
        var registro = new MemberRecognition
        {
            Id = id, MemberId = memberId, RecognitionLevelId = nivel, ReachedDate = alcanzado, QualifyingTotal = total,
            Source = origen, CreationDate = Semilla, CreatedBy = "seed"
        };
        db.MemberRecognitions.Add(registro);
        return registro;
    }

    public static void Evento(AppDbContext db, string id, string titulo, DateTime fecha, bool borrado = false) =>
        db.CorporateEvents.Add(new CorporateEvent
        {
            Id = id, Title = titulo, EventDate = fecha, IsDeleted = borrado,
            CreatedBy = "seed", CreationDate = Semilla, LastUpdateDate = Semilla
        });

    public static IDateTimeProvider Reloj(DateTime ahora)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(ahora);
        return reloj.Object;
    }

    public static ICurrentUserService Usuario(string id = "admin-1")
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.SetupGet(u => u.UserId).Returns(id);
        return usuario.Object;
    }
}
```

- [ ] **Step 2: Pruebas que fallan**

`MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminClubRecognitionParametersControllerTests.cs`:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using static MLMConquerorGlobalEdition.AdminAPI.Controllers.AdminClubRecognitionParametersController;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminClubRecognitionParametersControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 0, 0);

    private static AdminClubRecognitionParametersController Controlador(AppDbContext db) =>
        new(db, ClubRecognitionTestData.Reloj(Ahora), ClubRecognitionTestData.Usuario());

    private static T Datos<T>(IActionResult r) =>
        ((ApiResponse<T>)r.Should().BeOfType<OkObjectResult>().Subject.Value!).Data!;

    private static string Codigo(IActionResult r) =>
        ((ApiResponse<object>)r.Should().BeOfType<BadRequestObjectResult>().Subject.Value!).ErrorCode!;

    [Fact]
    public async Task Crear_un_nivel_lo_guarda_limpio_con_autor_y_hora_del_servidor()
    {
        using var db = ClubRecognitionTestData.Db();

        var r = await Controlador(db).CreateLevel(
            new LevelRequest("  Diamond Club  ", 2_000_000m, null, " Ring ", true, 7), default);

        var dto = Datos<LevelDto>(r);
        dto.Should().Be(new LevelDto(dto.Id, "Diamond Club", 2_000_000m, null, "Ring", true, 7, true));
        var guardado = await db.RecognitionLevels.SingleAsync();
        (guardado.CreatedBy, guardado.CreationDate).Should().Be(("admin-1", Ahora));
    }

    [Theory]
    [InlineData("", 1000, null, "INVALID_NAME")]
    [InlineData("Club", 0, null, "INVALID_THRESHOLD")]
    [InlineData("Club", 1000, 0, "INVALID_WINDOW")]
    [InlineData("Club", 1000, 3651, "INVALID_WINDOW")]
    public async Task Un_nivel_invalido_da_400(string nombre, int umbral, int? ventana, string codigo)
    {
        using var db = ClubRecognitionTestData.Db();

        var r = await Controlador(db).CreateLevel(new LevelRequest(nombre, umbral, ventana, null, false, 1), default);

        Codigo(r).Should().Be(codigo);
        (await db.RecognitionLevels.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Dos_niveles_no_pueden_llamarse_igual()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Niveles(db);
        await db.SaveChangesAsync();

        var r = await Controlador(db).CreateLevel(new LevelRequest("100k Earner", 1m, null, null, false, 9), default);

        Codigo(r).Should().Be("LEVEL_NAME_TAKEN");
    }

    [Fact]
    public async Task Editar_un_nivel_cambia_sus_campos_y_deja_quien_y_cuando()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Niveles(db);
        await db.SaveChangesAsync();

        var r = await Controlador(db).UpdateLevel(4,
            new LevelRequest("100k Lifetime Earner", 120_000m, null, "Ring", true, 4, IsActive: false), default);

        Datos<LevelDto>(r).Should().Be(new LevelDto(4, "100k Lifetime Earner", 120_000m, null, "Ring", true, 4, false));
        var nivel = await db.RecognitionLevels.SingleAsync(l => l.Id == 4);
        (nivel.LastUpdateBy, nivel.LastUpdateDate).Should().Be(("admin-1", (DateTime?)Ahora));
    }

    [Fact]
    public async Task Editar_un_nivel_que_no_existe_da_404()
    {
        using var db = ClubRecognitionTestData.Db();

        var r = await Controlador(db).UpdateLevel(99, new LevelRequest("X", 1m, null, null, false, 1), default);

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Crear_y_editar_una_opcion()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Opciones(db);
        await db.SaveChangesAsync();

        var creada = Datos<OptionDto>(await Controlador(db).CreateOption(new OptionRequest(" Recognized by video ", false, 4), default));
        creada.Should().Be(new OptionDto(creada.Id, "Recognized by video", false, 4, true));

        var editada = Datos<OptionDto>(await Controlador(db).UpdateOption(3,
            new OptionRequest("Do not recognize", true, 3, IsActive: false), default));
        editada.Should().Be(new OptionDto(3, "Do not recognize", true, 3, false));

        Codigo(await Controlador(db).CreateOption(new OptionRequest("Recognized Higher Level", false, 5), default))
            .Should().Be("OPTION_NAME_TAKEN");
        Codigo(await Controlador(db).CreateOption(new OptionRequest(" ", false, 5), default))
            .Should().Be("INVALID_NAME");
    }

    [Fact]
    public async Task Las_listas_traen_niveles_por_orden_todas_las_opciones_y_los_eventos_vigentes_del_mas_reciente()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Niveles(db);
        ClubRecognitionTestData.Opciones(db);
        (await db.SaveChangesAsync()).Should().BeGreaterThan(0);
        (await db.RecognitionOptions.SingleAsync(o => o.Id == 2)).IsActive = false;
        ClubRecognitionTestData.Evento(db, "EVT-1", "Bootcamp CIS 2026", new DateTime(2026, 3, 10));
        ClubRecognitionTestData.Evento(db, "EVT-2", "Leadership Summit 2026", new DateTime(2026, 9, 20));
        ClubRecognitionTestData.Evento(db, "EVT-3", "Cancelled Gala", new DateTime(2026, 10, 1), borrado: true);
        await db.SaveChangesAsync();

        var listas = Datos<LookupsDto>(await Controlador(db).Lookups(default));

        listas.Levels.Select(l => l.Id).Should().Equal(1, 2, 3, 4, 5, 6);
        listas.Options.Select(o => (o.Id, o.IsActive)).Should().Equal((1, true), (2, false), (3, true));
        listas.Events.Select(e => e.Id).Should().Equal("EVT-2", "EVT-1");
    }

    [Fact]
    public void Solo_SuperAdmin_y_Admin()
        => typeof(AdminClubRecognitionParametersController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin");
}
```

- [ ] **Step 3: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminClubRecognitionParametersControllerTests"`
Expected: FAIL de compilación («AdminClubRecognitionParametersController no existe»).

- [ ] **Step 4: Implementar el controlador**

`MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionParametersController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Parámetros de Club Recognition: niveles (monto, ventana, premio) y opciones de reconocimiento, más las listas que usa
/// la captura (lookups). No se borra nada: se desactiva, porque los registros ya creados los referencian.
/// </summary>
[ApiController]
[Route("api/v1/admin/club-recognition")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminClubRecognitionParametersController : ControllerBase
{
    public const int MaxWindowDays = 3650;

    private readonly AppDbContext        _db;
    private readonly IDateTimeProvider   _clock;
    private readonly ICurrentUserService _currentUser;

    public AdminClubRecognitionParametersController(AppDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser)
    {
        _db          = db;
        _clock       = clock;
        _currentUser = currentUser;
    }

    public sealed record LevelRequest(string Name, decimal Threshold, int? WindowDays, string? Reward, bool RewardNeedsSize,
        int SortOrder, bool IsActive = true);

    public sealed record OptionRequest(string Name, bool RequiresReason, int SortOrder, bool IsActive = true);

    public sealed record LevelDto(int Id, string Name, decimal Threshold, int? WindowDays, string? Reward,
        bool RewardNeedsSize, int SortOrder, bool IsActive);

    public sealed record OptionDto(int Id, string Name, bool RequiresReason, int SortOrder, bool IsActive);

    public sealed record EventDto(string Id, string Title, DateTime EventDate);

    public sealed record LookupsDto(IReadOnlyList<LevelDto> Levels, IReadOnlyList<OptionDto> Options,
        IReadOnlyList<EventDto> Events);

    // ── Niveles ──────────────────────────────────────────────────────────────

    [HttpGet("levels")]
    public async Task<IActionResult> Levels(CancellationToken ct) =>
        Ok(ApiResponse<List<LevelDto>>.Ok(await LevelsAsync(ct)));

    [HttpPost("levels")]
    public async Task<IActionResult> CreateLevel([FromBody] LevelRequest request, CancellationToken ct)
    {
        var error = await ValidateLevelAsync(request, null, ct);
        if (error is not null) return BadRequest(error);

        var level = new RecognitionLevel { CreationDate = _clock.Now, CreatedBy = _currentUser.UserId };
        Apply(level, request);
        _db.RecognitionLevels.Add(level);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<LevelDto>.Ok(ToDto(level)));
    }

    [HttpPut("levels/{id:int}")]
    public async Task<IActionResult> UpdateLevel(int id, [FromBody] LevelRequest request, CancellationToken ct)
    {
        var level = await _db.RecognitionLevels.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (level is null) return NotFound(Fail("LEVEL_NOT_FOUND", "Recognition level not found."));

        var error = await ValidateLevelAsync(request, id, ct);
        if (error is not null) return BadRequest(error);

        Apply(level, request);
        level.LastUpdateDate = _clock.Now;
        level.LastUpdateBy   = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<LevelDto>.Ok(ToDto(level)));
    }

    // ── Opciones ─────────────────────────────────────────────────────────────

    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        Ok(ApiResponse<List<OptionDto>>.Ok(await OptionsAsync(ct)));

    [HttpPost("options")]
    public async Task<IActionResult> CreateOption([FromBody] OptionRequest request, CancellationToken ct)
    {
        var error = await ValidateOptionAsync(request, null, ct);
        if (error is not null) return BadRequest(error);

        var option = new RecognitionOption { CreationDate = _clock.Now, CreatedBy = _currentUser.UserId };
        Apply(option, request);
        _db.RecognitionOptions.Add(option);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<OptionDto>.Ok(ToDto(option)));
    }

    [HttpPut("options/{id:int}")]
    public async Task<IActionResult> UpdateOption(int id, [FromBody] OptionRequest request, CancellationToken ct)
    {
        var option = await _db.RecognitionOptions.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (option is null) return NotFound(Fail("OPTION_NOT_FOUND", "Recognition option not found."));

        var error = await ValidateOptionAsync(request, id, ct);
        if (error is not null) return BadRequest(error);

        Apply(option, request);
        option.LastUpdateDate = _clock.Now;
        option.LastUpdateBy   = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<OptionDto>.Ok(ToDto(option)));
    }

    // ── Listas para la captura y los filtros ────────────────────────────────

    /// <summary>Todos los niveles y opciones (con IsActive: la captura muestra la opción inactiva que un registro ya tiene)
    /// y los eventos corporativos no borrados, del más reciente al más antiguo.</summary>
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        var events = await _db.CorporateEvents.AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.EventDate)
            .Select(e => new EventDto(e.Id, e.Title, e.EventDate))
            .ToListAsync(ct);
        return Ok(ApiResponse<LookupsDto>.Ok(new LookupsDto(await LevelsAsync(ct), await OptionsAsync(ct), events)));
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────

    private Task<List<LevelDto>> LevelsAsync(CancellationToken ct) =>
        _db.RecognitionLevels.AsNoTracking()
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Id)
            .Select(l => new LevelDto(l.Id, l.Name, l.Threshold, l.WindowDays, l.Reward, l.RewardNeedsSize, l.SortOrder, l.IsActive))
            .ToListAsync(ct);

    private Task<List<OptionDto>> OptionsAsync(CancellationToken ct) =>
        _db.RecognitionOptions.AsNoTracking()
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
            .Select(o => new OptionDto(o.Id, o.Name, o.RequiresReason, o.SortOrder, o.IsActive))
            .ToListAsync(ct);

    private async Task<ApiResponse<object>?> ValidateLevelAsync(LevelRequest r, int? id, CancellationToken ct)
    {
        var name = r.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
            return Fail("INVALID_NAME", "Name is required (100 characters max).");
        if (r.Threshold <= 0)
            return Fail("INVALID_THRESHOLD", "Threshold must be greater than zero.");
        if (r.WindowDays is < 1 or > MaxWindowDays)
            return Fail("INVALID_WINDOW", $"Window must be between 1 and {MaxWindowDays} days, or empty for lifetime.");
        if (r.Reward?.Trim().Length > 200)
            return Fail("INVALID_REWARD", "Reward is 200 characters max.");
        if (await _db.RecognitionLevels.AnyAsync(l => l.Name == name && l.Id != id, ct))
            return Fail("LEVEL_NAME_TAKEN", "Another level already has that name.");
        return null;
    }

    private async Task<ApiResponse<object>?> ValidateOptionAsync(OptionRequest r, int? id, CancellationToken ct)
    {
        var name = r.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
            return Fail("INVALID_NAME", "Name is required (100 characters max).");
        if (await _db.RecognitionOptions.AnyAsync(o => o.Name == name && o.Id != id, ct))
            return Fail("OPTION_NAME_TAKEN", "Another option already has that name.");
        return null;
    }

    private static void Apply(RecognitionLevel level, LevelRequest r)
    {
        level.Name            = r.Name.Trim();
        level.Threshold       = r.Threshold;
        level.WindowDays      = r.WindowDays;
        level.Reward          = string.IsNullOrWhiteSpace(r.Reward) ? null : r.Reward.Trim();
        level.RewardNeedsSize = r.RewardNeedsSize;
        level.SortOrder       = r.SortOrder;
        level.IsActive        = r.IsActive;
    }

    private static void Apply(RecognitionOption option, OptionRequest r)
    {
        option.Name           = r.Name.Trim();
        option.RequiresReason = r.RequiresReason;
        option.SortOrder      = r.SortOrder;
        option.IsActive       = r.IsActive;
    }

    private static LevelDto ToDto(RecognitionLevel l) =>
        new(l.Id, l.Name, l.Threshold, l.WindowDays, l.Reward, l.RewardNeedsSize, l.SortOrder, l.IsActive);

    private static OptionDto ToDto(RecognitionOption o) => new(o.Id, o.Name, o.RequiresReason, o.SortOrder, o.IsActive);

    private static ApiResponse<object> Fail(string code, string message) => ApiResponse<object>.Fail(code, message);
}
```

Si el `using` de `IDateTimeProvider` choca con `AdminAPI.Services`, usar el alias de los otros controladores: `using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;`.

- [ ] **Step 5: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminClubRecognitionParametersControllerTests"`
Expected: PASS (11).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionParametersController.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionTestData.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminClubRecognitionParametersControllerTests.cs
git commit -m "Admin: endpoints de parametros de Club Recognition (niveles y opciones sin borrado, validacion de nombre, monto y ventana) y listas para la captura"
```

---

### Task 3: Pantallas de parámetros (niveles y opciones)

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecognitionLevels.razor`
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecognitionOptions.razor`

Mismo patrón que `AdminDocumentTypes.razor` (tabla y modal de alta/edición), sin borrar: el estado se cambia con «Active». Los catálogos tienen pocas filas: tabla simple, sin rejilla de servidor.

- [ ] **Step 1: Pantalla de niveles**

`MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecognitionLevels.razor`:

```razor
@page "/admin/parameters/recognition-levels"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin,Admin")]
@inject HttpClient Http

<PageTitle>Recognition Levels — Admin</PageTitle>

<BreadcrumbNav Items="@(new List<BreadcrumbNav.BreadcrumbItem> { new("Admin Dashboard", "/admin"), new("Recognition Levels") })" />

@if (_alertMsg is not null)
{
    <div class="alert @(_alertIsError ? "alert-danger" : "alert-success") alert-dismissible mb-3">
        @_alertMsg <button type="button" class="btn-close" @onclick="() => _alertMsg = null"></button>
    </div>
}

<div class="card border-0 shadow-sm">
    <div class="card-header py-3 d-flex align-items-center gap-2">
        <h6 class="card-title mb-0 fw-semibold flex-grow-1">
            <i class="ri-trophy-line me-2 text-primary"></i>Recognition Levels
        </h6>
        <button class="btn btn-primary btn-sm" @onclick="OpenCreate">
            <i class="ri-add-line me-1"></i>New Level
        </button>
    </div>
    <div class="card-body p-0">
        @if (_loading)
        {
            <div class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary"></div></div>
        }
        else
        {
            <div class="table-responsive">
                <table class="table table-sm align-middle mb-0">
                    <thead class="table-light">
                        <tr>
                            <th>Order</th><th>Name</th><th class="text-end">Threshold</th><th>Window</th>
                            <th>Reward</th><th>Needs size</th><th>Status</th><th></th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var l in _items)
                        {
                            <tr>
                                <td>@l.SortOrder</td>
                                <td class="fw-semibold">@l.Name</td>
                                <td class="text-end">@l.Threshold.ToString("N2")</td>
                                <td>@(l.WindowDays is null ? "Lifetime" : $"{l.WindowDays} days")</td>
                                <td>@l.Reward</td>
                                <td>@(l.RewardNeedsSize ? "Yes" : "No")</td>
                                <td>
                                    <span class="badge @(l.IsActive ? "bg-success" : "bg-secondary")">@(l.IsActive ? "Active" : "Inactive")</span>
                                </td>
                                <td class="text-end">
                                    <button class="btn btn-sm btn-outline-primary py-0" @onclick="() => OpenEdit(l)">
                                        <i class="ri-edit-line"></i>
                                    </button>
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </div>
</div>

@if (_showForm)
{
    <div class="modal d-block" tabindex="-1" style="background:rgba(0,0,0,.45)">
        <div class="modal-dialog modal-dialog-scrollable">
            <div class="modal-content border-0 rounded-3 shadow">
                <div class="modal-header border-bottom">
                    <h5 class="modal-title fw-bold">@(_editId is null ? "New Recognition Level" : "Edit Recognition Level")</h5>
                    <button type="button" class="btn-close" @onclick="CloseForm"></button>
                </div>
                <div class="modal-body">
                    @if (_formError is not null)
                    {
                        <div class="alert alert-danger py-2">@_formError</div>
                    }
                    <div class="row g-3">
                        <div class="col-12">
                            <label class="form-label fw-semibold">Name <span class="text-danger">*</span></label>
                            <input class="form-control" maxlength="100" @bind="_form.Name" placeholder="e.g. 100k Earner" />
                        </div>
                        <div class="col-md-6">
                            <label class="form-label fw-semibold">Threshold <span class="text-danger">*</span></label>
                            <input type="number" min="0" step="0.01" class="form-control" @bind="_form.Threshold" />
                        </div>
                        <div class="col-md-6">
                            <label class="form-label fw-semibold">Window</label>
                            <div class="form-check">
                                <input class="form-check-input" type="checkbox" id="lvlLifetime" @bind="_form.Lifetime" />
                                <label class="form-check-label" for="lvlLifetime">Lifetime</label>
                            </div>
                            @if (!_form.Lifetime)
                            {
                                <div class="input-group input-group-sm mt-1">
                                    <input type="number" min="1" max="3650" class="form-control" @bind="_form.WindowDays" />
                                    <span class="input-group-text">days</span>
                                </div>
                            }
                        </div>
                        <div class="col-12">
                            <label class="form-label fw-semibold">Reward</label>
                            <input class="form-control" maxlength="200" @bind="_form.Reward" placeholder="e.g. Ring" />
                        </div>
                        <div class="col-md-6">
                            <div class="form-check">
                                <input class="form-check-input" type="checkbox" id="lvlSize" @bind="_form.RewardNeedsSize" />
                                <label class="form-check-label fw-semibold" for="lvlSize">Reward needs a size</label>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-check">
                                <input class="form-check-input" type="checkbox" id="lvlActive" @bind="_form.IsActive" />
                                <label class="form-check-label fw-semibold" for="lvlActive">Active</label>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <label class="form-label fw-semibold">Sort Order</label>
                            <input type="number" class="form-control" @bind="_form.SortOrder" />
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn btn-secondary" @onclick="CloseForm">Cancel</button>
                    <button class="btn btn-primary" @onclick="SaveAsync" disabled="@_saving">
                        @if (_saving) { <span class="spinner-border spinner-border-sm me-1"></span> }
                        @(_editId is null ? "Create" : "Save Changes")
                    </button>
                </div>
            </div>
        </div>
    </div>
}

@code {
    private const string Endpoint = "api/v1/admin/club-recognition/levels";

    private List<LevelDto> _items = new();
    private bool _loading = true;
    private bool _saving;
    private string? _alertMsg;
    private bool _alertIsError;
    private bool _showForm;
    private int? _editId;
    private string? _formError;
    private LevelForm _form = new();

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiResp<List<LevelDto>>>(Endpoint);
            _items = resp?.Data ?? new();
        }
        catch (Exception ex) { _alertMsg = $"Error loading levels: {ex.Message}"; _alertIsError = true; }
        finally { _loading = false; }
    }

    private void OpenCreate()
    {
        _editId = null;
        _formError = null;
        _form = new LevelForm { SortOrder = _items.Count + 1 };
        _showForm = true;
    }

    private void OpenEdit(LevelDto l)
    {
        _editId = l.Id;
        _formError = null;
        _form = new LevelForm
        {
            Name = l.Name, Threshold = l.Threshold, Lifetime = l.WindowDays is null, WindowDays = l.WindowDays ?? 180,
            Reward = l.Reward, RewardNeedsSize = l.RewardNeedsSize, SortOrder = l.SortOrder, IsActive = l.IsActive
        };
        _showForm = true;
    }

    private void CloseForm() { _showForm = false; _editId = null; }

    private async Task SaveAsync()
    {
        _saving = true;
        _formError = null;
        try
        {
            var body = new
            {
                _form.Name, _form.Threshold, WindowDays = _form.Lifetime ? (int?)null : _form.WindowDays,
                _form.Reward, _form.RewardNeedsSize, _form.SortOrder, _form.IsActive
            };
            var resp = _editId is null
                ? await Http.PostAsJsonAsync(Endpoint, body)
                : await Http.PutAsJsonAsync($"{Endpoint}/{_editId}", body);
            if (resp.IsSuccessStatusCode)
            {
                _alertMsg = _editId is null ? "Level created." : "Level updated.";
                _alertIsError = false;
                CloseForm();
                await LoadAsync();
            }
            else _formError = await ErrorAsync(resp);
        }
        catch (Exception ex) { _formError = ex.Message; }
        finally { _saving = false; }
    }

    private static async Task<string> ErrorAsync(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<ApiResp<object>>();
            var msg  = body?.Errors?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(msg)) return msg;
        }
        catch { /* cuerpo no JSON */ }
        return $"Save failed ({(int)resp.StatusCode}).";
    }

    private sealed class LevelForm
    {
        public string  Name            { get; set; } = string.Empty;
        public decimal Threshold       { get; set; }
        public bool    Lifetime        { get; set; }
        public int     WindowDays      { get; set; } = 180;
        public string? Reward          { get; set; }
        public bool    RewardNeedsSize { get; set; }
        public int     SortOrder       { get; set; }
        public bool    IsActive        { get; set; } = true;
    }

    private sealed record LevelDto(int Id, string Name, decimal Threshold, int? WindowDays, string? Reward,
        bool RewardNeedsSize, int SortOrder, bool IsActive);
    private sealed record ApiResp<T>(bool Success, T? Data, string? ErrorCode, IEnumerable<string>? Errors);
}
```

- [ ] **Step 2: Pantalla de opciones**

`MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecognitionOptions.razor`:

```razor
@page "/admin/parameters/recognition-options"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin,Admin")]
@inject HttpClient Http

<PageTitle>Recognition Options — Admin</PageTitle>

<BreadcrumbNav Items="@(new List<BreadcrumbNav.BreadcrumbItem> { new("Admin Dashboard", "/admin"), new("Recognition Options") })" />

<p class="text-muted">Results an admin can pick instead of a CEO call month or an event (e.g. "Recognized Higher Level").
    Options that need a reason are shown as "Name – reason".</p>

@if (_alertMsg is not null)
{
    <div class="alert @(_alertIsError ? "alert-danger" : "alert-success") alert-dismissible mb-3">
        @_alertMsg <button type="button" class="btn-close" @onclick="() => _alertMsg = null"></button>
    </div>
}

<div class="card border-0 shadow-sm">
    <div class="card-header py-3 d-flex align-items-center gap-2">
        <h6 class="card-title mb-0 fw-semibold flex-grow-1">
            <i class="ri-list-check-2 me-2 text-primary"></i>Recognition Options
        </h6>
        <button class="btn btn-primary btn-sm" @onclick="OpenCreate">
            <i class="ri-add-line me-1"></i>New Option
        </button>
    </div>
    <div class="card-body p-0">
        @if (_loading)
        {
            <div class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary"></div></div>
        }
        else
        {
            <table class="table table-sm align-middle mb-0">
                <thead class="table-light">
                    <tr><th>Order</th><th>Name</th><th>Needs reason</th><th>Status</th><th></th></tr>
                </thead>
                <tbody>
                    @foreach (var o in _items)
                    {
                        <tr>
                            <td>@o.SortOrder</td>
                            <td class="fw-semibold">@o.Name</td>
                            <td>@(o.RequiresReason ? "Yes" : "No")</td>
                            <td>
                                <span class="badge @(o.IsActive ? "bg-success" : "bg-secondary")">@(o.IsActive ? "Active" : "Inactive")</span>
                            </td>
                            <td class="text-end">
                                <button class="btn btn-sm btn-outline-primary py-0" @onclick="() => OpenEdit(o)">
                                    <i class="ri-edit-line"></i>
                                </button>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        }
    </div>
</div>

@if (_showForm)
{
    <div class="modal d-block" tabindex="-1" style="background:rgba(0,0,0,.45)">
        <div class="modal-dialog">
            <div class="modal-content border-0 rounded-3 shadow">
                <div class="modal-header border-bottom">
                    <h5 class="modal-title fw-bold">@(_editId is null ? "New Recognition Option" : "Edit Recognition Option")</h5>
                    <button type="button" class="btn-close" @onclick="CloseForm"></button>
                </div>
                <div class="modal-body">
                    @if (_formError is not null)
                    {
                        <div class="alert alert-danger py-2">@_formError</div>
                    }
                    <div class="row g-3">
                        <div class="col-12">
                            <label class="form-label fw-semibold">Name <span class="text-danger">*</span></label>
                            <input class="form-control" maxlength="100" @bind="_form.Name" placeholder="e.g. Recognized Higher Level" />
                        </div>
                        <div class="col-md-6">
                            <div class="form-check">
                                <input class="form-check-input" type="checkbox" id="optReason" @bind="_form.RequiresReason" />
                                <label class="form-check-label fw-semibold" for="optReason">Needs a reason</label>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-check">
                                <input class="form-check-input" type="checkbox" id="optActive" @bind="_form.IsActive" />
                                <label class="form-check-label fw-semibold" for="optActive">Active</label>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <label class="form-label fw-semibold">Sort Order</label>
                            <input type="number" class="form-control" @bind="_form.SortOrder" />
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn btn-secondary" @onclick="CloseForm">Cancel</button>
                    <button class="btn btn-primary" @onclick="SaveAsync" disabled="@_saving">
                        @if (_saving) { <span class="spinner-border spinner-border-sm me-1"></span> }
                        @(_editId is null ? "Create" : "Save Changes")
                    </button>
                </div>
            </div>
        </div>
    </div>
}

@code {
    private const string Endpoint = "api/v1/admin/club-recognition/options";

    private List<OptionDto> _items = new();
    private bool _loading = true;
    private bool _saving;
    private string? _alertMsg;
    private bool _alertIsError;
    private bool _showForm;
    private int? _editId;
    private string? _formError;
    private OptionForm _form = new();

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiResp<List<OptionDto>>>(Endpoint);
            _items = resp?.Data ?? new();
        }
        catch (Exception ex) { _alertMsg = $"Error loading options: {ex.Message}"; _alertIsError = true; }
        finally { _loading = false; }
    }

    private void OpenCreate()
    {
        _editId = null;
        _formError = null;
        _form = new OptionForm { SortOrder = _items.Count + 1 };
        _showForm = true;
    }

    private void OpenEdit(OptionDto o)
    {
        _editId = o.Id;
        _formError = null;
        _form = new OptionForm { Name = o.Name, RequiresReason = o.RequiresReason, SortOrder = o.SortOrder, IsActive = o.IsActive };
        _showForm = true;
    }

    private void CloseForm() { _showForm = false; _editId = null; }

    private async Task SaveAsync()
    {
        _saving = true;
        _formError = null;
        try
        {
            var body = new { _form.Name, _form.RequiresReason, _form.SortOrder, _form.IsActive };
            var resp = _editId is null
                ? await Http.PostAsJsonAsync(Endpoint, body)
                : await Http.PutAsJsonAsync($"{Endpoint}/{_editId}", body);
            if (resp.IsSuccessStatusCode)
            {
                _alertMsg = _editId is null ? "Option created." : "Option updated.";
                _alertIsError = false;
                CloseForm();
                await LoadAsync();
            }
            else _formError = await ErrorAsync(resp);
        }
        catch (Exception ex) { _formError = ex.Message; }
        finally { _saving = false; }
    }

    private static async Task<string> ErrorAsync(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<ApiResp<object>>();
            var msg  = body?.Errors?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(msg)) return msg;
        }
        catch { /* cuerpo no JSON */ }
        return $"Save failed ({(int)resp.StatusCode}).";
    }

    private sealed class OptionForm
    {
        public string Name           { get; set; } = string.Empty;
        public bool   RequiresReason { get; set; }
        public int    SortOrder      { get; set; }
        public bool   IsActive       { get; set; } = true;
    }

    private sealed record OptionDto(int Id, string Name, bool RequiresReason, int SortOrder, bool IsActive);
    private sealed record ApiResp<T>(bool Success, T? Data, string? ErrorCode, IEnumerable<string>? Errors);
}
```

- [ ] **Step 3: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecognitionLevels.razor MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminRecognitionOptions.razor
git commit -m "Admin: pantallas de parametros de niveles y opciones de reconocimiento (alta, edicion y desactivacion)"
```

---

### Task 4: Regla de ganancias compartida

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsQueries.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsRankingService.cs` (método `ComputeAsync`)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/EarningsQueriesTests.cs`

Club Recognition calcula las ganancias «con la misma regla de los reportes Earnings». Esa regla se extrae a un solo sitio y `EarningsRankingService` la usa: el SQL generado es el mismo (filtro de estado y fechas, `GROUP BY BeneficiaryMemberId`, `SUM(Amount)`), así que sus pruebas no cambian.

- [ ] **Step 1: Pruebas que fallan**

```csharp
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
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~EarningsQueriesTests"`
Expected: FAIL de compilación («EarningsQueries no existe»).

- [ ] **Step 3: Implementar**

`MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsQueries.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Total de ganancias de un miembro (lo que devuelve <see cref="EarningsQueries.TotalsByMember"/>).</summary>
public sealed class MemberEarningsTotal
{
    public string  MemberId { get; init; } = string.Empty;
    public decimal Total    { get; init; }
}

/// <summary>
/// La regla de ganancias de los reportes del admin (Earnings Lifetime, Earnings por ventana y Club Recognition): suma de
/// <c>CommissionEarning.Amount</c> por beneficiario, solo Paid y Pending (las Cancelled no cuentan; las borradas tampoco,
/// por el filtro global), con <c>EarnedDate</c> en [<paramref name="from"/>, <paramref name="to"/>). Los residuales diarios
/// cuentan cuando se consolidan en CommissionEarning. Usa el índice IX_CommissionEarnings_EarnedDate_Beneficiary.
/// </summary>
public static class EarningsQueries
{
    public static IQueryable<MemberEarningsTotal> TotalsByMember(AppDbContext db, DateTime? from, DateTime to)
    {
        var earnings = db.CommissionEarnings.AsNoTracking()
            .Where(c => (c.Status == CommissionEarningStatus.Paid || c.Status == CommissionEarningStatus.Pending)
                     && c.EarnedDate < to);
        if (from is not null) earnings = earnings.Where(c => c.EarnedDate >= from);

        return earnings.GroupBy(c => c.BeneficiaryMemberId)
                       .Select(g => new MemberEarningsTotal { MemberId = g.Key, Total = g.Sum(c => c.Amount) });
    }
}
```

- [ ] **Step 4: `EarningsRankingService` usa la regla compartida**

En `EarningsRankingService.ComputeAsync`, reemplazar este bloque:

```csharp
        var earnings = _db.CommissionEarnings.AsNoTracking()
            .Where(c => (c.Status == CommissionEarningStatus.Paid || c.Status == CommissionEarningStatus.Pending)
                     && c.EarnedDate < to);
        if (from is not null) earnings = earnings.Where(c => c.EarnedDate >= from);

        var totals = await (
            from t in earnings.GroupBy(c => c.BeneficiaryMemberId)
                              .Select(g => new { MemberId = g.Key, Total = g.Sum(c => c.Amount) })
            join m in _db.MemberProfiles.AsNoTracking() on t.MemberId equals m.MemberId
```

por:

```csharp
        var totals = await (
            from t in EarningsQueries.TotalsByMember(_db, from, to)
            join m in _db.MemberProfiles.AsNoTracking() on t.MemberId equals m.MemberId
```

El resto del método (el `where !m.IsCorporateAccount && t.Total > 0`, la proyección y el orden) no cambia. Si `using MLMConquerorGlobalEdition.Domain.Enums;` queda sin uso en el archivo, quitarlo.

- [ ] **Step 5: Correr las pruebas nuevas y las del ranking**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~EarningsQueriesTests|FullyQualifiedName~EarningsRankingServiceTests|FullyQualifiedName~AdminReportsControllerTests"`
Expected: PASS (las 2 nuevas y todas las del Plan A, sin cambios).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsQueries.cs MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsRankingService.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/EarningsQueriesTests.cs
git commit -m "Reportes: regla de ganancias por miembro en un solo sitio (pagadas y pendientes en una ventana); el ranking de comisiones la usa sin cambiar su resultado"
```

---

### Task 5: Detección nocturna

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.History.cs` (provisional; la Tarea 6 lo reemplaza)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionDetectorTests.cs`

- [ ] **Step 1: Pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using static MLMConquerorGlobalEdition.AdminAPI.Tests.Reports.ClubRecognitionTestData;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Detección nocturna: el día que cerró, quien llega al umbral de un nivel activo (regla de Earnings, solo Ambassadors no
/// cancelados ni corporativos) y no tiene registro de ese nivel recibe uno con Reached = ese día. Nunca borra ni cambia.
/// </summary>
public class ClubRecognitionDetectorTests
{
    private static readonly DateTime Dia   = new(2026, 11, 13);              // el día que cerró
    private static readonly DateTime Ahora = new(2026, 11, 14, 0, 45, 0);

    private static AppDbContext Base()
    {
        var db = Db();
        Niveles(db);
        // AMB-1: 105.000 de por vida; en la ventana de 180 días (2026-05-18 .. 2026-11-13) solo 45.000.
        Miembro(db, "AMB-1");
        Comision(db, "AMB-1", 60_000m, new DateTime(2026, 1, 10));
        Comision(db, "AMB-1", 45_000m, new DateTime(2026, 11, 13, 18, 0, 0), CommissionEarningStatus.Pending);
        // AMB-2: 55.000 en la ventana.
        Miembro(db, "AMB-2");
        Comision(db, "AMB-2", 30_000m, new DateTime(2026, 10, 1));
        Comision(db, "AMB-2", 25_000m, new DateTime(2026, 11, 13));
        // AMB-3: 260.000 en la ventana: 100k Earner, 500k Earner y 100k Lifetime Earner.
        Miembro(db, "AMB-3");
        Comision(db, "AMB-3", 260_000m, new DateTime(2026, 9, 1));
        // No califican: canceladas, cuenta corporativa, Terminated, cliente y una comisión posterior al día que cerró.
        Miembro(db, "AMB-4");
        Comision(db, "AMB-4", 200_000m, new DateTime(2026, 9, 1), CommissionEarningStatus.Cancelled);
        Miembro(db, "AMB-5", corporativa: true);
        Comision(db, "AMB-5", 200_000m, new DateTime(2026, 9, 1));
        Miembro(db, "AMB-6", estado: MemberAccountStatus.Terminated);
        Comision(db, "AMB-6", 200_000m, new DateTime(2026, 9, 1));
        Miembro(db, "CUS-1", tipo: MemberType.ExternalMember);
        Comision(db, "CUS-1", 200_000m, new DateTime(2026, 9, 1));
        Miembro(db, "AMB-7");
        Comision(db, "AMB-7", 120_000m, new DateTime(2026, 11, 14));
        db.SaveChanges();
        return db;
    }

    private static ClubRecognitionDetector Detector(AppDbContext db) => new(db, Reloj(Ahora));

    private static async Task<List<(string, int, decimal)>> Registros(AppDbContext db) =>
        (await db.MemberRecognitions.AsNoTracking().ToListAsync())
            .Select(r => (r.MemberId, r.RecognitionLevelId, r.QualifyingTotal)).ToList();

    [Fact]
    public async Task Registra_a_quien_cruza_cada_nivel_activo_con_el_dia_que_cerro()
    {
        using var db = Base();

        var nuevos = await Detector(db).DetectDayAsync(Dia);

        nuevos.Should().Be(5);
        (await Registros(db)).Should().BeEquivalentTo(new[]
        {
            ("AMB-1", 4, 105_000m),
            ("AMB-2", 1, 55_000m),
            ("AMB-3", 1, 260_000m),
            ("AMB-3", 2, 260_000m),
            ("AMB-3", 4, 260_000m),
        });
        (await db.MemberRecognitions.ToListAsync()).Should().OnlyContain(r =>
            r.ReachedDate == Dia && r.Source == RecognitionSource.Nightly
            && r.CreatedBy == ClubRecognitionDetector.Author && r.CreationDate == Ahora);
    }

    [Fact]
    public async Task Correrlo_dos_veces_el_mismo_dia_no_duplica()
    {
        using var db = Base();

        await Detector(db).DetectDayAsync(Dia);
        var segunda = await Detector(db).DetectDayAsync(Dia);

        segunda.Should().Be(0);
        (await db.MemberRecognitions.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Un_registro_que_ya_existe_no_se_toca_ni_se_pierde_aunque_ya_no_califique()
    {
        using var db = Base();
        Registro(db, 99, "AMB-1", 4, new DateTime(2026, 3, 1), 100_000m);
        Miembro(db, "AMB-8");                                                  // calificó en 2025 y hoy no gana nada
        Registro(db, 98, "AMB-8", 1, new DateTime(2025, 1, 1), 50_000m);
        await db.SaveChangesAsync();

        await Detector(db).DetectDayAsync(Dia);

        var amb1 = await db.MemberRecognitions.Where(r => r.MemberId == "AMB-1").ToListAsync();
        amb1.Should().ContainSingle();
        (amb1[0].Id, amb1[0].ReachedDate, amb1[0].QualifyingTotal).Should().Be((99L, new DateTime(2026, 3, 1), 100_000m));
        (await db.MemberRecognitions.AnyAsync(r => r.Id == 98)).Should().BeTrue();
    }

    [Fact]
    public async Task Un_nivel_inactivo_no_se_detecta()
    {
        using var db = Base();
        (await db.RecognitionLevels.SingleAsync(l => l.Id == 4)).IsActive = false;
        await db.SaveChangesAsync();

        await Detector(db).DetectDayAsync(Dia);

        (await db.MemberRecognitions.AnyAsync(r => r.RecognitionLevelId == 4)).Should().BeFalse();
    }

    [Fact]
    public async Task El_marcador_dice_si_ya_se_hizo_la_carga_historica()
    {
        using var db = Base();
        (await Detector(db).HistoryLoadedAsync()).Should().BeFalse();

        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = ClubRecognitionDetector.HistoryLoadedKey, Value = "2026-11-13", CreatedBy = "seed", CreationDate = Ahora
        });
        await db.SaveChangesAsync();

        (await Detector(db).HistoryLoadedAsync()).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~ClubRecognitionDetectorTests"`
Expected: FAIL de compilación («ClubRecognitionDetector no existe»).

- [ ] **Step 3: Implementar el detector (nocturno y marcador)**

`MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IClubRecognitionDetector
{
    /// <summary>¿Ya se hizo la carga histórica (existe el marcador en GlobalParameters)?</summary>
    Task<bool> HistoryLoadedAsync(CancellationToken ct = default);

    /// <summary>Carga histórica hasta <paramref name="lastDay"/> inclusive. Devuelve los registros nuevos.</summary>
    Task<int> LoadHistoryAsync(DateTime lastDay, CancellationToken ct = default);

    /// <summary>Detección del día que cerró (<paramref name="day"/>). Devuelve los registros nuevos.</summary>
    Task<int> DetectDayAsync(DateTime day, CancellationToken ct = default);
}

/// <summary>
/// Detección de Club Recognition. Califica quien, con la regla de ganancias de los reportes (<see cref="EarningsQueries"/>),
/// llega al umbral de un nivel activo dentro de su ventana (WindowDays; null = de por vida) y es Ambassador, no
/// Terminated y no cuenta corporativa (estado de hoy). Crea un registro por Ambassador y nivel y nunca borra ni cambia
/// uno existente: es idempotente contra el índice único UX_MemberRecognitions_Member_Level.
/// <para>Nocturno (<see cref="DetectDayAsync"/>): un agregado por ventana distinta; Reached = el día que cerró.</para>
/// <para>Histórico (<see cref="LoadHistoryAsync"/>, en ClubRecognitionDetector.History.cs): una sentencia SQL por ventana
/// distinta; Reached = el día en que se cruzó el umbral; deja el marcador <see cref="HistoryLoadedKey"/>.</para>
/// </summary>
public partial class ClubRecognitionDetector : IClubRecognitionDetector
{
    public const string Author           = "club-recognition";
    public const string HistoryLoadedKey = "ClubRecognition.HistoryLoadedThrough";

    // Recorre CommissionEarnings entero: el tiempo de comando por defecto (30 s) no basta a escala.
    private static readonly TimeSpan LongCommand = TimeSpan.FromMinutes(15);

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public ClubRecognitionDetector(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    /// <summary>Ambassadors que pueden calificar hoy.</summary>
    public static IQueryable<MemberProfile> EligibleMembers(AppDbContext db) =>
        db.MemberProfiles.AsNoTracking().Where(m => m.MemberType == MemberType.Ambassador
                                                 && m.Status != MemberAccountStatus.Terminated
                                                 && !m.IsCorporateAccount);

    /// <summary>
    /// Totales de [<paramref name="from"/>, <paramref name="to"/>) que llegan a <paramref name="minimum"/>, solo de Ambassadors
    /// elegibles. Expuesta para comprobar su traducción a SQL y medirla.
    /// </summary>
    public static IQueryable<MemberEarningsTotal> QualifiedTotals(AppDbContext db, DateTime? from, DateTime to, decimal minimum) =>
        from t in EarningsQueries.TotalsByMember(db, from, to)
        where t.Total >= minimum
        join m in EligibleMembers(db) on t.MemberId equals m.MemberId
        select t;

    public Task<bool> HistoryLoadedAsync(CancellationToken ct = default) =>
        _db.GlobalParameters.AsNoTracking().AnyAsync(p => p.Key == HistoryLoadedKey, ct);

    public async Task<int> DetectDayAsync(DateTime day, CancellationToken ct = default)
    {
        var closed = day.Date;
        var to     = closed.AddDays(1);                                   // el día que cerró entra entero
        var levels = await _db.RecognitionLevels.AsNoTracking().Where(l => l.IsActive).ToListAsync(ct);
        if (levels.Count == 0) return 0;

        var existing = (await _db.MemberRecognitions.AsNoTracking()
                .Select(r => new { r.MemberId, r.RecognitionLevelId })
                .ToListAsync(ct))
            .Select(r => Key(r.MemberId, r.RecognitionLevelId))
            .ToHashSet();

        if (_db.Database.IsRelational()) _db.Database.SetCommandTimeout(LongCommand);

        var now   = _clock.Now;
        var added = 0;
        foreach (var window in levels.GroupBy(l => l.WindowDays))
        {
            // Ventana de N días que cierra el día D: (D - N, D], como Earnings 180 Days.
            DateTime? from = window.Key is int days ? to.AddDays(-days) : null;
            var qualified  = await QualifiedTotals(_db, from, to, window.Min(l => l.Threshold)).ToListAsync(ct);

            foreach (var total in qualified)
            foreach (var level in window.Where(l => total.Total >= l.Threshold))
            {
                if (!existing.Add(Key(total.MemberId, level.Id))) continue;
                _db.MemberRecognitions.Add(new MemberRecognition
                {
                    MemberId           = total.MemberId,
                    RecognitionLevelId = level.Id,
                    ReachedDate        = closed,
                    QualifyingTotal    = total.Total,
                    Source             = RecognitionSource.Nightly,
                    CreationDate       = now,
                    CreatedBy          = Author
                });
                added++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return added;
    }

    // SQL Server compara MemberId sin mayúsculas (intercalación CI): la clave en memoria hace lo mismo.
    private static string Key(string memberId, int levelId) => $"{memberId.Trim().ToUpperInvariant()}|{levelId}";
}
```

La clase es `partial`: la carga histórica vive en `ClubRecognitionDetector.History.cs` (Tarea 6). Para que esta tarea compile sola, crear ya ese archivo con la versión provisional:

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public partial class ClubRecognitionDetector
{
    public Task<int> LoadHistoryAsync(DateTime lastDay, CancellationToken ct = default) =>
        throw new NotImplementedException("Carga historica: Tarea 6 del plan C.");
}
```

- [ ] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~ClubRecognitionDetectorTests"`
Expected: PASS (5).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.cs MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.History.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionDetectorTests.cs
git commit -m "Reportes: deteccion nocturna de Club Recognition (un agregado por ventana, solo Ambassadors no cancelados ni corporativos, Reached el dia que cerro, idempotente y sin borrar)"
```

---

### Task 6: Carga histórica (SQL por conjuntos), pruebas contra SQL Server y medición

**Files:**
- Replace: `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.History.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerClubRecognitionTests.cs`

La carga histórica no se puede probar con EF InMemory (es SQL de SQL Server): sus pruebas son `[SqlServerFact]` contra la base local migrada (Tarea 1), dentro de una transacción que se deshace, como las de los Planes A y B.

- [ ] **Step 1: Pruebas que fallan**

`MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerClubRecognitionTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using Xunit.Abstractions;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Club Recognition contra SQL Server: la carga histórica fecha cada registro el día en que se cruzó el umbral (de por vida:
/// el día en que el acumulado llegó; ventana de 180 días: el primer día en que la ventana móvil llegó), respeta lo que ya
/// existe y es idempotente; la detección nocturna se traduce y fecha el día que cerró. Usa los niveles sembrados por la
/// migración (1 = 50.000 en 180 días, 4 = 100.000 de por vida). La última prueba escribe el SQL nocturno para medirlo.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerClubRecognitionTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 0, 45, 0);

    private readonly ITestOutputHelper _salida;

    public SqlServerClubRecognitionTests(ITestOutputHelper salida) => _salida = salida;

    private static ClubRecognitionDetector Detector(AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        return new ClubRecognitionDetector(db, reloj.Object);
    }

    private static void Comision(AppDbContext db, string memberId, decimal monto, DateTime fecha,
        CommissionEarningStatus estado = CommissionEarningStatus.Paid) =>
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = memberId, Amount = monto, EarnedDate = fecha, PaymentDate = fecha, Status = estado,
            CommissionTypeId = 1, CreatedBy = "prueba", CreationDate = fecha, LastUpdateDate = fecha
        });

    [SqlServerFact]
    public Task La_carga_historica_fecha_el_dia_en_que_se_cruzo_el_umbral() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        string a = SqlServerReportes.NuevoId("CA"), b = SqlServerReportes.NuevoId("CB"), c = SqlServerReportes.NuevoId("CC"),
               d = SqlServerReportes.NuevoId("CD"), e = SqlServerReportes.NuevoId("CE"), f = SqlServerReportes.NuevoId("CF"),
               g = SqlServerReportes.NuevoId("CG"), h = SqlServerReportes.NuevoId("CH");

        // A: 60.000 el 10-ene (cruza 50k/180d ese día) y 50.000 el 5-mar (el acumulado llega a 110.000: cruza 100k de por vida).
        ReportesDb.Miembro(db, a);
        Comision(db, a, 60_000m, new DateTime(2025, 1, 10, 9, 0, 0));
        Comision(db, a, 50_000m, new DateTime(2025, 3, 5, 15, 0, 0), CommissionEarningStatus.Pending);
        // B: 30.000 el 1-ene sale de la ventana el 30-jun; el 1-jul la ventana tiene 30.000; el 1-ago, 55.000.
        ReportesDb.Miembro(db, b);
        Comision(db, b, 30_000m, new DateTime(2025, 1, 1));
        Comision(db, b, 30_000m, new DateTime(2025, 7, 1));
        Comision(db, b, 25_000m, new DateTime(2025, 8, 1));
        // G: dos comisiones el mismo día suman 60.000 ese día.
        ReportesDb.Miembro(db, g);
        Comision(db, g, 40_000m, new DateTime(2025, 4, 1, 8, 0, 0));
        Comision(db, g, 20_000m, new DateTime(2025, 4, 1, 20, 0, 0));
        // H: ya tiene registro del nivel 1 (nocturno, 9-sep): la carga no lo toca.
        ReportesDb.Miembro(db, h);
        Comision(db, h, 60_000m, new DateTime(2025, 2, 1));
        db.MemberRecognitions.Add(new MemberRecognition
        {
            MemberId = h, RecognitionLevelId = 1, ReachedDate = new DateTime(2025, 9, 9), QualifyingTotal = 60_000m,
            Source = RecognitionSource.Nightly, CreatedBy = "prueba", CreationDate = Ahora
        });
        // No califican: canceladas, cuenta corporativa, Terminated y una comisión posterior al último día.
        ReportesDb.Miembro(db, c);
        Comision(db, c, 120_000m, new DateTime(2025, 2, 1), CommissionEarningStatus.Cancelled);
        ReportesDb.Miembro(db, d).IsCorporateAccount = true;
        Comision(db, d, 120_000m, new DateTime(2025, 2, 1));
        ReportesDb.Miembro(db, e, estado: MemberAccountStatus.Terminated);
        Comision(db, e, 120_000m, new DateTime(2025, 2, 1));
        ReportesDb.Miembro(db, f);
        Comision(db, f, 120_000m, new DateTime(2026, 1, 5));
        await db.SaveChangesAsync();
        var detector = Detector(db);

        var primera = await detector.LoadHistoryAsync(new DateTime(2025, 12, 31));
        var segunda = await detector.LoadHistoryAsync(new DateTime(2025, 12, 31));

        var ids = new[] { a, b, c, d, e, f, g, h };
        var registros = await db.MemberRecognitions.AsNoTracking().Where(r => ids.Contains(r.MemberId)).ToListAsync();
        registros.Select(r => (r.MemberId, r.RecognitionLevelId, r.ReachedDate, r.QualifyingTotal)).Should().BeEquivalentTo(new[]
        {
            (a, 1, new DateTime(2025, 1, 10), 60_000m),
            (a, 4, new DateTime(2025, 3, 5), 110_000m),
            (b, 1, new DateTime(2025, 8, 1), 55_000m),
            (g, 1, new DateTime(2025, 4, 1), 60_000m),
            (h, 1, new DateTime(2025, 9, 9), 60_000m),
        });
        registros.Where(r => r.MemberId != h).Should().OnlyContain(r =>
            r.Source == RecognitionSource.Historical && r.CreatedBy == ClubRecognitionDetector.Author && r.CreationDate == Ahora);
        primera.Should().BeGreaterOrEqualTo(4);          // la base local puede tener más Ambassadors que califiquen
        segunda.Should().Be(0);
        (await detector.HistoryLoadedAsync()).Should().BeTrue();
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task La_deteccion_nocturna_se_traduce_y_fecha_el_dia_que_cerro() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var n = SqlServerReportes.NuevoId("CN");
        ReportesDb.Miembro(db, n);
        Comision(db, n, 120_000m, new DateTime(2026, 11, 13, 9, 0, 0));
        await db.SaveChangesAsync();

        await Detector(db).DetectDayAsync(new DateTime(2026, 11, 13));

        var registros = await db.MemberRecognitions.AsNoTracking().Where(r => r.MemberId == n).ToListAsync();
        registros.Select(r => (r.RecognitionLevelId, r.ReachedDate, r.QualifyingTotal, r.Source)).Should().BeEquivalentTo(new[]
        {
            (1, new DateTime(2026, 11, 13), 120_000m, RecognitionSource.Nightly),
            (4, new DateTime(2026, 11, 13), 120_000m, RecognitionSource.Nightly),
        });
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public void La_consulta_nocturna_se_traduce_a_SQL()
    {
        using var db = SqlServerReportes.Db();
        var hasta = new DateTime(2026, 11, 14);
        foreach (var ventana in new int?[] { 180, null })
        {
            DateTime? desde = ventana is int dias ? hasta.AddDays(-dias) : null;
            var sql = ClubRecognitionDetector.QualifiedTotals(db, desde, hasta, 50_000m).ToQueryString();
            sql.Should().Contain("[MemberProfiles]");
            _salida.WriteLine($"-- ventana {ventana?.ToString() ?? "de por vida"}{Environment.NewLine}{sql}{Environment.NewLine}");
        }
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~SqlServerClubRecognitionTests"`
Expected: `La_carga_historica_...` FAIL con `NotImplementedException` («Carga historica: Tarea 6 del plan C.»); las otras dos PASS. Si las tres salen SKIPPED, no hay SQL Server local o la base no está migrada: aplicar la migración de la Tarea 1 (Step 7) y repetir; sin SQL Server no se puede cerrar esta tarea.

- [ ] **Step 3: Implementar la carga histórica**

Reemplazar todo `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.History.cs` por:

```csharp
using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public partial class ClubRecognitionDetector
{
    /// <summary>
    /// Carga histórica de UNA ventana (@window; NULL = niveles de por vida), por conjuntos y en una sola lectura de
    /// CommissionEarnings:
    /// <list type="number">
    /// <item>Daily: ganancias por miembro y día (Paid + Pending, sin borradas, hasta @lastDay inclusive).</item>
    /// <item>Steps: cada día suma su monto y, con ventana, lo resta @window días después (el día en que sale de la
    /// ventana (D - @window, D]). Sin ventana, la segunda fila es NULL y se descarta.</item>
    /// <item>Totals: la suma acumulada de esos cambios por miembro = total de la ventana (o acumulado de por vida) en cada
    /// día en que cambia. Entre esos días no cambia, así que el primer día en que llega al umbral es uno de ellos.</item>
    /// <item>Crossed: por miembro y nivel activo de esa ventana, el primer día con total >= umbral.</item>
    /// </list>
    /// Inserta solo a Ambassadors no Terminated ni corporativos que aún no tienen registro de ese nivel (NOT EXISTS).
    /// SQL Server no admite RANGE con intervalo de fechas en las funciones de ventana: por eso la ventana móvil se
    /// resuelve con entradas y salidas. Expuesta para medirla (Tarea 6 del plan C) y para revisarla.
    /// </summary>
    public const string HistoricalSql = """
        WITH Daily AS (
            SELECT c.[BeneficiaryMemberId] AS MemberId, CAST(c.[EarnedDate] AS date) AS [Day], SUM(c.[Amount]) AS Amount
            FROM [CommissionEarnings] c
            WHERE c.[IsDeleted] = 0 AND c.[Status] IN (@paid, @pending) AND c.[EarnedDate] < @to
            GROUP BY c.[BeneficiaryMemberId], CAST(c.[EarnedDate] AS date)
        ), Steps AS (
            SELECT d.MemberId, e.[Day], SUM(e.Delta) AS Delta
            FROM Daily d
            CROSS APPLY (VALUES (d.[Day], d.Amount), (DATEADD(day, @window, d.[Day]), -d.Amount)) e([Day], Delta)
            WHERE e.[Day] IS NOT NULL AND e.[Day] <= @lastDay
            GROUP BY d.MemberId, e.[Day]
        ), Totals AS (
            SELECT MemberId, [Day], SUM(Delta) OVER (PARTITION BY MemberId ORDER BY [Day] ROWS UNBOUNDED PRECEDING) AS Total
            FROM Steps
        ), Crossed AS (
            SELECT t.MemberId, l.[Id] AS LevelId, t.[Day], t.Total,
                   ROW_NUMBER() OVER (PARTITION BY t.MemberId, l.[Id] ORDER BY t.[Day]) AS Rn
            FROM Totals t
            JOIN [RecognitionLevels] l
              ON l.[IsActive] = 1
             AND ((@window IS NULL AND l.[WindowDays] IS NULL) OR l.[WindowDays] = @window)
             AND t.Total >= l.[Threshold]
        )
        INSERT INTO [MemberRecognitions]
            ([MemberId], [RecognitionLevelId], [ReachedDate], [QualifyingTotal], [Source], [CreationDate], [CreatedBy])
        SELECT x.MemberId, x.LevelId, x.[Day], x.Total, @source, @now, @author
        FROM Crossed x
        JOIN [MemberProfiles] m ON m.[MemberId] = x.MemberId
        WHERE x.Rn = 1
          AND m.[IsDeleted] = 0 AND m.[MemberType] = @ambassador AND m.[Status] <> @terminated AND m.[IsCorporateAccount] = 0
          AND NOT EXISTS (SELECT 1 FROM [MemberRecognitions] r
                          WHERE r.[MemberId] = x.MemberId AND r.[RecognitionLevelId] = x.LevelId);
        """;

    public async Task<int> LoadHistoryAsync(DateTime lastDay, CancellationToken ct = default)
    {
        var day     = lastDay.Date;
        var windows = await _db.RecognitionLevels.AsNoTracking()
            .Where(l => l.IsActive).Select(l => l.WindowDays).Distinct().ToListAsync(ct);

        var previous = _db.Database.GetCommandTimeout();
        _db.Database.SetCommandTimeout(LongCommand);
        var added = 0;
        try
        {
            foreach (var window in windows)
                added += await _db.Database.ExecuteSqlRawAsync(HistoricalSql, HistoryParameters(day, window), ct);
        }
        finally
        {
            _db.Database.SetCommandTimeout(previous);
        }

        await MarkHistoryLoadedAsync(day, ct);
        return added;
    }

    private object[] HistoryParameters(DateTime day, int? window) => new object[]
    {
        new SqlParameter("@lastDay",    SqlDbType.Date)          { Value = day },
        new SqlParameter("@to",         SqlDbType.DateTime2)     { Value = day.AddDays(1) },
        new SqlParameter("@window",     SqlDbType.Int)           { Value = (object?)window ?? DBNull.Value },
        new SqlParameter("@paid",       SqlDbType.Int)           { Value = (int)CommissionEarningStatus.Paid },
        new SqlParameter("@pending",    SqlDbType.Int)           { Value = (int)CommissionEarningStatus.Pending },
        new SqlParameter("@ambassador", SqlDbType.Int)           { Value = (int)MemberType.Ambassador },
        new SqlParameter("@terminated", SqlDbType.Int)           { Value = (int)MemberAccountStatus.Terminated },
        new SqlParameter("@source",     SqlDbType.Int)           { Value = (int)RecognitionSource.Historical },
        new SqlParameter("@now",        SqlDbType.DateTime2)     { Value = _clock.Now },
        new SqlParameter("@author",     SqlDbType.NVarChar, 100) { Value = Author },
    };

    /// <summary>Deja (o actualiza) el marcador con el último día cargado: el job nocturno deja de hacer la carga inicial.</summary>
    private async Task MarkHistoryLoadedAsync(DateTime day, CancellationToken ct)
    {
        var now    = _clock.Now;
        var marker = await _db.GlobalParameters.FirstOrDefaultAsync(p => p.Key == HistoryLoadedKey, ct);
        if (marker is null)
        {
            marker = new GlobalParameter
            {
                Key          = HistoryLoadedKey,
                Description  = "Club Recognition: last day covered by the historical load. Delete it to make the nightly job run the historical load again.",
                CreationDate = now,
                CreatedBy    = Author
            };
            _db.GlobalParameters.Add(marker);
        }
        else
        {
            marker.LastUpdateDate = now;
            marker.LastUpdateBy   = Author;
        }
        marker.Value = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~SqlServerClubRecognitionTests" --logger "console;verbosity=detailed"`
Expected: PASS (3). Si `La_carga_historica_...` falla por nombre de columna o tabla, el SQL quedó desalineado con el modelo de la Tarea 1: corregir el SQL, no el modelo. Copiar de la salida el SQL de `La_consulta_nocturna_se_traduce_a_SQL` para el Step 5.
Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~ClubRecognitionDetectorTests"`
Expected: PASS (5), sin cambios.

- [ ] **Step 5: Medir contra la base de escala (solo lectura)**

La base de escala no tiene las tablas nuevas, ni `IsCorporateAccount`, ni el índice `IX_CommissionEarnings_EarnedDate_Beneficiary` (recorre el índice agrupado: la medida es una cota alta), y su mayor acumulado de por vida es 18.875, así que con los umbrales reales no califica nadie. Se mide la misma consulta con un `SELECT` en lugar del `INSERT`, los niveles en una tabla `VALUES` con umbrales bajados y sin la condición de cuenta corporativa.

1. Escribir fuera del repo `%TEMP%\club-historico.sql` (con `DECLARE @window int = 180;`) y `%TEMP%\club-historico-vida.sql` (igual con `DECLARE @window int = NULL;`). El cuerpo de `Daily`, `Steps`, `Totals` y `Crossed` es el de `HistoricalSql` sin cambios, salvo `[RecognitionLevels] l` → `Levels l` y los parámetros declarados arriba:

```sql
SET NOCOUNT ON; SET STATISTICS TIME, IO ON;
DECLARE @lastDay date = CAST(DATEADD(day, -1, GETDATE()) AS date);
DECLARE @to datetime2 = DATEADD(day, 1, CAST(@lastDay AS datetime2));
DECLARE @window int = 180;
DECLARE @paid int = 2, @pending int = 1;
WITH Levels AS (
    SELECT * FROM (VALUES (1, CAST(500 AS decimal(18,4)), 180, 1), (2, 1500, 180, 1), (3, 3000, 180, 1),
                          (4, 1000, NULL, 1), (5, 2000, NULL, 1), (6, 4000, NULL, 1)) v([Id], [Threshold], [WindowDays], [IsActive])
), Daily AS (
    SELECT c.[BeneficiaryMemberId] AS MemberId, CAST(c.[EarnedDate] AS date) AS [Day], SUM(c.[Amount]) AS Amount
    FROM [CommissionEarnings] c
    WHERE c.[IsDeleted] = 0 AND c.[Status] IN (@paid, @pending) AND c.[EarnedDate] < @to
    GROUP BY c.[BeneficiaryMemberId], CAST(c.[EarnedDate] AS date)
), Steps AS (
    SELECT d.MemberId, e.[Day], SUM(e.Delta) AS Delta
    FROM Daily d
    CROSS APPLY (VALUES (d.[Day], d.Amount), (DATEADD(day, @window, d.[Day]), -d.Amount)) e([Day], Delta)
    WHERE e.[Day] IS NOT NULL AND e.[Day] <= @lastDay
    GROUP BY d.MemberId, e.[Day]
), Totals AS (
    SELECT MemberId, [Day], SUM(Delta) OVER (PARTITION BY MemberId ORDER BY [Day] ROWS UNBOUNDED PRECEDING) AS Total
    FROM Steps
), Crossed AS (
    SELECT t.MemberId, l.[Id] AS LevelId, t.[Day], t.Total,
           ROW_NUMBER() OVER (PARTITION BY t.MemberId, l.[Id] ORDER BY t.[Day]) AS Rn
    FROM Totals t
    JOIN Levels l
      ON l.[IsActive] = 1
     AND ((@window IS NULL AND l.[WindowDays] IS NULL) OR l.[WindowDays] = @window)
     AND t.Total >= l.[Threshold]
)
SELECT x.LevelId, COUNT(*) AS Members, MIN(x.[Day]) AS FirstDay, MAX(x.[Day]) AS LastDay
FROM Crossed x
JOIN [MemberProfiles] m ON m.[MemberId] = x.MemberId
WHERE x.Rn = 1 AND m.[IsDeleted] = 0 AND m.[MemberType] = 0 AND m.[Status] <> 4
GROUP BY x.LevelId;
```

2. Escribir `%TEMP%\club-nocturno.sql` con el SQL de `La_consulta_nocturna_se_traduce_a_SQL` (Step 4) para cada ventana: anteponer `SET NOCOUNT ON; SET STATISTICS TIME, IO ON;`, declarar sus parámetros con los valores que imprime EF (`@__to_0` = mañana a las 00:00, `@__from_1` = 180 días antes, `@__minimum_2` = 500), quitar la condición `[IsCorporateAccount]` y envolver el SELECT en `SELECT COUNT(*) FROM ( ... ) q;`.
3. Run (tres veces cada uno; `-I` es obligatorio):
   `sqlcmd -S . -E -C -I -d MLMConquerorGlobalEditionDb_Escala -i "%TEMP%\club-historico.sql"`
   `sqlcmd -S . -E -C -I -d MLMConquerorGlobalEditionDb_Escala -i "%TEMP%\club-historico-vida.sql"`
   `sqlcmd -S . -E -C -I -d MLMConquerorGlobalEditionDb_Escala -i "%TEMP%\club-nocturno.sql"`
4. Anotar en «Medidas» (final de este plan): filas por nivel, lecturas lógicas, CPU y tiempo de cada corrida.

Expected: la carga histórica < 60 s por ventana (se hace una vez; la preparación del plan midió ~14-15 s) y el nocturno < 30 s en total (~6-13 s por ventana). Una sola exploración de `CommissionEarnings` por consulta (Scan count = grado de paralelismo, no 2×). Si la histórica pasa de 60 s, anotarlo y avisar a Nick: el tiempo de comando es de 15 minutos, pero el botón manual del admin espera la respuesta en el navegador.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionDetector.History.cs MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerClubRecognitionTests.cs docs/superpowers/plans/2026-11-14-reportes-admin-C-club-recognition.md
git commit -m "Reportes: carga historica de Club Recognition por conjuntos (ventana movil con entradas y salidas, primer dia que se cruza el umbral, idempotente y con marcador), pruebas contra SQL Server y medidas en la base de escala"
```

---

### Task 7: Job nocturno en el BizCenter y carga histórica manual

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter/Jobs/ClubRecognitionJob.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs` (registro junto a `MemberCountrySnapshotJob`, línea ~267, y `RecurringJob` junto a `member-country-snapshot`, línea ~453)
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionDetectionController.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs` (junto al registro de `IRankReportService`, línea ~111)
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/ClubRecognitionJobTests.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminClubRecognitionDetectionControllerTests.cs`

- [ ] **Step 1: Pruebas que fallan del job**

`MLMConquerorGlobalEdition.BizCenter.Tests/ClubRecognitionJobTests.cs`:

```csharp
using System.Globalization;
using System.Reflection;
using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Jobs;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El job evalúa el día que cerró en hora del servidor. La primera vez (sin marcador) hace la carga histórica hasta ese
/// día; después, la detección nocturna. Nunca las dos en la misma corrida.
/// </summary>
public class ClubRecognitionJobTests
{
    private static (ClubRecognitionJob Job, Mock<IClubRecognitionDetector> Detector) Montar(string ahora, bool cargada)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(DateTime.Parse(ahora, CultureInfo.InvariantCulture));
        var detector = new Mock<IClubRecognitionDetector>();
        detector.Setup(d => d.HistoryLoadedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cargada);
        return (new ClubRecognitionJob(detector.Object, reloj.Object, NullLogger<ClubRecognitionJob>.Instance), detector);
    }

    [Theory]
    [InlineData("2026-11-14T00:45:00", "2026-11-13")]   // invierno: 05:45 UTC = 00:45 local
    [InlineData("2026-07-14T01:45:00", "2026-07-13")]   // verano: 05:45 UTC = 01:45 local
    [InlineData("2026-12-01T00:45:00", "2026-11-30")]   // cambio de mes
    public async Task Con_la_carga_hecha_detecta_el_dia_que_cerro(string ahora, string esperado)
    {
        var (job, detector) = Montar(ahora, cargada: true);
        var dia = DateTime.Parse(esperado, CultureInfo.InvariantCulture);
        detector.Setup(d => d.DetectDayAsync(dia, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        await job.ExecuteAsync(CancellationToken.None);

        detector.Verify(d => d.DetectDayAsync(dia, It.IsAny<CancellationToken>()), Times.Once);
        detector.Verify(d => d.LoadHistoryAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_la_carga_hecha_hace_la_historica_hasta_el_dia_que_cerro_y_no_la_nocturna()
    {
        var (job, detector) = Montar("2026-11-14T00:45:00", cargada: false);
        detector.Setup(d => d.LoadHistoryAsync(new DateTime(2026, 11, 13), It.IsAny<CancellationToken>())).ReturnsAsync(812);

        await job.ExecuteAsync(CancellationToken.None);

        detector.Verify(d => d.LoadHistoryAsync(new DateTime(2026, 11, 13), It.IsAny<CancellationToken>()), Times.Once);
        detector.Verify(d => d.DetectDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Va_en_la_cola_del_bizcenter()
        => typeof(ClubRecognitionJob).GetCustomAttribute<QueueAttribute>()!.Queue.Should().Be("bizcenter");
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~ClubRecognitionJobTests"`
Expected: FAIL de compilación («ClubRecognitionJob no existe»).

- [ ] **Step 3: Implementar el job**

`MLMConquerorGlobalEdition.BizCenter/Jobs/ClubRecognitionJob.cs`:

```csharp
using Hangfire;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// Club Recognition: cada noche evalúa el día que acaba de cerrar (ayer en hora del servidor, IDateTimeProvider.Now). La
/// primera vez, sin el marcador ClubRecognition.HistoryLoadedThrough, hace la carga histórica hasta ese día (Reached = día
/// en que se cruzó el umbral); a partir de ahí, la detección nocturna (Reached = el día que cerró). Se programa a las
/// 05:45 UTC (00:45 en invierno, 01:45 en verano), 15 minutos después de la foto de países para no recorrer dos tablas
/// grandes a la vez. Relanzar: panel de Hangfire del BizCenter (/hangfire) -> Recurring jobs -> club-recognition ->
/// Trigger now. Es idempotente.
/// </summary>
[Queue("bizcenter")]
public class ClubRecognitionJob
{
    private readonly IClubRecognitionDetector    _detector;
    private readonly IDateTimeProvider           _clock;
    private readonly ILogger<ClubRecognitionJob> _logger;

    public ClubRecognitionJob(IClubRecognitionDetector detector, IDateTimeProvider clock, ILogger<ClubRecognitionJob> logger)
    {
        _detector = detector;
        _clock    = clock;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var day = _clock.Now.Date.AddDays(-1);

        if (!await _detector.HistoryLoadedAsync(ct))
        {
            var loaded = await _detector.LoadHistoryAsync(day, ct);
            _logger.LogInformation("ClubRecognitionJob: carga historica hasta {Day:yyyy-MM-dd}, {Rows} registros nuevos.", day, loaded);
            return;
        }

        var added = await _detector.DetectDayAsync(day, ct);
        _logger.LogInformation("ClubRecognitionJob: {Rows} registros nuevos para {Day:yyyy-MM-dd}.", added, day);
    }
}
```

- [ ] **Step 4: Registro y programación en el BizCenter**

En `MLMConquerorGlobalEdition.BizCenter/Program.cs`, justo después de `builder.Services.AddScoped<MemberCountrySnapshotJob>();`:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Reports.IClubRecognitionDetector,
                            MLMConquerorGlobalEdition.Repository.Services.Reports.ClubRecognitionDetector>();
builder.Services.AddScoped<ClubRecognitionJob>();
```

Y justo después del `RecurringJob.AddOrUpdate<MemberCountrySnapshotJob>(...)` (el bloque que termina en `new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "bizcenter" });`):

```csharp

// Club Recognition: dia que cerro (la primera vez, la carga historica). 05:45 UTC = 00:45 en invierno y 01:45 en verano,
// 15 minutos despues de la foto de paises; el dia lo decide IDateTimeProvider.Now, no esta hora.
RecurringJob.AddOrUpdate<ClubRecognitionJob>(
    "club-recognition",
    job => job.ExecuteAsync(CancellationToken.None),
    "45 5 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "bizcenter" });
```

- [ ] **Step 5: Correr las pruebas del job**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~ClubRecognitionJobTests"`
Expected: PASS (5).

- [ ] **Step 6: Pruebas que fallan de la carga manual**

`MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminClubRecognitionDetectionControllerTests.cs`:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminClubRecognitionDetectionControllerTests
{
    [Fact]
    public async Task La_carga_manual_llega_hasta_ayer_en_hora_del_servidor()
    {
        var detector = new Mock<IClubRecognitionDetector>();
        detector.Setup(d => d.LoadHistoryAsync(new DateTime(2026, 11, 13), It.IsAny<CancellationToken>())).ReturnsAsync(42);
        var c = new AdminClubRecognitionDetectionController(detector.Object,
            ClubRecognitionTestData.Reloj(new DateTime(2026, 11, 14, 9, 30, 0)));

        var r = await c.LoadHistory(default);

        var datos = ((ApiResponse<AdminClubRecognitionDetectionController.HistoryResult>)
            r.Should().BeOfType<OkObjectResult>().Subject.Value!).Data!;
        datos.Should().Be(new AdminClubRecognitionDetectionController.HistoryResult(new DateTime(2026, 11, 13), 42));
    }

    [Fact]
    public void Solo_SuperAdmin()
        => typeof(AdminClubRecognitionDetectionController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin");
}
```

- [ ] **Step 7: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminClubRecognitionDetectionControllerTests"`
Expected: FAIL de compilación («AdminClubRecognitionDetectionController no existe»).

- [ ] **Step 8: Implementar el controlador**

`MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionDetectionController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// POST api/v1/admin/club-recognition/detection/history — relanza la carga histórica de Club Recognition hasta ayer (hora
/// del servidor). Solo añade lo que falta, con el día real en que se cruzó cada umbral; no cambia ni borra registros.
/// Sirve tras crear un nivel o cambiar un umbral. Solo SuperAdmin: recorre todas las comisiones.
/// </summary>
[ApiController]
[Route("api/v1/admin/club-recognition/detection")]
[Authorize(Roles = "SuperAdmin")]
public class AdminClubRecognitionDetectionController : ControllerBase
{
    private readonly IClubRecognitionDetector _detector;
    private readonly IDateTimeProvider        _clock;

    public AdminClubRecognitionDetectionController(IClubRecognitionDetector detector, IDateTimeProvider clock)
    {
        _detector = detector;
        _clock    = clock;
    }

    public sealed record HistoryResult(DateTime LastDay, int Added);

    [HttpPost("history")]
    public async Task<IActionResult> LoadHistory(CancellationToken ct)
    {
        var lastDay = _clock.Now.Date.AddDays(-1);
        // Sin el token de la petición: si el navegador deja de esperar, la carga termina igual y no queda a medias.
        var added = await _detector.LoadHistoryAsync(lastDay, CancellationToken.None);
        return Ok(ApiResponse<HistoryResult>.Ok(new HistoryResult(lastDay, added)));
    }
}
```

- [ ] **Step 9: Registro en el AdminAPI**

En `MLMConquerorGlobalEdition.AdminAPI/Program.cs`, justo después del registro de `IRankReportService` (`...RankReportService>();`):

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Reports.IClubRecognitionDetector,
                            MLMConquerorGlobalEdition.Repository.Services.Reports.ClubRecognitionDetector>();
```

- [ ] **Step 10: Correr pruebas y compilar**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminClubRecognitionDetectionControllerTests"`
Expected: PASS (2).
Run: `dotnet build MLMConquerorGlobalEdition.BizCenter` y `dotnet build MLMConquerorGlobalEdition.AdminAPI`
Expected: Build succeeded en los dos.

- [ ] **Step 11: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter/Jobs/ClubRecognitionJob.cs MLMConquerorGlobalEdition.BizCenter/Program.cs MLMConquerorGlobalEdition.BizCenter.Tests/ClubRecognitionJobTests.cs MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionDetectionController.cs MLMConquerorGlobalEdition.AdminAPI/Program.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminClubRecognitionDetectionControllerTests.cs
git commit -m "BizCenter: job nocturno de Club Recognition (carga historica la primera vez, despues el dia que cerro; 05:45 UTC en la cola bizcenter) y carga historica manual solo para SuperAdmin"
```

---

### Task 8: Servicio de lectura (vistas y registros de un miembro)

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionViews.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionReportService.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs` (junto al registro de `IClubRecognitionDetector`)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionReportServiceTests.cs`

Una sola fila (`ClubRecognitionRowView`) sirve a las cinco vistas, a la exportación y a la pestaña del perfil; los textos de las columnas «CEO Call Recognized» y «Event Recognized» salen de un solo sitio (`RecognitionText`).

- [ ] **Step 1: Pruebas que fallan**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using static MLMConquerorGlobalEdition.AdminAPI.Tests.Reports.ClubRecognitionTestData;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Vistas de Club Recognition: filtro por vista y nivel, búsqueda por nombre, email o país sin mayúsculas, orden por
/// Reached de la más reciente, sin cuentas corporativas; los textos de CEO Call y evento; los registros de un miembro.
/// </summary>
public class ClubRecognitionReportServiceTests
{
    private static AppDbContext Base()
    {
        var db = Db();
        Niveles(db);
        Opciones(db);
        Evento(db, "EVT-1", "Bootcamp CIS 2026", new DateTime(2026, 3, 10));
        Miembro(db, "AMB-1", "Ana", "Lopez", "ana@example.com", "Bulgaria", "ana");
        Miembro(db, "AMB-2", "Beto", "Diaz", "beto@example.com", "France", "beto");
        Miembro(db, "AMB-3", "Carla", "Ruiz", "carla@example.com", "Japan", null);
        Miembro(db, "AMB-4", "MWR", "Corp.", "info@example.com", "United States", "corp", corporativa: true);

        var r1 = Registro(db, 1, "AMB-1", 1, new DateTime(2026, 11, 13), 55_000m, RecognitionSource.Nightly);
        r1.CeoCallMonth = new DateTime(2026, 10, 1);
        r1.EventId = "EVT-1";
        r1.RewardSize = "US9";
        var r2 = Registro(db, 2, "AMB-2", 4, new DateTime(2026, 3, 5), 110_000m);
        r2.CeoCallOptionId = 3;
        r2.CeoCallReason = "Sponsor request";
        Registro(db, 3, "AMB-3", 1, new DateTime(2026, 6, 1), 51_000m);
        Registro(db, 4, "AMB-3", 4, new DateTime(2026, 7, 1), 101_000m).EventOptionId = 2;
        Registro(db, 5, "AMB-4", 1, new DateTime(2026, 11, 13), 300_000m);   // cuenta corporativa
        db.SaveChanges();
        return db;
    }

    private static ClubRecognitionReportService Servicio(AppDbContext db) => new(db);

    [Theory]
    [InlineData(ClubRecognitionView.All,                 new long[] { 1, 4, 3, 2 })]
    [InlineData(ClubRecognitionView.NotRecognizedOnline, new long[] { 4, 3 })]
    [InlineData(ClubRecognitionView.NotRecognizedEvents, new long[] { 3, 2 })]
    [InlineData(ClubRecognitionView.RecognizedOnline,    new long[] { 1, 2 })]
    [InlineData(ClubRecognitionView.RecognizedEvents,    new long[] { 1, 4 })]
    public async Task Cada_vista_filtra_y_ordena_por_Reached_sin_cuentas_corporativas(ClubRecognitionView vista, long[] ids)
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new ClubRecognitionFilter(vista));

        filas.Select(f => f.Id).Should().Equal(ids);
    }

    [Fact]
    public async Task Filtra_por_nivel()
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new ClubRecognitionFilter(LevelId: 4));

        filas.Select(f => f.Id).Should().Equal(4, 2);
    }

    [Theory]
    [InlineData("japan", new long[] { 4, 3 })]
    [InlineData("BETO", new long[] { 2 })]
    [InlineData("ana@EXAMPLE", new long[] { 1 })]
    [InlineData("lopez", new long[] { 1 })]
    public async Task Busca_por_nombre_email_o_pais_sin_mayusculas(string termino, long[] ids)
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new ClubRecognitionFilter(Search: termino));

        filas.Select(f => f.Id).Should().Equal(ids);
    }

    [Fact]
    public async Task Los_textos_de_CEO_Call_y_evento()
    {
        using var db = Base();

        var filas = (await Servicio(db).GetAllAsync(new ClubRecognitionFilter())).ToDictionary(f => f.Id);

        (filas[1].CeoCallRecognized, filas[1].EventRecognized).Should().Be(("10 2026", "Bootcamp CIS 2026"));
        (filas[2].CeoCallRecognized, filas[2].EventRecognized).Should().Be(("Do not recognize – Sponsor request", (string?)null));
        (filas[4].CeoCallRecognized, filas[4].EventRecognized).Should().Be(((string?)null, "Recognized Long Time Ago"));
        filas[1].Should().BeEquivalentTo(new
        {
            MemberId = "AMB-1", MemberName = "Ana Lopez", Email = "ana@example.com", Username = "ana", Country = "Bulgaria",
            LevelName = "100k Earner", ReachedDate = new DateTime(2026, 11, 13), QualifyingTotal = 55_000m, RewardSize = "US9"
        });
    }

    [Fact]
    public async Task La_pagina_trae_el_total_filtrado()
    {
        using var db = Base();

        var pagina = await Servicio(db).GetPageAsync(new ClubRecognitionFilter(),
            new GridDataRequest { Page = 2, PageSize = 2 });

        pagina.TotalCount.Should().Be(4);
        pagina.Items.Select(f => f.Id).Should().Equal(3, 2);
    }

    [Fact]
    public async Task Los_registros_de_un_miembro_van_por_nivel_y_la_cuenta_corporativa_ve_los_suyos()
    {
        using var db = Base();

        (await Servicio(db).GetForMemberAsync("AMB-3")).Select(f => f.Id).Should().Equal(3, 4);
        (await Servicio(db).GetForMemberAsync("AMB-4")).Select(f => f.Id).Should().Equal(5);
    }

    [Fact]
    public void Slug_de_cada_vista()
    {
        ClubRecognitionViewNames.Slug(ClubRecognitionView.All).Should().Be("all");
        ClubRecognitionViewNames.Slug(ClubRecognitionView.NotRecognizedOnline).Should().Be("not-recognized-online");
        ClubRecognitionViewNames.Slug(ClubRecognitionView.NotRecognizedEvents).Should().Be("not-recognized-events");
        ClubRecognitionViewNames.Slug(ClubRecognitionView.RecognizedOnline).Should().Be("recognized-online");
        ClubRecognitionViewNames.Slug(ClubRecognitionView.RecognizedEvents).Should().Be("recognized-events");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~ClubRecognitionReportServiceTests"`
Expected: FAIL de compilación («ClubRecognitionView no existe»).

- [ ] **Step 3: Vistas, filtro, fila y textos**

`MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionViews.cs`:

```csharp
using System.Globalization;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Las cinco vistas del menú Club Recognition. «Online» = CEO Call.</summary>
public enum ClubRecognitionView
{
    All,
    NotRecognizedOnline,
    NotRecognizedEvents,
    RecognizedOnline,
    RecognizedEvents
}

/// <summary>Vista, nivel (null = todos) y búsqueda por nombre, email o país.</summary>
public sealed record ClubRecognitionFilter(
    ClubRecognitionView View = ClubRecognitionView.All,
    int? LevelId = null,
    string? Search = null);

/// <summary>Nombre de cada vista en las rutas de AdminWeb y en el archivo CSV.</summary>
public static class ClubRecognitionViewNames
{
    public static string Slug(ClubRecognitionView view) => view switch
    {
        ClubRecognitionView.NotRecognizedOnline => "not-recognized-online",
        ClubRecognitionView.NotRecognizedEvents => "not-recognized-events",
        ClubRecognitionView.RecognizedOnline    => "recognized-online",
        ClubRecognitionView.RecognizedEvents    => "recognized-events",
        _                                       => "all"
    };
}

/// <summary>Textos de las columnas «CEO Call Recognized» y «Event Recognized». Null = no reconocido.</summary>
public static class RecognitionText
{
    /// <summary>Separador entre una opción y su motivo («Do not recognize – Sponsor request»).</summary>
    public const string ReasonSeparator = " – ";

    /// <summary>El mes como «10 2026» (mes y año, como en la herramienta del cliente) o la opción.</summary>
    public static string? CeoCall(DateTime? month, string? optionName, string? reason) =>
        month is not null ? month.Value.ToString("M yyyy", CultureInfo.InvariantCulture) : Option(optionName, reason);

    /// <summary>El título del evento o la opción.</summary>
    public static string? Event(string? eventTitle, string? optionName, string? reason) =>
        eventTitle ?? Option(optionName, reason);

    private static string? Option(string? name, string? reason) =>
        name is null ? null : string.IsNullOrWhiteSpace(reason) ? name : name + ReasonSeparator + reason;
}

/// <summary>Una fila de Club Recognition: vistas, exportación y pestaña del perfil.</summary>
public sealed class ClubRecognitionRowView
{
    public long              Id                 { get; init; }
    public string            MemberId           { get; init; } = string.Empty;
    public string            MemberName         { get; init; } = string.Empty;
    public string            Email              { get; init; } = string.Empty;
    public string?           Username           { get; init; }
    public string            Country            { get; init; } = string.Empty;
    public int               RecognitionLevelId { get; init; }
    public string            LevelName          { get; init; } = string.Empty;
    public int               LevelSortOrder     { get; init; }
    public string?           Reward             { get; init; }
    public bool              RewardNeedsSize    { get; init; }
    public DateTime          ReachedDate        { get; init; }
    public decimal           QualifyingTotal    { get; init; }
    public RecognitionSource Source             { get; init; }
    public DateTime?         CeoCallMonth       { get; init; }
    public int?              CeoCallOptionId    { get; init; }
    public string?           CeoCallReason      { get; init; }
    public string?           CeoCallRecognized  { get; init; }
    public string?           EventId            { get; init; }
    public int?              EventOptionId      { get; init; }
    public string?           EventReason        { get; init; }
    public string?           EventRecognized    { get; init; }
    public string?           RewardSize         { get; init; }
    public string?           Notes              { get; init; }
}
```

- [ ] **Step 4: El servicio**

`MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionReportService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IClubRecognitionReportService
{
    /// <summary>Una página. La búsqueda llega en <see cref="GridDataRequest.Search"/> (la del filtro se ignora).</summary>
    Task<PagedResult<ClubRecognitionRowView>> GetPageAsync(ClubRecognitionFilter filter, GridDataRequest request,
        CancellationToken ct = default);

    /// <summary>Todas las filas que cumplen el filtro (exportación).</summary>
    Task<IReadOnlyList<ClubRecognitionRowView>> GetAllAsync(ClubRecognitionFilter filter, CancellationToken ct = default);

    /// <summary>Los registros de un miembro por orden de nivel (pestaña Recognition), aunque sea cuenta corporativa.</summary>
    Task<IReadOnlyList<ClubRecognitionRowView>> GetForMemberAsync(string memberId, CancellationToken ct = default);
}

/// <summary>
/// Lectura de Club Recognition. Vista y nivel se filtran en SQL; búsqueda (nombre, email, país, sin mayúsculas), orden y
/// paginación, en memoria: la tabla tiene una fila por Ambassador y nivel alcanzado. Sin caché: lo que el admin captura se
/// ve al momento. Las vistas no muestran cuentas corporativas. Orden: Reached de la más reciente a la más antigua, nivel
/// de mayor a menor, nombre.
/// </summary>
public class ClubRecognitionReportService : IClubRecognitionReportService
{
    private readonly AppDbContext _db;

    public ClubRecognitionReportService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ClubRecognitionRowView>> GetPageAsync(ClubRecognitionFilter filter,
        GridDataRequest request, CancellationToken ct = default)
    {
        var rows = await GetAllAsync(filter with { Search = request.Search }, ct);
        var (page, pageSize) = GridQueryableExtensions.ResolvePage(request);
        return new PagedResult<ClubRecognitionRowView>
        {
            Items      = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = rows.Count,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<IReadOnlyList<ClubRecognitionRowView>> GetAllAsync(ClubRecognitionFilter filter,
        CancellationToken ct = default)
    {
        var term = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var rows = await LoadAsync(Filtered(filter), includeCorporate: false, ct);
        return rows
            .Where(r => term is null
                     || r.MemberName.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || r.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || r.Country.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ReachedDate)
            .ThenByDescending(r => r.LevelSortOrder)
            .ThenBy(r => r.MemberName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)
            .ToList();
    }

    public async Task<IReadOnlyList<ClubRecognitionRowView>> GetForMemberAsync(string memberId,
        CancellationToken ct = default) =>
        (await LoadAsync(_db.MemberRecognitions.AsNoTracking().Where(r => r.MemberId == memberId), includeCorporate: true, ct))
            .OrderBy(r => r.LevelSortOrder).ThenBy(r => r.Id)
            .ToList();

    private IQueryable<MemberRecognition> Filtered(ClubRecognitionFilter f)
    {
        var q = _db.MemberRecognitions.AsNoTracking();
        if (f.LevelId is not null) q = q.Where(r => r.RecognitionLevelId == f.LevelId);
        return f.View switch
        {
            ClubRecognitionView.NotRecognizedOnline => q.Where(r => r.CeoCallMonth == null && r.CeoCallOptionId == null),
            ClubRecognitionView.RecognizedOnline    => q.Where(r => r.CeoCallMonth != null || r.CeoCallOptionId != null),
            ClubRecognitionView.NotRecognizedEvents => q.Where(r => r.EventId == null && r.EventOptionId == null),
            ClubRecognitionView.RecognizedEvents    => q.Where(r => r.EventId != null || r.EventOptionId != null),
            _                                       => q
        };
    }

    private async Task<List<ClubRecognitionRowView>> LoadAsync(IQueryable<MemberRecognition> records,
        bool includeCorporate, CancellationToken ct)
    {
        var rows = await (
            from r in records
            join m in _db.MemberProfiles.AsNoTracking() on r.MemberId equals m.MemberId
            where includeCorporate || !m.IsCorporateAccount
            select new
            {
                r.Id, r.MemberId, m.FirstName, m.LastName, m.Email, m.ReplicateSiteSlug, m.Country,
                r.RecognitionLevelId,
                LevelName       = r.Level!.Name,
                LevelSortOrder  = r.Level!.SortOrder,
                Reward          = r.Level!.Reward,
                RewardNeedsSize = r.Level!.RewardNeedsSize,
                r.ReachedDate, r.QualifyingTotal, r.Source,
                r.CeoCallMonth, r.CeoCallOptionId, r.CeoCallReason,
                CeoCallOptionName = r.CeoCallOption == null ? null : r.CeoCallOption.Name,
                r.EventId, r.EventOptionId, r.EventReason,
                EventTitle      = r.Event == null ? null : r.Event.Title,
                EventOptionName = r.EventOption == null ? null : r.EventOption.Name,
                r.RewardSize, r.Notes
            }).ToListAsync(ct);

        return rows.Select(x => new ClubRecognitionRowView
        {
            Id                 = x.Id,
            MemberId           = x.MemberId,
            MemberName         = $"{x.FirstName} {x.LastName}".Trim(),
            Email              = x.Email,
            Username           = x.ReplicateSiteSlug,
            Country            = x.Country,
            RecognitionLevelId = x.RecognitionLevelId,
            LevelName          = x.LevelName,
            LevelSortOrder     = x.LevelSortOrder,
            Reward             = x.Reward,
            RewardNeedsSize    = x.RewardNeedsSize,
            ReachedDate        = x.ReachedDate,
            QualifyingTotal    = x.QualifyingTotal,
            Source             = x.Source,
            CeoCallMonth       = x.CeoCallMonth,
            CeoCallOptionId    = x.CeoCallOptionId,
            CeoCallReason      = x.CeoCallReason,
            CeoCallRecognized  = RecognitionText.CeoCall(x.CeoCallMonth, x.CeoCallOptionName, x.CeoCallReason),
            EventId            = x.EventId,
            EventOptionId      = x.EventOptionId,
            EventReason        = x.EventReason,
            EventRecognized    = RecognitionText.Event(x.EventTitle, x.EventOptionName, x.EventReason),
            RewardSize         = x.RewardSize,
            Notes              = x.Notes
        }).ToList();
    }
}
```

- [ ] **Step 5: Registro en el AdminAPI**

En `MLMConquerorGlobalEdition.AdminAPI/Program.cs`, justo después del registro de `IClubRecognitionDetector` (Tarea 7):

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Reports.IClubRecognitionReportService,
                            MLMConquerorGlobalEdition.Repository.Services.Reports.ClubRecognitionReportService>();
```

- [ ] **Step 6: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~ClubRecognitionReportServiceTests"`
Expected: PASS (14).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionViews.cs MLMConquerorGlobalEdition.Repository/Services/Reports/ClubRecognitionReportService.cs MLMConquerorGlobalEdition.AdminAPI/Program.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/ClubRecognitionReportServiceTests.cs
git commit -m "Reportes: lectura de Club Recognition (cinco vistas, filtro por nivel, busqueda por nombre, email o pais, textos de CEO Call y evento, registros de un miembro)"
```

---

### Task 9: Captura del admin con auditoría

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberRecognitionController.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminMemberRecognitionControllerTests.cs`

Contrato (SuperAdmin y Admin):
- `GET api/v1/admin/members/{memberId}/recognitions` → `ApiResponse<IReadOnlyList<ClubRecognitionRowView>>`.
- `PUT api/v1/admin/members/{memberId}/recognitions/{id}` con `CaptureRequest` → la fila actualizada. 404 `RECOGNITION_NOT_FOUND` (también si el registro es de otro miembro); 400 `CEO_CALL_CONFLICT`, `EVENT_CONFLICT`, `EVENT_NOT_FOUND`, `OPTION_NOT_FOUND`, `REASON_REQUIRED`, `REASON_TOO_LONG`, `REWARD_SIZE_TOO_LONG`, `NOTES_TOO_LONG`.
- El PUT reemplaza los ocho campos capturables (lo que llegue vacío queda vacío: así se deshace un reconocimiento).

- [ ] **Step 1: Pruebas que fallan**

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using static MLMConquerorGlobalEdition.AdminAPI.Controllers.AdminMemberRecognitionController;
using static MLMConquerorGlobalEdition.AdminAPI.Tests.Reports.ClubRecognitionTestData;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>El admin captura CEO Call, evento, talla y notas; cada cambio real deja una fila en GeneralAuditTracking.</summary>
public class AdminMemberRecognitionControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 11, 0, 0);

    private static AppDbContext Base()
    {
        var db = Db();
        Niveles(db, conTalla: 4);                     // 100k Lifetime Earner lleva talla
        Opciones(db);
        Evento(db, "EVT-1", "Bootcamp CIS 2026", new DateTime(2026, 3, 10));
        Miembro(db, "AMB-1", "Ana", "Lopez");
        Miembro(db, "AMB-2", "Beto", "Diaz");
        Registro(db, 10, "AMB-1", 4, new DateTime(2026, 3, 5), 110_000m);
        Registro(db, 11, "AMB-1", 1, new DateTime(2026, 11, 13), 55_000m, RecognitionSource.Nightly);
        Registro(db, 20, "AMB-2", 1, new DateTime(2026, 11, 13), 52_000m, RecognitionSource.Nightly);
        db.SaveChanges();
        return db;
    }

    private static AdminMemberRecognitionController C(AppDbContext db) =>
        new(db, new ClubRecognitionReportService(db), Reloj(Ahora), Usuario());

    private static T Datos<T>(IActionResult r) =>
        ((ApiResponse<T>)r.Should().BeOfType<OkObjectResult>().Subject.Value!).Data!;

    private static string Codigo(IActionResult r) =>
        ((ApiResponse<object>)r.Should().BeOfType<BadRequestObjectResult>().Subject.Value!).ErrorCode!;

    private static CaptureRequest Vacio() => new(null, null, null, null, null, null, null, null);

    [Fact]
    public async Task Captura_mes_evento_talla_y_notas_y_lo_audita()
    {
        using var db = Base();

        var r = await C(db).Capture("AMB-1", 10,
            Vacio() with { CeoCallMonth = new DateTime(2026, 10, 15), EventId = "EVT-1", RewardSize = " US9 ", Notes = " Ring delivered " },
            default);

        var fila = Datos<ClubRecognitionRowView>(r);
        fila.CeoCallMonth.Should().Be(new DateTime(2026, 10, 1));
        (fila.CeoCallRecognized, fila.EventRecognized).Should().Be(("10 2026", "Bootcamp CIS 2026"));
        (fila.RewardSize, fila.Notes).Should().Be(("US9", "Ring delivered"));
        var guardado = await db.MemberRecognitions.AsNoTracking().SingleAsync(x => x.Id == 10);
        (guardado.LastUpdateBy, guardado.LastUpdateDate).Should().Be(("admin-1", (DateTime?)Ahora));
        var audit = await db.AuditTracking.SingleAsync();
        (audit.EntityName, audit.EntityId, audit.Action).Should().Be(("MemberRecognition", "10", "RecognitionCaptured"));
        audit.OldValues.Should().Contain("\"CeoCallMonth\":null").And.Contain("\"EventId\":null");
        audit.NewValues.Should().Contain("\"CeoCallMonth\":\"2026-10-01T00:00:00\"")
            .And.Contain("\"EventId\":\"EVT-1\"").And.Contain("\"RewardSize\":\"US9\"");
        (audit.ChangedBy, audit.ChangedAt).Should().Be(("admin-1", Ahora));
    }

    [Fact]
    public async Task Repetir_los_mismos_valores_no_audita_otra_vez()
    {
        using var db = Base();
        var pedido = Vacio() with { CeoCallMonth = new DateTime(2026, 10, 1), RewardSize = "US9" };

        await C(db).Capture("AMB-1", 10, pedido, default);
        await C(db).Capture("AMB-1", 10, pedido, default);

        (await db.AuditTracking.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Valor_y_opcion_a_la_vez_dan_400()
    {
        using var db = Base();

        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { CeoCallMonth = new DateTime(2026, 10, 1), CeoCallOptionId = 1 }, default))
            .Should().Be("CEO_CALL_CONFLICT");
        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { EventId = "EVT-1", EventOptionId = 1 }, default))
            .Should().Be("EVENT_CONFLICT");
        (await db.AuditTracking.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_opcion_que_pide_motivo_lo_exige_y_lo_muestra()
    {
        using var db = Base();

        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { CeoCallOptionId = 3 }, default)).Should().Be("REASON_REQUIRED");

        var fila = Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 10,
            Vacio() with { CeoCallOptionId = 3, CeoCallReason = "Sponsor request" }, default));
        fila.CeoCallRecognized.Should().Be("Do not recognize – Sponsor request");
    }

    [Fact]
    public async Task Una_opcion_sin_motivo_descarta_el_texto()
    {
        using var db = Base();

        var fila = Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 10,
            Vacio() with { EventOptionId = 1, EventReason = "ignored" }, default));

        (fila.EventReason, fila.EventRecognized).Should().Be(((string?)null, "Recognized Higher Level"));
    }

    [Fact]
    public async Task La_talla_solo_se_guarda_si_el_nivel_la_lleva()
    {
        using var db = Base();

        var fila = Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 11, Vacio() with { RewardSize = "US9" }, default));

        fila.RewardSize.Should().BeNull();
    }

    [Fact]
    public async Task Evento_inexistente_y_opcion_inexistente_dan_400()
    {
        using var db = Base();

        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { EventId = "EVT-9" }, default)).Should().Be("EVENT_NOT_FOUND");
        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { EventOptionId = 9 }, default)).Should().Be("OPTION_NOT_FOUND");
    }

    [Fact]
    public async Task Una_opcion_inactiva_no_se_elige_salvo_la_que_el_registro_ya_tiene()
    {
        using var db = Base();
        (await db.MemberRecognitions.SingleAsync(x => x.Id == 11)).EventOptionId = 2;
        (await db.RecognitionOptions.SingleAsync(o => o.Id == 2)).IsActive = false;
        await db.SaveChangesAsync();

        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { EventOptionId = 2 }, default)).Should().Be("OPTION_NOT_FOUND");
        Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 11, Vacio() with { EventOptionId = 2, Notes = "kept" }, default))
            .EventRecognized.Should().Be("Recognized Long Time Ago");
    }

    [Fact]
    public async Task Vaciar_devuelve_el_registro_a_no_reconocido_y_tambien_se_audita()
    {
        using var db = Base();
        await C(db).Capture("AMB-1", 10, Vacio() with { CeoCallMonth = new DateTime(2026, 10, 1) }, default);

        var fila = Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 10, Vacio(), default));

        fila.CeoCallRecognized.Should().BeNull();
        (await db.AuditTracking.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task El_registro_de_otro_miembro_da_404()
    {
        using var db = Base();

        var r = await C(db).Capture("AMB-1", 20, Vacio() with { Notes = "x" }, default);

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task La_lista_del_miembro_va_por_nivel()
    {
        using var db = Base();

        var filas = Datos<IReadOnlyList<ClubRecognitionRowView>>(await C(db).List("AMB-1", default));

        filas.Select(f => f.Id).Should().Equal(11, 10);
    }

    [Fact]
    public void Solo_SuperAdmin_y_Admin()
        => typeof(AdminMemberRecognitionController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin");
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminMemberRecognitionControllerTests"`
Expected: FAIL de compilación («AdminMemberRecognitionController no existe»).

- [ ] **Step 3: Implementar el controlador**

`MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberRecognitionController.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Pestaña «Recognition» del perfil: los niveles de Club Recognition que alcanzó el miembro y la captura de su
/// reconocimiento. CEO Call = un mes (se guarda el día 1) o una opción del catálogo; evento = un CorporateEvent o una
/// opción; talla del premio (solo si el nivel la lleva) y notas. Cada cambio real deja una fila en GeneralAuditTracking
/// con los valores de antes y después.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/recognitions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberRecognitionController : ControllerBase
{
    public const string AuditEntity   = "MemberRecognition";
    public const string AuditAction   = "RecognitionCaptured";
    public const int    MaxReason     = 300;
    public const int    MaxRewardSize = 20;
    public const int    MaxNotes      = 1000;

    private readonly AppDbContext                  _db;
    private readonly IClubRecognitionReportService _reports;
    private readonly IDateTimeProvider             _clock;
    private readonly ICurrentUserService           _currentUser;

    public AdminMemberRecognitionController(AppDbContext db, IClubRecognitionReportService reports,
        IDateTimeProvider clock, ICurrentUserService currentUser)
    {
        _db          = db;
        _reports     = reports;
        _clock       = clock;
        _currentUser = currentUser;
    }

    public sealed record CaptureRequest(
        DateTime? CeoCallMonth, int? CeoCallOptionId, string? CeoCallReason,
        string? EventId, int? EventOptionId, string? EventReason,
        string? RewardSize, string? Notes);

    /// <summary>Lo que se audita (antes y después).</summary>
    private sealed record Snapshot(
        DateTime? CeoCallMonth, int? CeoCallOptionId, string? CeoCallReason,
        string? EventId, int? EventOptionId, string? EventReason,
        string? RewardSize, string? Notes);

    [HttpGet]
    public async Task<IActionResult> List(string memberId, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ClubRecognitionRowView>>.Ok(await _reports.GetForMemberAsync(memberId, ct)));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Capture(string memberId, long id, [FromBody] CaptureRequest request, CancellationToken ct)
    {
        var record = await _db.MemberRecognitions.Include(r => r.Level)
            .FirstOrDefaultAsync(r => r.Id == id && r.MemberId == memberId, ct);
        if (record is null)
            return NotFound(Fail("RECOGNITION_NOT_FOUND", "Recognition record not found."));

        if (request.CeoCallMonth is not null && request.CeoCallOptionId is not null)
            return BadRequest(Fail("CEO_CALL_CONFLICT", "Choose the CEO call month or an option, not both."));
        var eventId = Clean(request.EventId);
        if (eventId is not null && request.EventOptionId is not null)
            return BadRequest(Fail("EVENT_CONFLICT", "Choose an event or an option, not both."));
        if (eventId is not null && !await _db.CorporateEvents.AnyAsync(e => e.Id == eventId, ct))
            return BadRequest(Fail("EVENT_NOT_FOUND", "Corporate event not found."));

        var (ceoReason, ceoError) = await ReasonAsync(request.CeoCallOptionId, request.CeoCallReason, record.CeoCallOptionId, ct);
        if (ceoError is not null) return BadRequest(ceoError);
        var (eventReason, eventError) = await ReasonAsync(request.EventOptionId, request.EventReason, record.EventOptionId, ct);
        if (eventError is not null) return BadRequest(eventError);

        var rewardSize = record.Level!.RewardNeedsSize ? Clean(request.RewardSize) : null;
        if (rewardSize?.Length > MaxRewardSize)
            return BadRequest(Fail("REWARD_SIZE_TOO_LONG", $"Reward size is {MaxRewardSize} characters max."));
        var notes = Clean(request.Notes);
        if (notes?.Length > MaxNotes)
            return BadRequest(Fail("NOTES_TOO_LONG", $"Notes are {MaxNotes} characters max."));

        var before = Snap(record);
        var after  = new Snapshot(
            request.CeoCallMonth is { } month ? new DateTime(month.Year, month.Month, 1) : (DateTime?)null,
            request.CeoCallOptionId, ceoReason, eventId, request.EventOptionId, eventReason, rewardSize, notes);

        if (after != before)
        {
            var now = _clock.Now;
            _db.AuditTracking.Add(new GeneralAuditTracking
            {
                EntityName = AuditEntity,
                EntityId   = record.Id.ToString(CultureInfo.InvariantCulture),
                Action     = AuditAction,
                OldValues  = JsonSerializer.Serialize(before),
                NewValues  = JsonSerializer.Serialize(after),
                ChangedBy  = _currentUser.UserId,
                ChangedAt  = now
            });
            record.CeoCallMonth    = after.CeoCallMonth;
            record.CeoCallOptionId = after.CeoCallOptionId;
            record.CeoCallReason   = after.CeoCallReason;
            record.EventId         = after.EventId;
            record.EventOptionId   = after.EventOptionId;
            record.EventReason     = after.EventReason;
            record.RewardSize      = after.RewardSize;
            record.Notes           = after.Notes;
            record.LastUpdateDate  = now;
            record.LastUpdateBy    = _currentUser.UserId;
            await _db.SaveChangesAsync(ct);
        }

        var row = (await _reports.GetForMemberAsync(memberId, ct)).Single(r => r.Id == id);
        return Ok(ApiResponse<ClubRecognitionRowView>.Ok(row));
    }

    /// <summary>
    /// Valida la opción elegida y devuelve el motivo que se guarda: el texto si la opción lo pide (obligatorio), null si
    /// no. Una opción inactiva solo vale si es la que el registro ya tenía.
    /// </summary>
    private async Task<(string? Reason, ApiResponse<object>? Error)> ReasonAsync(int? optionId, string? reason,
        int? currentOptionId, CancellationToken ct)
    {
        if (optionId is null) return (null, null);

        var option = await _db.RecognitionOptions.AsNoTracking().FirstOrDefaultAsync(o => o.Id == optionId, ct);
        if (option is null || (!option.IsActive && optionId != currentOptionId))
            return (null, Fail("OPTION_NOT_FOUND", "Recognition option not found or inactive."));
        if (!option.RequiresReason) return (null, null);

        var text = Clean(reason);
        if (text is null)
            return (null, Fail("REASON_REQUIRED", $"'{option.Name}' needs a reason."));
        if (text.Length > MaxReason)
            return (null, Fail("REASON_TOO_LONG", $"Reason is {MaxReason} characters max."));
        return (text, null);
    }

    private static Snapshot Snap(MemberRecognition r) => new(
        r.CeoCallMonth, r.CeoCallOptionId, r.CeoCallReason, r.EventId, r.EventOptionId, r.EventReason, r.RewardSize, r.Notes);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ApiResponse<object> Fail(string code, string message) => ApiResponse<object>.Fail(code, message);
}
```

Si el `using` de `IDateTimeProvider` choca con `AdminAPI.Services`, usar el alias `using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;`.

- [ ] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminMemberRecognitionControllerTests"`
Expected: PASS (12).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberRecognitionController.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminMemberRecognitionControllerTests.cs
git commit -m "Admin: captura del reconocimiento por registro (CEO Call por mes u opcion, evento u opcion, motivo, talla y notas) con auditoria en GeneralAuditTracking"
```

---

### Task 10: Pestaña «Recognition» en el perfil del miembro

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Shared/MemberRecognitionPanel.razor`
- Modify: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminMemberProfile.razor`

- [ ] **Step 1: Panel de la pestaña**

`MLMConquerorGlobalEdition.AdminWeb/Components/Shared/MemberRecognitionPanel.razor`:

```razor
@* Pestaña «Recognition» del perfil del admin: los niveles de Club Recognition que alcanzó el miembro y la captura de
   CEO Call, evento, talla y notas (PUT api/v1/admin/members/{id}/recognitions/{recId}, auditado en el AdminAPI). *@
@inject HttpClient Http

<div class="card border-0 shadow-sm">
    <div class="card-header py-3">
        <h6 class="card-title mb-0 fw-semibold"><i class="ri-trophy-line me-2 text-primary"></i>Club Recognition</h6>
    </div>
    <div class="card-body p-0">
        @if (_loading)
        {
            <div class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary"></div></div>
        }
        else if (_loadError is not null)
        {
            <div class="alert alert-danger m-3">@_loadError</div>
        }
        else if (_rows.Count == 0)
        {
            <p class="text-muted text-center py-5 mb-0">This member has not reached any recognition level yet.</p>
        }
        else
        {
            <div class="table-responsive">
                <table class="table table-sm align-middle mb-0">
                    <thead class="table-light">
                        <tr>
                            <th>Level</th><th>Reached</th><th class="text-end">Total</th><th>CEO Call Recognized</th>
                            <th>Event Recognized</th><th>Reward</th><th>Reward Size</th><th>Notes</th><th></th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var r in _rows)
                        {
                            <tr>
                                <td class="fw-semibold">@r.LevelName</td>
                                <td>@r.ReachedDate.ToString("yyyy-MM-dd")</td>
                                <td class="text-end">@r.QualifyingTotal.ToString("N2")</td>
                                <td>
                                    @if (r.CeoCallRecognized is null) { <span class="badge bg-warning text-dark">Not recognized</span> }
                                    else { @r.CeoCallRecognized }
                                </td>
                                <td>
                                    @if (r.EventRecognized is null) { <span class="badge bg-warning text-dark">Not recognized</span> }
                                    else { @r.EventRecognized }
                                </td>
                                <td>@r.Reward</td>
                                <td>@(r.RewardNeedsSize ? r.RewardSize ?? "—" : string.Empty)</td>
                                <td class="small text-muted">@r.Notes</td>
                                <td class="text-end">
                                    <button class="btn btn-sm btn-outline-primary py-0" title="Edit recognition" @onclick="() => Edit(r)">
                                        <i class="ri-edit-line"></i>
                                    </button>
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </div>
</div>

@if (_editing is not null)
{
    <div class="modal d-block" tabindex="-1" style="background:rgba(0,0,0,.45)">
        <div class="modal-dialog modal-dialog-scrollable">
            <div class="modal-content border-0 rounded-3 shadow">
                <div class="modal-header border-bottom">
                    <h5 class="modal-title fw-bold">@_editing.LevelName — reached @_editing.ReachedDate.ToString("yyyy-MM-dd")</h5>
                    <button type="button" class="btn-close" @onclick="Close"></button>
                </div>
                <div class="modal-body">
                    @if (_formError is not null)
                    {
                        <div class="alert alert-danger py-2">@_formError</div>
                    }
                    <div class="mb-3">
                        <label class="form-label fw-semibold">CEO Call Recognized</label>
                        <select class="form-select" @bind="_form.CeoChoice">
                            <option value="">Not recognized yet</option>
                            <option value="@MonthChoice">Recognized on the CEO call (pick the month)</option>
                            @foreach (var o in OptionsFor(_editing.CeoCallOptionId))
                            {
                                <option value="@($"opt:{o.Id}")">@o.Name</option>
                            }
                        </select>
                        @if (_form.CeoChoice == MonthChoice)
                        {
                            <input type="month" class="form-control mt-2" @bind="_form.CeoMonth" />
                        }
                        @if (NeedsReason(_form.CeoChoice))
                        {
                            <input class="form-control mt-2" maxlength="300" placeholder="Reason" @bind="_form.CeoReason" />
                        }
                    </div>
                    <div class="mb-3">
                        <label class="form-label fw-semibold">Event Recognized</label>
                        <select class="form-select" @bind="_form.EventChoice">
                            <option value="">Not recognized yet</option>
                            @foreach (var e in _events)
                            {
                                <option value="@($"evt:{e.Id}")">@e.Title (@e.EventDate.ToString("yyyy-MM-dd"))</option>
                            }
                            @foreach (var o in OptionsFor(_editing.EventOptionId))
                            {
                                <option value="@($"opt:{o.Id}")">@o.Name</option>
                            }
                        </select>
                        @if (NeedsReason(_form.EventChoice))
                        {
                            <input class="form-control mt-2" maxlength="300" placeholder="Reason" @bind="_form.EventReason" />
                        }
                    </div>
                    @if (_editing.RewardNeedsSize)
                    {
                        <div class="mb-3">
                            <label class="form-label fw-semibold">Reward Size</label>
                            <input class="form-control" maxlength="20" placeholder="e.g. US9" @bind="_form.RewardSize" />
                        </div>
                    }
                    <div>
                        <label class="form-label fw-semibold">Notes</label>
                        <textarea class="form-control" rows="3" maxlength="1000" @bind="_form.Notes"></textarea>
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn btn-secondary" @onclick="Close">Cancel</button>
                    <button class="btn btn-primary" @onclick="SaveAsync" disabled="@_saving">
                        @if (_saving) { <span class="spinner-border spinner-border-sm me-1"></span> }
                        Save
                    </button>
                </div>
            </div>
        </div>
    </div>
}

@code {
    [Parameter, EditorRequired] public string MemberId { get; set; } = string.Empty;

    private const string MonthChoice = "month";

    private List<Row> _rows = new();
    private List<OptionItem> _options = new();
    private List<EventItem> _events = new();
    private bool _loading = true;
    private string? _loadError;
    private Row? _editing;
    private CaptureForm _form = new();
    private string? _formError;
    private bool _saving;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var rows    = await Http.GetFromJsonAsync<ApiResp<List<Row>>>($"api/v1/admin/members/{MemberId}/recognitions");
            var lookups = await Http.GetFromJsonAsync<ApiResp<Lookups>>("api/v1/admin/club-recognition/lookups");
            _rows    = rows?.Data ?? new();
            _options = lookups?.Data?.Options ?? new();
            _events  = lookups?.Data?.Events ?? new();
        }
        catch (Exception ex) { _loadError = $"Could not load club recognition: {ex.Message}"; }
        finally { _loading = false; }
    }

    private void Edit(Row r)
    {
        _formError = null;
        _form = new CaptureForm
        {
            CeoChoice   = r.CeoCallMonth is not null ? MonthChoice : r.CeoCallOptionId is int c ? $"opt:{c}" : string.Empty,
            CeoMonth    = r.CeoCallMonth ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            CeoReason   = r.CeoCallReason,
            EventChoice = r.EventId is not null ? $"evt:{r.EventId}" : r.EventOptionId is int e ? $"opt:{e}" : string.Empty,
            EventReason = r.EventReason,
            RewardSize  = r.RewardSize,
            Notes       = r.Notes
        };
        _editing = r;
    }

    private void Close() => _editing = null;

    private IEnumerable<OptionItem> OptionsFor(int? current) =>
        _options.Where(o => o.IsActive || o.Id == current).OrderBy(o => o.SortOrder);

    private bool NeedsReason(string choice) =>
        OptionId(choice) is int id && _options.Any(o => o.Id == id && o.RequiresReason);

    private static int? OptionId(string choice) =>
        choice.StartsWith("opt:", StringComparison.Ordinal) && int.TryParse(choice[4..], out var id) ? id : null;

    private static string? EventId(string choice) =>
        choice.StartsWith("evt:", StringComparison.Ordinal) ? choice[4..] : null;

    private async Task SaveAsync()
    {
        if (_editing is null) return;
        _saving = true;
        _formError = null;
        try
        {
            var body = new
            {
                CeoCallMonth    = _form.CeoChoice == MonthChoice ? _form.CeoMonth : null,
                CeoCallOptionId = OptionId(_form.CeoChoice),
                CeoCallReason   = NeedsReason(_form.CeoChoice) ? _form.CeoReason : null,
                EventId         = EventId(_form.EventChoice),
                EventOptionId   = OptionId(_form.EventChoice),
                EventReason     = NeedsReason(_form.EventChoice) ? _form.EventReason : null,
                _form.RewardSize,
                _form.Notes
            };
            var resp = await Http.PutAsJsonAsync($"api/v1/admin/members/{MemberId}/recognitions/{_editing.Id}", body);
            if (resp.IsSuccessStatusCode)
            {
                var saved = (await resp.Content.ReadFromJsonAsync<ApiResp<Row>>())?.Data;
                var index = saved is null ? -1 : _rows.FindIndex(x => x.Id == saved.Id);
                if (index >= 0) _rows[index] = saved!;
                _editing = null;
            }
            else
            {
                var error = await resp.Content.ReadFromJsonAsync<ApiResp<object>>();
                _formError = error?.Errors?.FirstOrDefault() ?? $"Save failed ({(int)resp.StatusCode}).";
            }
        }
        catch (Exception ex) { _formError = ex.Message; }
        finally { _saving = false; }
    }

    private sealed class CaptureForm
    {
        public string    CeoChoice   { get; set; } = string.Empty;
        public DateTime? CeoMonth    { get; set; }
        public string?   CeoReason   { get; set; }
        public string    EventChoice { get; set; } = string.Empty;
        public string?   EventReason { get; set; }
        public string?   RewardSize  { get; set; }
        public string?   Notes       { get; set; }
    }

    private sealed class Row
    {
        public long      Id                { get; set; }
        public string    LevelName         { get; set; } = string.Empty;
        public string?   Reward            { get; set; }
        public bool      RewardNeedsSize   { get; set; }
        public DateTime  ReachedDate       { get; set; }
        public decimal   QualifyingTotal   { get; set; }
        public DateTime? CeoCallMonth      { get; set; }
        public int?      CeoCallOptionId   { get; set; }
        public string?   CeoCallReason     { get; set; }
        public string?   CeoCallRecognized { get; set; }
        public string?   EventId           { get; set; }
        public int?      EventOptionId     { get; set; }
        public string?   EventReason       { get; set; }
        public string?   EventRecognized   { get; set; }
        public string?   RewardSize        { get; set; }
        public string?   Notes             { get; set; }
    }

    private sealed record OptionItem(int Id, string Name, bool RequiresReason, int SortOrder, bool IsActive);
    private sealed record EventItem(string Id, string Title, DateTime EventDate);
    private sealed record Lookups(List<OptionItem> Options, List<EventItem> Events);
    private sealed record ApiResp<T>(bool Success, T? Data, string? ErrorCode, IEnumerable<string>? Errors);
}
```

- [ ] **Step 2: La pestaña en el perfil**

En `AdminMemberProfile.razor`, en la lista `_tabs`, justo después de `new("ranks",      "Ranks",           "ri-award-line"),`:

```csharp
        new("recognition","Recognition",     "ri-trophy-line"),
```

Y en la cadena de pestañas, justo antes de `else if (_activeTab == "membership")`:

```razor
    else if (_activeTab == "recognition")
    {
        <AuthorizeView Roles="SuperAdmin,Admin">
            <Authorized>
                <MemberRecognitionPanel MemberId="@MemberId" />
            </Authorized>
            <NotAuthorized>
                <div class="alert alert-secondary">Club recognition is available to SuperAdmin and Admin only.</div>
            </NotAuthorized>
        </AuthorizeView>
    }
```

`?tab=recognition` abre la pestaña directamente (la página ya acepta cualquier clave de `_tabs`); las vistas enlazan así.

- [ ] **Step 3: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/Components/Shared/MemberRecognitionPanel.razor MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminMemberProfile.razor
git commit -m "Admin: pestana Recognition en el perfil del miembro con los niveles alcanzados y la captura de CEO Call, evento, talla y notas"
```

---

### Task 11: Endpoints de las vistas y exportación a CSV

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionReportController.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminClubRecognitionReportControllerTests.cs`

Contrato:
- `POST api/v1/admin/reports/club-recognition/grid?view=NotRecognizedOnline&levelId=4` con `GridDataRequest` (búsqueda en `Search`) → `ApiResponse<PagedResult<ClubRecognitionRowView>>`. Sin `view` = All.
- `GET api/v1/admin/reports/club-recognition/export?view=...&levelId=...&search=...` → CSV `club-recognition-<slug>-yyyy-MM-dd.csv` (fecha de hoy, hora del servidor). Columnas: Ambassador, Email, Username, Country, Level, Reached, Total, CEO Call Recognized, Event Recognized, Reward Size.
- Clase y exportación: SuperAdmin y Admin.

- [ ] **Step 1: Pruebas que fallan**

```csharp
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminClubRecognitionReportControllerTests
{
    private static (AdminClubRecognitionReportController C, Mock<IClubRecognitionReportService> S) Montar()
    {
        var servicio = new Mock<IClubRecognitionReportService>();
        return (new AdminClubRecognitionReportController(servicio.Object, new CsvReportWriter(),
            ClubRecognitionTestData.Reloj(new DateTime(2026, 11, 14, 10, 30, 0))), servicio);
    }

    [Fact]
    public async Task La_rejilla_pasa_vista_nivel_y_pedido_al_servicio()
    {
        var (c, s) = Montar();
        var pedido = new GridDataRequest { Search = "ana" };
        s.Setup(x => x.GetPageAsync(
                It.Is<ClubRecognitionFilter>(f => f.View == ClubRecognitionView.NotRecognizedOnline && f.LevelId == 4),
                pedido, It.IsAny<CancellationToken>()))
         .ReturnsAsync(new PagedResult<ClubRecognitionRowView>());

        var r = await c.Grid(pedido, ClubRecognitionView.NotRecognizedOnline, 4, default);

        r.Should().BeOfType<OkObjectResult>();
        s.VerifyAll();
    }

    [Fact]
    public async Task Exportar_da_un_csv_con_las_diez_columnas()
    {
        var (c, s) = Montar();
        s.Setup(x => x.GetAllAsync(
                It.Is<ClubRecognitionFilter>(f => f.View == ClubRecognitionView.NotRecognizedOnline && f.LevelId == null
                                               && f.Search == "ana"),
                It.IsAny<CancellationToken>()))
         .ReturnsAsync(new[]
         {
             new ClubRecognitionRowView
             {
                 Id = 1, MemberId = "AMB-1", MemberName = "Ana Lopez", Email = "ana@example.com", Username = "ana",
                 Country = "Bulgaria", LevelName = "100k Earner", ReachedDate = new DateTime(2026, 11, 13),
                 QualifyingTotal = 55_000m, CeoCallRecognized = null, EventRecognized = "Bootcamp CIS 2026", RewardSize = "US9"
             }
         });

        var r = await c.Export(ClubRecognitionView.NotRecognizedOnline, null, "ana", default);

        var archivo = r.Should().BeOfType<FileContentResult>().Subject;
        archivo.ContentType.Should().Be(CsvReportWriter.ContentType);
        archivo.FileDownloadName.Should().Be("club-recognition-not-recognized-online-2026-11-14.csv");
        Encoding.UTF8.GetString(archivo.FileContents, 3, archivo.FileContents.Length - 3).Should().Be(
            "Ambassador,Email,Username,Country,Level,Reached,Total,CEO Call Recognized,Event Recognized,Reward Size\r\n" +
            "Ana Lopez,ana@example.com,ana,Bulgaria,100k Earner,2026-11-13,55000,,Bootcamp CIS 2026,US9\r\n");
    }

    [Fact]
    public void Leer_y_exportar_es_de_SuperAdmin_y_Admin()
    {
        typeof(AdminClubRecognitionReportController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin");
        typeof(AdminClubRecognitionReportController).GetMethod(nameof(AdminClubRecognitionReportController.Export))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminClubRecognitionReportControllerTests"`
Expected: FAIL de compilación («AdminClubRecognitionReportController no existe»).

- [ ] **Step 3: Implementar el controlador**

`MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionReportController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Vistas de Club Recognition (All Records, Not Recognized Online, Not Recognized on Events, Recognized Online,
/// Recognized Events: un endpoint con el parámetro view). Lectura y exportación a CSV para SuperAdmin y Admin.
/// </summary>
[ApiController]
[Route("api/v1/admin/reports/club-recognition")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminClubRecognitionReportController : ControllerBase
{
    private readonly IClubRecognitionReportService _reports;
    private readonly CsvReportWriter               _csv;
    private readonly IDateTimeProvider             _clock;

    public AdminClubRecognitionReportController(IClubRecognitionReportService reports, CsvReportWriter csv,
        IDateTimeProvider clock)
    {
        _reports = reports;
        _csv     = csv;
        _clock   = clock;
    }

    /// <summary>POST reports/club-recognition/grid?view=&amp;levelId= — la búsqueda va en el cuerpo.</summary>
    [HttpPost("grid")]
    public async Task<IActionResult> Grid([FromBody] GridDataRequest request, [FromQuery] ClubRecognitionView view,
        [FromQuery] int? levelId, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<ClubRecognitionRowView>>.Ok(
            await _reports.GetPageAsync(new ClubRecognitionFilter(view, levelId), request, ct)));

    /// <summary>GET reports/club-recognition/export?view=&amp;levelId=&amp;search= — CSV con las filas filtradas.</summary>
    [HttpGet("export")]
    // Punto de enganche para acotar la exportacion a un rol propio mas adelante (permiso reports.export).
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Export([FromQuery] ClubRecognitionView view, [FromQuery] int? levelId,
        [FromQuery] string? search, CancellationToken ct)
    {
        var rows  = await _reports.GetAllAsync(new ClubRecognitionFilter(view, levelId, search), ct);
        var bytes = _csv.Write(rows,
            ("Ambassador", r => r.MemberName), ("Email", r => r.Email), ("Username", r => r.Username),
            ("Country", r => r.Country), ("Level", r => r.LevelName), ("Reached", r => r.ReachedDate),
            ("Total", r => r.QualifyingTotal), ("CEO Call Recognized", r => r.CeoCallRecognized),
            ("Event Recognized", r => r.EventRecognized), ("Reward Size", r => r.RewardSize));
        var file = $"club-recognition-{ClubRecognitionViewNames.Slug(view)}-{_clock.Now:yyyy-MM-dd}.csv";
        return File(bytes, CsvReportWriter.ContentType, file);
    }
}
```

- [ ] **Step 4: Correr las pruebas de la carpeta y compilar**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~Reports"`
Expected: PASS (todas las de la carpeta, también las de los Planes A y B).
Run: `dotnet build MLMConquerorGlobalEdition.AdminAPI`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminClubRecognitionReportController.cs MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminClubRecognitionReportControllerTests.cs
git commit -m "Admin: endpoints de las vistas de Club Recognition (rejilla de servidor por vista y nivel, exportacion a CSV)"
```

---

### Task 12: Página de las vistas

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminClubRecognitionReport.razor`

Una página, cinco rutas (`/admin/club-recognition/{all|not-recognized-online|not-recognized-events|recognized-online|recognized-events}`). El nombre del Ambassador enlaza a su perfil en la pestaña Recognition. En All Records, un SuperAdmin ve «Run historical load».

- [ ] **Step 1: La página**

`MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminClubRecognitionReport.razor`:

```razor
@page "/admin/club-recognition/all"
@page "/admin/club-recognition/not-recognized-online"
@page "/admin/club-recognition/not-recognized-events"
@page "/admin/club-recognition/recognized-online"
@page "/admin/club-recognition/recognized-events"
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

<PageTitle>@Current.Title</PageTitle>

<h4 class="mb-1">@Current.Title</h4>
<p class="text-muted mb-4">@Current.Subtitle</p>

<div class="d-flex flex-wrap gap-2 align-items-end mb-3">
    <div class="flex-grow-1" style="min-width:240px">
        <input class="form-control" placeholder="Search name, email or country..." @bind="_search" @bind:event="oninput"
               @onkeyup="OnSearchKeyAsync" />
    </div>
    <div>
        <button class="btn btn-primary" @onclick="ApplySearchAsync"><i class="ri-search-line me-1"></i>Search</button>
    </div>
    <div>
        <label class="form-label small mb-1">Level</label>
        <select class="form-select" @bind="_levelId" @bind:after="RefreshAsync">
            <option value="">All levels</option>
            @foreach (var l in _levels)
            {
                <option value="@l.Id">@l.Name@(l.IsActive ? string.Empty : " (inactive)")</option>
            }
        </select>
    </div>
    @if (ViewContext.HasPermission(Permissions.Reports.Export))
    {
        <button class="btn btn-outline-success" @onclick="ExportAsync" disabled="@_exporting">
            <i class="ri-file-download-line me-1"></i>Export CSV
        </button>
    }
    @if (Current.View == "All")
    {
        <AuthorizeView Roles="SuperAdmin">
            <Authorized>
                <button class="btn btn-outline-secondary" @onclick="LoadHistoryAsync" disabled="@_loadingHistory"
                        title="Reads all past commissions and adds who already reached each level, with the day they crossed it">
                    @if (_loadingHistory) { <span class="spinner-border spinner-border-sm me-1"></span> }
                    <i class="ri-history-line me-1"></i>Run historical load
                </button>
            </Authorized>
        </AuthorizeView>
    }
</div>

@if (_info is not null)
{
    <div class="alert alert-info alert-dismissible">
        @_info <button type="button" class="btn-close" @onclick="() => _info = null"></button>
    </div>
}

@if (_error is not null)
{
    <div class="alert alert-danger">@_error</div>
}

@if (_total is not null)
{
    <div class="text-muted small mb-2">@_total.Value.ToString("N0") results</div>
}

<SfGrid TValue="RecognitionRow" @ref="_grid" @key="Current.View" AllowPaging="true">
    <SfDataManager Adaptor="Adaptors.CustomAdaptor">
        <ServerGridAdaptor TValue="RecognitionRow" Http="Http" Endpoint="api/v1/admin/reports/club-recognition/grid"
                           QueryStringProvider="Query" OnCount="OnCountAsync" OnServerError="OnServerErrorAsync" />
    </SfDataManager>
    <GridPageSettings PageSize="20" PageSizes="@(new int[] { 20, 50, 100 })" />
    <GridColumns>
        <GridColumn Field="@nameof(RecognitionRow.MemberName)" HeaderText="Ambassador">
            <Template>
                @{ var r = (context as RecognitionRow)!; }
                <a href="@($"/admin/members/{r.MemberId}?tab=recognition")">@r.MemberName</a>
            </Template>
        </GridColumn>
        <GridColumn Field="@nameof(RecognitionRow.Email)" HeaderText="Email" />
        <GridColumn Field="@nameof(RecognitionRow.Username)" HeaderText="Username" />
        <GridColumn Field="@nameof(RecognitionRow.Country)" HeaderText="Country" />
        <GridColumn Field="@nameof(RecognitionRow.LevelName)" HeaderText="Level" />
        <GridColumn Field="@nameof(RecognitionRow.ReachedDate)" HeaderText="Reached" Width="120">
            <Template>@((context as RecognitionRow)!.ReachedDate.ToString("yyyy-MM-dd"))</Template>
        </GridColumn>
        <GridColumn Field="@nameof(RecognitionRow.QualifyingTotal)" HeaderText="Total" TextAlign="TextAlign.Right" Width="130">
            <Template>@((context as RecognitionRow)!.QualifyingTotal.ToString("N2"))</Template>
        </GridColumn>
        <GridColumn Field="@nameof(RecognitionRow.CeoCallRecognized)" HeaderText="CEO Call Recognized" />
        <GridColumn Field="@nameof(RecognitionRow.EventRecognized)" HeaderText="Event Recognized" />
        <GridColumn Field="@nameof(RecognitionRow.RewardSize)" HeaderText="Reward Size" Width="120" />
    </GridColumns>
</SfGrid>

@code {
    private sealed record ViewInfo(string Slug, string View, string Title, string Subtitle);

    private static readonly ViewInfo[] Views =
    {
        new("all", "All", "All Records", "Every Ambassador who reached a recognition level."),
        new("not-recognized-online", "NotRecognizedOnline", "Not Recognized Online",
            "Reached a level and still without a CEO call result."),
        new("not-recognized-events", "NotRecognizedEvents", "Not Recognized on Events",
            "Reached a level and still without an event result."),
        new("recognized-online", "RecognizedOnline", "Recognized Online",
            "Recognized on the CEO call, or marked with a recognition option."),
        new("recognized-events", "RecognizedEvents", "Recognized Events",
            "Recognized at an event, or marked with a recognition option."),
    };

    private SfGrid<RecognitionRow>? _grid;
    private List<LevelItem> _levels = new();
    private string  _search = string.Empty;
    private string  _appliedSearch = string.Empty;   // el termino que la rejilla ya aplico: el que usa la exportacion
    private int?    _levelId;
    private int?    _total;
    private string? _lastView;
    private bool    _exporting;
    private bool    _loadingHistory;
    private string? _error;
    private string? _info;

    private ViewInfo Current
    {
        get
        {
            var path = new Uri(Nav.Uri).AbsolutePath.TrimEnd('/');
            var slug = path[(path.LastIndexOf('/') + 1)..];
            return Views.FirstOrDefault(v => v.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)) ?? Views[0];
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiResp<Lookups>>("api/v1/admin/club-recognition/lookups");
            _levels = resp?.Data?.Levels ?? new();
        }
        catch { /* sin la lista, el filtro queda en «All levels» */ }
    }

    protected override void OnParametersSet()
    {
        var view = Current.View;
        if (_lastView is not null && _lastView != view)
        {
            _search = _appliedSearch = string.Empty;
            _levelId = null;
            _total = null;
            _error = null;
            _info = null;
        }
        _lastView = view;
    }

    private Task OnServerErrorAsync(string? errorCode)
    {
        _total = null;
        _error = errorCode is null ? "The report could not be loaded." : $"The report could not be loaded ({errorCode}).";
        return InvokeAsync(StateHasChanged);
    }

    private async Task OnCountAsync(int count)
    {
        _error = null;
        _total = count;
        await InvokeAsync(StateHasChanged);
    }

    private string Query()
    {
        var q = new List<string> { $"view={Current.View}" };
        if (_levelId is not null) q.Add($"levelId={_levelId}");
        return string.Join("&", q);
    }

    private async Task RefreshAsync()
    {
        if (_grid is not null) await _grid.Refresh();
    }

    // Como Earnings y Rank: la busqueda se lanza con Enter o con el boton, no en cada tecla.
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
            var url   = $"api/v1/admin/reports/club-recognition/export?{Query()}&search={Uri.EscapeDataString(_appliedSearch)}";
            var bytes = await Http.GetByteArrayAsync(url);
            var file  = $"club-recognition-{Current.Slug}-{DateTime.Today:yyyy-MM-dd}.csv";
            await Js.InvokeVoidAsync("mlmDownloadFile", file, "text/csv", Convert.ToBase64String(bytes));
        }
        catch (Exception ex)
        {
            _error = $"Export failed: {ex.Message}";
        }
        finally { _exporting = false; }
    }

    private async Task LoadHistoryAsync()
    {
        if (!await Js.InvokeAsync<bool>("confirm",
                "Read all past commissions and add every Ambassador who already reached a level? Existing records are not changed."))
            return;
        _loadingHistory = true;
        _error = null;
        try
        {
            var resp = await Http.PostAsync("api/v1/admin/club-recognition/detection/history", null);
            if (resp.IsSuccessStatusCode)
            {
                var result = (await resp.Content.ReadFromJsonAsync<ApiResp<HistoryResult>>())?.Data;
                _info = result is null
                    ? "Historical load finished."
                    : $"Historical load through {result.LastDay:yyyy-MM-dd}: {result.Added:N0} new records.";
                await RefreshAsync();
            }
            else _error = $"Historical load failed ({(int)resp.StatusCode}).";
        }
        catch (Exception ex) { _error = $"Historical load failed: {ex.Message}"; }
        finally { _loadingHistory = false; }
    }

    private sealed record LevelItem(int Id, string Name, int SortOrder, bool IsActive);
    private sealed record Lookups(List<LevelItem> Levels);
    private sealed record HistoryResult(DateTime LastDay, int Added);
    private sealed record ApiResp<T>(bool Success, T? Data, IEnumerable<string>? Errors);

    private sealed class RecognitionRow
    {
        public long     Id                { get; set; }
        public string   MemberId          { get; set; } = string.Empty;
        public string   MemberName        { get; set; } = string.Empty;
        public string   Email             { get; set; } = string.Empty;
        public string?  Username          { get; set; }
        public string   Country           { get; set; } = string.Empty;
        public string   LevelName         { get; set; } = string.Empty;
        public DateTime ReachedDate       { get; set; }
        public decimal  QualifyingTotal   { get; set; }
        public string?  CeoCallRecognized { get; set; }
        public string?  EventRecognized   { get; set; }
        public string?  RewardSize        { get; set; }
    }
}
```

- [ ] **Step 2: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminClubRecognitionReport.razor
git commit -m "Admin: pagina de Club Recognition con cinco vistas, busqueda, filtro por nivel, conteo, exportacion solo con permiso y carga historica para SuperAdmin"
```

---

### Task 13: Menú (Club Recognition y Parameters)

**Files:**
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor`

Orden de la especificación: Club Recognition → All Records · Not Recognized Online · Not Recognized on Events · Recognized Online · Recognized Events. Parámetros → Niveles de reconocimiento · Opciones de reconocimiento.

- [ ] **Step 1: Los dos grupos, debajo de Reports**

Dentro del primer `<AuthorizeView Roles="SuperAdmin,Admin">` (el del grupo Reports), justo después del `</li>` que cierra el grupo Reports (la línea `                </li>` que sigue al `</ul>` de la entrada Current Rank) y antes de `            </Authorized>`:

```razor
                @* ── Club Recognition ── *@
                <li class="menu-item @(IsSection("/admin/club-recognition") ? "open active" : "")">
                    <a href="javascript:void(0);" class="menu-link menu-toggle">
                        <i class="ri-trophy-line menu-icon"></i>
                        <span class="title">Club Recognition</span>
                    </a>
                    <ul class="menu-sub">
                        <li class="menu-item @IsActive("/admin/club-recognition/all")">
                            <a href="/admin/club-recognition/all" class="menu-link @IsActive("/admin/club-recognition/all")">
                                <span class="title">All Records</span>
                            </a>
                        </li>
                        <li class="menu-item @IsActive("/admin/club-recognition/not-recognized-online")">
                            <a href="/admin/club-recognition/not-recognized-online" class="menu-link @IsActive("/admin/club-recognition/not-recognized-online")">
                                <span class="title">Not Recognized Online</span>
                            </a>
                        </li>
                        <li class="menu-item @IsActive("/admin/club-recognition/not-recognized-events")">
                            <a href="/admin/club-recognition/not-recognized-events" class="menu-link @IsActive("/admin/club-recognition/not-recognized-events")">
                                <span class="title">Not Recognized on Events</span>
                            </a>
                        </li>
                        <li class="menu-item @IsActive("/admin/club-recognition/recognized-online")">
                            <a href="/admin/club-recognition/recognized-online" class="menu-link @IsActive("/admin/club-recognition/recognized-online")">
                                <span class="title">Recognized Online</span>
                            </a>
                        </li>
                        <li class="menu-item @IsActive("/admin/club-recognition/recognized-events")">
                            <a href="/admin/club-recognition/recognized-events" class="menu-link @IsActive("/admin/club-recognition/recognized-events")">
                                <span class="title">Recognized Events</span>
                            </a>
                        </li>
                    </ul>
                </li>
                @* ── Parameters ── *@
                <li class="menu-item @(IsSection("/admin/parameters") ? "open active" : "")">
                    <a href="javascript:void(0);" class="menu-link menu-toggle">
                        <i class="ri-equalizer-line menu-icon"></i>
                        <span class="title">Parameters</span>
                    </a>
                    <ul class="menu-sub">
                        <li class="menu-item @IsActive("/admin/parameters/recognition-levels")">
                            <a href="/admin/parameters/recognition-levels" class="menu-link @IsActive("/admin/parameters/recognition-levels")">
                                <span class="title">Recognition Levels</span>
                            </a>
                        </li>
                        <li class="menu-item @IsActive("/admin/parameters/recognition-options")">
                            <a href="/admin/parameters/recognition-options" class="menu-link @IsActive("/admin/parameters/recognition-options")">
                                <span class="title">Recognition Options</span>
                            </a>
                        </li>
                    </ul>
                </li>
```

- [ ] **Step 2: Compilar y correr la prueba que lee el menú**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.
Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~PantallaAdminEntraxSignupsTests"`
Expected: PASS (esa prueba lee `AdminSidebarMenu.razor`; no debe romperse).

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor
git commit -m "Admin: grupos Club Recognition (cinco vistas) y Parameters (niveles y opciones de reconocimiento) en el menu"
```

---

### Task 14: Verificación final del plan C

- [ ] **Step 1: Suites completas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests`
Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests`
Expected: 0 fallos en las dos. Las `[SqlServerFact]` pasan (con la base local migrada) o se saltan; `Migraciones` pasa (el script de despliegue incluye `ReconocimientoDeClubes`); las reglas de escritores (`EscritoresDelBinarioTests`, `EscritoresDeSuscripcionesTests`) siguen en verde: este plan no escribe en `DualTeamTree` ni en `MembershipSubscriptions`.

- [ ] **Step 2: Probar en el navegador (AdminWeb + AdminAPI + BizCenter levantados contra la base local)**

1. Como SuperAdmin, Parameters → Recognition Levels: los seis niveles de la semilla. Marcar «Reward needs a size» en «100k Lifetime Earner» y guardar. Crear un nivel con el nombre de otro: aparece el error en el modal.
2. Parameters → Recognition Options: las tres opciones; «Do not recognize» con «Needs reason = Yes».
3. Club Recognition → All Records: pulsar «Run historical load» y confirmar; aparece «Historical load through <ayer>: N new records» y la rejilla con el conteo. En la base local con pocas comisiones puede ser 0: sembrar a mano no es parte de la prueba; basta con que no falle y que el segundo clic diga 0.
4. En el panel de Hangfire del BizCenter (`/hangfire`), `club-recognition` aparece en Recurring jobs con «45 5 * * *» UTC; «Trigger now» corre sin error (con el marcador ya puesto, hace la detección nocturna).
5. Si hay algún registro: abrir el Ambassador desde la rejilla (va a `?tab=recognition`), editar: elegir «Recognized on the CEO call» y un mes, un evento, talla y notas; guardar. La fila muestra «M yyyy» y el título del evento. Elegir «Do not recognize» sin motivo: error «needs a reason». En la base, `AuditTracking` tiene una fila `MemberRecognition / RecognitionCaptured` por guardado con cambios.
6. Not Recognized Online / on Events, Recognized Online / Events: el registro editado pasa de una a otra lista al instante; el filtro por nivel y la búsqueda por país funcionan; exportar trae las mismas filas. Con un usuario IT no se ven los grupos nuevos del menú ni el botón Export; con Admin no se ve «Run historical load».

- [ ] **Step 3: Medidas**

Completar la sección «Medidas» con los números de la Tarea 6, Step 5. Si la carga histórica pasa de 60 s por ventana o el nocturno de 30 s en total con la máquina libre, avisar a Nick con los números antes de dar el plan por cerrado.

- [ ] **Step 4: Revisión de código de la rama** con `superpowers:requesting-code-review` y corregir lo que encuentre.

## Preguntas abiertas (con el valor por defecto que aplica este plan)

1. **Reached del nocturno.** La especificación dice «la fecha del proceso nocturno que lo detectó». El job corre pasada la medianoche y evalúa el día que cerró, así que por defecto `Reached = día que cerró` (ayer en hora del servidor), que es el día en que de verdad se cruzó el umbral y coincide con la regla de la carga histórica. ¿Prefiere Nick la fecha de la corrida (un día después)?
2. **Elegibilidad con el estado de hoy.** La carga histórica decide quién califica (Ambassador, no Terminated, no corporativo) con el estado actual del perfil, no con el que tenía el día del cruce. Un Ambassador hoy Terminated no recibe registro aunque cruzara el umbral cuando estaba activo.
3. **Terminated y corporativos en las vistas.** Por defecto las vistas ocultan las cuentas corporativas (aunque tuvieran registro de antes de marcarlas) y **muestran** a quien quedó Terminated después de calificar (el registro no se pierde). La pestaña del perfil muestra siempre los registros del miembro.
4. **Premio y talla de la semilla.** Los seis niveles se siembran sin `Reward` y con `RewardNeedsSize = false`: no sabemos qué premio lleva cada uno. Nick (o el admin) los completa en Parameters; mientras tanto la columna Reward Size queda vacía.
5. **«Do not recognize – <motivo>».** Por defecto es una sola opción que pide motivo libre (300 caracteres). Si el cliente usa siempre los mismos motivos, el admin puede crear opciones fijas («Do not recognize – Sponsor request») sin motivo.
6. **Un catálogo para las dos columnas.** Las opciones sirven igual para CEO Call y para evento. Si alguna solo aplica a una de las dos, haría falta un campo «aplica a» (no incluido).
7. **Niveles nuevos o umbrales cambiados.** Desde el cambio, el nocturno los aplica con la fecha de la noche. Para fechar a quien ya los había cruzado antes, un SuperAdmin pulsa «Run historical load»: añade lo que falta con la fecha real de cruce y no toca lo existente. Bajar un umbral nunca borra registros; subirlo tampoco.
8. **Auditoría de los parámetros.** Se auditan los cambios de cada registro (lo que pide la especificación). Los cambios de niveles y opciones solo dejan `LastUpdateBy`/`LastUpdateDate`. ¿Auditarlos también?
9. **Carga manual y el navegador.** El endpoint corre la carga sin el token de la petición: si el navegador corta (100 s por defecto de `HttpClient`), la carga termina igual. Medido en la base de escala: ~14-15 s por ventana sin el índice de fechas.
10. **Menú de parámetros.** Se crea un grupo «Parameters» con las dos entradas; «Global Parameters» sigue donde está. ¿Mover «Global Parameters» a ese grupo?
11. **Ventana móvil con montos negativos.** La carga histórica es exacta también si hubiera montos negativos (ajustes): el total solo cambia el día que entra o sale un monto y se evalúa en todos esos días. Hoy la base de escala no tiene negativos.

## Medidas

_(Tarea 6, Step 5. Base `MLMConquerorGlobalEditionDb_Escala` en `Server=.`, solo lectura, `sqlcmd -I`.)_

Preparación del plan (14-nov-2026), SQL escrito a mano equivalente al del plan, máquina con otras cargas:
- Base de escala: `CommissionEarnings` 1.939.875 filas (2024-10-21 a 2026-10-22), sin montos negativos; mayor acumulado de por vida 18.875 (con los umbrales reales no califica nadie: se miden umbrales bajados). Índices: PK, `IX_..._BeneficiaryMemberId_Status`, `IX_..._CommissionOperationTypeId`, `IX_..._CommissionTypeId`, `IX_..._PeriodDate`, `IX_..._SourceOrderId_CommissionTypeId`. **No** tiene `IX_CommissionEarnings_EarnedDate_Beneficiary` (Plan A): se recorre el índice agrupado, así que estas medidas son una cota alta.
- Carga histórica, ventana de 180 días (umbrales 500 / 1.500 / 3.000): 51.383 / 8 / 5 miembros; `CommissionEarnings` 182.362 lecturas lógicas (una sola exploración con `CROSS APPLY`; con la CTE leída dos veces eran 369.029), `MemberProfiles` 86.599; CPU 19,3 s, **13,7 s**.
- Carga histórica, de por vida (umbrales 1.000 / 2.000 / 4.000): 4.219 / 7 / 5 miembros; `CommissionEarnings` 182.482, `MemberProfiles` 88.745; CPU 15,1 s, **15,0 s**.
- Agregado nocturno equivalente (unión a perfiles, `HAVING`): ventana de 180 días ≥ 500 → 20.044 miembros, CPU 8,5 s, **6,5 s**; de por vida ≥ 1.000 → 4.219 miembros, CPU 11,8 s, **13,2 s**. Lecturas: `CommissionEarnings` ~180.600, `MemberProfiles` ~85.000-88.700.

Tarea 6 (SQL del plan y de EF):

| Consulta | Filas | Lecturas lógicas | Servidor (3 corridas) |
|---|---|---|---|
| Histórica 180 días (umbrales 500 / 1.500 / 3.000) | 51.383 / 8 / 5 | `CommissionEarnings` ~182.200-182.500, `MemberProfiles` ~84.900-88.700 | CPU 30,2 / 22,8 / 29,5 s; **14,2 / 7,9 / 10,7 s** |
| Histórica de por vida (umbrales 1.000 / 2.000 / 4.000) | 4.219 / 7 / 5 | `CommissionEarnings` ~183.300-186.100, `MemberProfiles` ~87.800-88.700 | CPU 22,3 / 20,6 / 23,8 s; **6,2 / 7,1 / 9,7 s** |
| Nocturna 180 días (EF, ≥ 500) | 19.959 | `CommissionEarnings` ~185.800-196.100, `MemberProfiles` ~86.000-88.700 | CPU 9,6 / 10,4 / 9,5 s; **3,4 / 3,5 / 3,4 s** |
| Nocturna de por vida (EF, ≥ 500) | 54.513 | `CommissionEarnings` ~182.400-186.700, `MemberProfiles` 88.745 | CPU 11,9 / 14,8 / 16,3 s; **3,9 / 6,4 / 5,6 s** |

Notas (14-nov-2026, máquina con otras cargas: servicios `-run` y pruebas en curso):
- Una sola exploración de `CommissionEarnings` por consulta en las cuatro (Scan count 7 = grado de paralelismo, no 2×).
- La histórica: el `SELECT` de recuento sobre la misma CTE del plan (sin `INSERT`, niveles en `VALUES`, sin `IsCorporateAccount`); < 15 s por ventana, lejos del límite de 60 s. El nocturno: < 10 s las dos ventanas juntas (límite 30 s).
- Las nocturnas usan `@to` = mañana 00:00 de la fecha de la corrida (ventana de 180 días que termina hoy), por eso 19.959 frente a los 20.044 de la preparación; la de por vida se midió con el mínimo de 500 que indica el Step 5 (la preparación usó 1.000 → 4.219).
