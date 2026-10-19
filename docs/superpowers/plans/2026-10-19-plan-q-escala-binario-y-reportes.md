# Plan Q: escala del binario y reportes — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que lo que el Bloque D del Plan P midió en rojo con 400.000 y 1.000.000 de altas completas responda en menos de 2 s con 1.000.000 (y con 500.000, que es el punto que pidió Nick):
- el progreso de rango deja de agotarse (objetivo < 1 s con 1.000.000);
- colocar deja de subir por todos los ascendientes dentro de la transacción: 10 colocaciones profundas a la vez con p95 < 2 s;
- el binario deja de mantener `HierarchyPath` (23,5 GB con 400.000 y 65,8 GB con 1.000.000) y mover un subárbol de 50.000 baja unos 75 s;
- un alta deja de escribir tres filas por cada ascendiente de inscripción (29,4 millones por tabla con 1.000.000);
- Actividad, Clientes, Eventos, Mi equipo de inscripción y la búsqueda por nombre de Mi equipo del binario bajan de 2 s;
- una base vacía se puede construir con `dotnet ef database update`.

**Architecture:**
- **Bloque 1. Progreso de rango en SQL.** Los calificados del equipo de inscripción salen de una sola consulta (`CurrentUnder` unido a las suscripciones activas), como ya hace la parte binaria, con un índice filtrado de suscripciones activas.
- **Bloque 2. Cola de deltas de pierna (decisión P-R9).** Colocar inserta la hoja y una fila en `DualLegDeltas` (punto de arranque, lado, puntos y nodos). Un trabajo de Hangfire (`ApplyDualLegDeltasJob`, cada minuto, como `ApplyMemberStatisticDeltasJob`) agrega los deltas por ascendiente y los aplica de una vez: los ascendientes salen de la clave compacta con la función `dbo.DualAncestorKeys`, sin recursión. Mover o retirar encola el delta negativo en la línea vieja y el positivo en la nueva. Las comisiones y la instantánea vacían la cola antes de leer piernas.
- **Bloque 3. Sin `HierarchyPath` en el binario.** Los dos lectores que quedan (`SearchBinarySubtreeAsync` y `GetDeepestNodeAsync`) leen el camino por la clave. `DualTeamNodes` deja de escribir la ruta, la columna se elimina con una migración y el espacio se recupera por lotes con `DBCC CLEANTABLE`.
- **Bloque 4. Alta acotada.** Una sola fila de `MemberStatisticDeltas` por alta (`Scope = SponsorLine`), que el trabajo expande en SQL por los ascendientes del `Node` y aplica con un `MERGE` único. La cola de rango y el aviso `DOWNLINE_ENROLLED` llegan a la línea de patrocinio hasta 10 niveles (`GenealogyUpline.EnrollmentLevels`, la misma cota que I3 del Plan P).
- **Bloque 5. Reportes.**
  - Un solo nodo vivo por miembro en `GenealogyTree` (índice único filtrado con guarda), así que `CurrentUnder` deja de necesitar la antijoin.
  - Actividad agrupa en SQL con el nivel de alta guardado en `MemberProfiles.SignupMembershipLevelId`.
  - Clientes y Eventos dejan el `TOP 1` correlacionado con `IsDescendantOf` y unen con el rango.
  - Orden por estructura por defecto en Mi equipo y límite de orden y búsqueda libres por encima de 20.000 (decisión P7).
  - Búsqueda por prefijo sobre columnas calculadas persistidas e indexadas, porque Full-Text no está instalado (`FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 0`, comprobado el 2026-10-19).
- **Bloque 6. Migraciones desde cero.** `HelpdeskModule` marca sus operaciones como «solo si la tabla es nueva», y un generador de SQL de migraciones las envuelve en guardas. Una prueba crea una base vacía, la migra y la compara con el modelo.
- **Bloque 7. Medición final compartida.** Se genera una vez cada base de escala (500.000 y 1.000.000), se mide todo y se borra.

**Tech Stack:**
- .NET 10 y EF Core 10.0.11 sobre SQL Server 2022 Developer (local).
- Hangfire (trabajos recurrentes en `Repository/Jobs`).
- xUnit, FluentAssertions, EF InMemory, `[SqlServerFact]` (`BizCenter.Tests/Reportes/SqlServerReportes.cs`) y `[EscalaFact]` (`BizCenter.Tests/Binario/EscalaDb.cs`).
- `sqlcmd -b -I` para los scripts.
- Rama: `plan-q-escala`, desde `master` (be932cc).

---

## Decisiones tomadas en este plan

Nick pidió que el plan decida con criterio. Estas son las decisiones; cualquiera se puede vetar antes de su bloque.

- **DQ1. Cola de deltas (P-R9).** Se elige la cola, como recomendaba el Plan P. Las piernas pasan a ser **consistentes en diferido**, con hasta 1 minuto más el atraso de la cola. Los lectores que pagan dinero (residual diario, bono presidencial e instantánea de estadísticas) vacían la cola antes de leer, así que siguen siendo exactos.
- **DQ2. Semántica de un delta pendiente.** Un delta `(arranque S, lado s, puntos, nodos)` se aplica a la pierna `s` de S y a cada ascendiente **que S tenga cuando se aplica**, no cuando se encoló. Los movimientos encolan lo que leen de las piernas ya aplicadas. Con esas dos reglas el resultado tras vaciar la cola es el mismo que el de aplicarlo todo en el acto (demostración en la Tarea 6). El trabajador toma `dual-tree` compartido y los movimientos lo toman exclusivo, así que nunca se cruzan.
- **DQ3. Raíz provisional.** Colocar a B (raíz provisional con equipo) bajo A encola **un** delta: `+(propios de B + piernas de B)` con arranque en el padre nuevo. Es el mismo valor que hoy sube `ApplySubtreeAsync(+1)`.
- **DQ4. `HierarchyPath` del binario se elimina** (no se deja nula). Al terminar el Bloque 3 no queda ningún lector, así que mantenerla nula solo ocuparía sitio en el modelo.
- **DQ5. Cota de la línea de inscripción: 10 niveles** para la cola de rango del alta, la recolocación en cola que hace el trabajo de deltas, `DownstreamTriggers` y `DOWNLINE_ENROLLED`. Los ascendientes más lejanos se evalúan en la fase 2 de `RankEvaluationSweepJob` (cada noche evalúa a todos los embajadores activos). Ver Q1.
- **DQ6. Los contadores de inscripción siguen siendo exactos.** `EnrollmentPoints`, `EnrollmentTeamSize` y `QualifiedSponsoredMembers` reciben el mismo delta que hoy en cada ascendiente. Lo que cambia es que la expansión se hace en el trabajo y no en el alta.
- **DQ7. P7 (orden de Mi equipo).** El orden por defecto es por estructura: `Node` en inscripción y `BinaryPath` en el binario. Con un equipo de más de 20.000 (`TeamGridLimits.FreeMaxRows`), el orden pedido se sustituye por el de estructura y la búsqueda se limita a nombre o id por prefijo. La rejilla lo avisa (`PagedResult.Limited`).
- **DQ8. Búsqueda por nombre por prefijo de palabra**, sin distinguir tildes ni mayúsculas, sobre `SearchName` («nombre apellido») y `SearchNameReversed` («apellido nombre»). En equipos de 20.000 o menos se mantiene además la búsqueda actual por subcadena en todos los campos.
- **DQ9. Un solo nodo vivo por miembro en `GenealogyTree`.** En local hay 0 miembros con más de un nodo (119.446 filas y 119.446 miembros, consulta de solo lectura del 2026-10-19). La migración falla cerrada si hay repetidos, y el script `01` del Plan Q marca como borrados los más antiguos (Q2).
- **DQ10. Nivel de alta guardado.** `MemberProfiles.SignupMembershipLevelId` aplica la regla N2 del Plan J (el nivel más alto del primer pedido con productos de membresía). Lo escribe un único componente (`SignupMembershipLevel.RefreshAsync`) desde `SelectProductsHandler` y desde la activación, y un script lo rellena para los existentes.
- **DQ11. Migración `HelpdeskModule`.** Se reproduce en una base vacía lo que ocurrió en las bases reales con `fix_helpdesk_tables.sql`: solo se crean las tablas que no existían antes de esa migración. No se reescribe el historial y no cambia nada en una base que ya la tiene aplicada.
- **DQ12. Fuera del alcance, como riesgos residuales:**
  - la cola de derrame sin caché (punto 7 del Plan P): la caché es obligatoria y `04` la reconstruye;
  - un movimiento grande sigue tomando el árbol en exclusivo (punto 9 del Plan P): se mide cuánto bloquea tras los bloques 2 y 3;
  - las escrituras por ascendiente que no vienen del alta (`UplineAggregator`, `CancelMembershipHandler`, `ProcessScheduledCancellationsJob`).

| Bloque | Tareas | Entrega | Migración | Script |
|---|---|---|---|---|
| 0. Línea base | 0 | Medición con 500.000 antes de cambiar nada | — | — |
| 1. Progreso de rango | 1–3 | Una consulta e índice de activas | `SuscripcionesActivasPorMiembro` | — |
| 2. Cola de piernas | 4–10 | `DualLegDeltas`, `DualAncestorKeys`, trabajador y vaciados | `ColaDeDeltasDePierna` | — |
| 3. Sin ruta en el binario | 11–15 | Lectores por la clave y columna eliminada | `RetiraRutaDelBinario` | `03-recuperar-espacio-de-la-ruta.sql` |
| 4. Alta acotada | 16–20 | Un delta por alta y cota de 10 niveles | `DeltaPorLineaDePatrocinio` | — |
| 5. Reportes | 21–28 | Nodo único, Actividad, Clientes, Eventos, P7, búsqueda | `UnNodoPorMiembro`, `BusquedaYNivelDeAlta` | `01-nodos-de-inscripcion-repetidos.sql`, `02-nivel-de-alta.sql` |
| 6. Migraciones | 29–31 | Base vacía migrable y prueba | — | — |
| 7. Medición | 32–34 | Generador al día, 500.000 y 1.000.000 medidos | — | — |
| Cierre | 35 | Guion de despliegue y verificación | — | `00-despliegue-plan-q.md` |

**Reglas que se aplican en todo el plan** (las del Plan P, más las del Plan Q):
- **TDD.** Primero la prueba en rojo y después el mínimo código que la pone en verde.
  - Las pruebas `[SqlServerFact]` siembran con prefijo propio dentro de una transacción que se deshace.
  - Las de concurrencia limpian por prefijo en `finally` (`BinarioDb.LimpiarAsync`).
- **Hora del servidor:** `IDateTimeProvider.Now` en C# y `GETDATE()` en SQL. `UtcNow` solo donde un protocolo lo exige; ninguna tarea de este plan lo necesita. Las expresiones cron de Hangfire siguen con `TimeZone = Utc` como las vecinas: es la configuración del planificador, no un dato de negocio.
- **Commits:** en español, sin tildes, describiendo solo lo que se hizo, **sin** `Co-Authored-By` ni menciones de herramientas. `git add` con rutas explícitas.
- **Una sola base: la local** (`MLMConquerorGlobalEditionDb` en `.`). Excepciones:
  - la base de escala (`MLMConquerorGlobalEditionDb_Escala`), con sus cerrojos;
  - las dos bases desechables de la prueba de migraciones (`MLMConquerorGlobalEditionDb_DesdeCero` y `MLMConquerorGlobalEditionDb_Modelo`), que la prueba crea y borra y solo admite por esos nombres exactos.
  - **Nunca** la RDS comentada en `MLMConquerorGlobalEdition.Signups/appsettings.json`. `DesignTimeDbContextFactory` lee ese fichero: por eso todo `dotnet ef` lleva `--connection` explícito.
- **Secretos:** ninguno en `appsettings.json` base. Este plan no añade ninguno.
- **Componentes unificados.** Cada regla vive en un sitio:
  - `LegPointMaintenance` (encolar y aplicar piernas);
  - `DualBinaryPath` y `dbo.DualAncestorKeys` (ascendientes por la clave, con una prueba que exige que den lo mismo);
  - `GenealogyUpline` (cota de la línea de inscripción);
  - `SignupMembershipLevel` (nivel de alta);
  - `MemberNameSearch` (búsqueda por nombre);
  - `TeamGridLimits` (límites de rejilla).
  - En la interfaz, `ServerGridAdaptor` avisa del límite a las dos rejillas de Mi equipo.
- **Identidad:** los endpoints de Mi equipo y de reportes siguen tomando el miembro del token (`_currentUser.MemberId`). Ninguna tarea añade un `memberId` enviado por el cliente.
- **Migraciones:** se crean con `dotnet ef migrations add` y se aplican a mano con `--connection` antes de arrancar ningún servicio (los `Program.cs` hacen `MigrateAsync`).
- **Pruebas en serie:** las suites se ejecutan una detrás de otra, nunca dos `dotnet test` a la vez contra la base local.
- **Errores de los scripts y guardas:** del 51100 al 51199 (el Plan P llegó al 51075).

**Comandos:**
```bash
# pruebas (en serie)
dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter "FullyQualifiedName~DualBinaryPath|FullyQualifiedName~GenealogyUpline"
dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~Binario|FullyQualifiedName~Reportes|FullyQualifiedName~Rendimiento"
dotnet test MLMConquerorGlobalEdition.RankEngine.Tests
dotnet test MLMConquerorGlobalEdition.Signups.Tests
dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests
# migraciones
dotnet ef migrations add <Nombre> --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.AdminAPI
dotnet ef database update --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.AdminAPI \
  --connection "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True;"
# scripts locales
sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-q/<script>.sql
```

---

## Antes de empezar

### Tarea 0: línea base con 500.000 (código de `master`)

Nick pidió los tiempos de 500.000. Sin una medición previa no habría con qué comparar, así que se mide **antes del primer commit del plan**, con el código de be932cc. El esquema de la base de escala se clona del de la local, que está en `ClaveCompactaDelBinario`.

**Files:**
- Modify: `docs/superpowers/plans/2026-10-19-plan-q-escala-binario-y-reportes.md` (sección «Resultados medidos», columna «500.000 antes»)

- [ ] **Paso 1.** Generar (unos 45 min, interpolando entre 29 min con 400.000 y 2 h 40 con 1.000.000):

```powershell
git rev-parse HEAD   # debe ser be932cc o un commit solo de documentos
powershell -File Scripts/sql/plan-p/escala/01-generar-altas.ps1 -Nodos 500000 -Salida C:\temp\escala-500k-antes.txt -Recrear
```

- [ ] **Paso 2.** Medir con las tres suites de escala, en serie:

```powershell
$env:MLM_ESCALA_SQLSERVER = "Server=.;Database=MLMConquerorGlobalEditionDb_Escala;Integrated Security=true;TrustServerCertificate=True;Encrypt=True"
dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --settings MLMConquerorGlobalEdition.BizCenter.Tests/Medicion.runsettings --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
dotnet test MLMConquerorGlobalEdition.RankEngine.Tests --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
```

**Esperado:** los mismos rojos que con 400.000 y 1.000.000, con cifras intermedias.

- [ ] **Paso 3.** Copiar las líneas `[ESCALA] … -> mediana N ms` a la columna «500.000 antes» de «Resultados medidos» (Bloque 7). Borrar la base:

```powershell
sqlcmd -S . -d master -E -C -b -Q "ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE MLMConquerorGlobalEditionDb_Escala;"
```

- [ ] **Paso 4.** Commit (solo el plan): `Plan Q: linea base del binario y los reportes con 500.000 altas antes de los cambios`.

---

## Bloque 1: progreso de rango en SQL

`GetRankProgressHandler.cs:127-137` carga en memoria los `MemberId` de todo el equipo de inscripción (450.000 o 1.050.000) y los manda como lista a un `Contains`. Con 400.000 y con 1.000.000 se agota a los 30 s. La parte binaria (`:141-150`) ya es una sola consulta.

### Tarea 1: prueba en rojo

**Files:**
- Modify: `MLMConquerorGlobalEdition.RankEngine.Tests/MLMConquerorGlobalEdition.RankEngine.Tests.csproj:33-36` (enlazar los ayudantes de SQL Server)
- Create: `MLMConquerorGlobalEdition.RankEngine.Tests/Features/SqlServerProgresoDeRangoTests.cs`

- [ ] **Paso 1.** Enlazar los ayudantes, junto al enlace de `EscalaDb.cs` que ya existe:

```xml
  <!-- Plan Q: [SqlServerFact], la base local y la siembra de los reportes, en un solo sitio. -->
  <ItemGroup>
    <Compile Include="..\MLMConquerorGlobalEdition.BizCenter.Tests\Reportes\SqlServerReportes.cs" Link="SqlServer\SqlServerReportes.cs" />
    <Compile Include="..\MLMConquerorGlobalEdition.BizCenter.Tests\Reportes\ReportesDb.cs" Link="SqlServer\ReportesDb.cs" />
  </ItemGroup>
```

- [ ] **Paso 2.** La prueba. Fija el resultado (tres suscripciones activas en el equipo: A una y C dos; B cancelada; la del propio R no cuenta). Además exige que ningún parámetro mande la lista de miembros del equipo, que es lo que hoy hace el `Contains`: EF 10 la manda como un único parámetro JSON con todos los ids.

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

/// <summary>
/// Plan Q, Bloque 1: los calificados del equipo de inscripcion salen de UNA consulta (el rango del Node unido a las
/// suscripciones activas), no de una lista de ids en memoria que se agotaba con 450.000 (Plan P, Bloque D).
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerProgresoDeRangoTests
{
    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Guarda el valor de cada parametro de cada comando que EF manda.</summary>
    private sealed class Capturador : DbCommandInterceptor
    {
        public readonly List<string> Valores = new();

        private void Guardar(DbCommand c)
        {
            foreach (DbParameter p in c.Parameters) Valores.Add(Convert.ToString(p.Value) ?? string.Empty);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default)
        {
            Guardar(c);
            return ValueTask.FromResult(r);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default)
        {
            Guardar(c);
            return ValueTask.FromResult(r);
        }
    }

    [SqlServerFact]
    public async Task Los_calificados_del_equipo_salen_de_una_consulta_sin_la_lista_de_ids()
    {
        var cap = new Capturador();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(SqlServerReportes.ConnectionString).AddInterceptors(cap).Options);
        await using var tx = await db.Database.BeginTransactionAsync();

        var (r, a, b, c) = (SqlServerReportes.NuevoId("RQ"), SqlServerReportes.NuevoId("RQ"),
                            SqlServerReportes.NuevoId("RQ"), SqlServerReportes.NuevoId("RQ"));
        ReportesDb.Miembro(db, r);
        ReportesDb.Miembro(db, a, r);
        ReportesDb.Miembro(db, b, r);
        ReportesDb.Miembro(db, c, b);
        ReportesDb.Nodo(db, r, null, ReportesDb.Ruta(r), 1);
        ReportesDb.Nodo(db, a, r, ReportesDb.Ruta(r, a), 2);
        ReportesDb.Nodo(db, b, r, ReportesDb.Ruta(r, b), 2);
        ReportesDb.Nodo(db, c, b, ReportesDb.Ruta(r, b, c), 3);
        ReportesDb.Suscripcion(db, r, 2, MembershipStatus.Active, ReportesDb.Base);   // el propio no cuenta
        ReportesDb.Suscripcion(db, a, 2, MembershipStatus.Active, ReportesDb.Base);
        ReportesDb.Suscripcion(db, b, 2, MembershipStatus.Cancelled, ReportesDb.Base);
        ReportesDb.Suscripcion(db, c, 2, MembershipStatus.Active, ReportesDb.Base);
        ReportesDb.Suscripcion(db, c, 3, MembershipStatus.Active, ReportesDb.Base);   // se cuentan suscripciones, como hoy
        await db.SaveChangesAsync();
        cap.Valores.Clear();

        var handler = new GetRankProgressHandler(db, new Reloj(),
            new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db));
        var res = await handler.Handle(new GetRankProgressQuery(r), CancellationToken.None);

        res.IsSuccess.Should().BeTrue(res.Error);
        res.Value!.CurrentMetrics.EnrollmentQualifiedTeamMembers.Should().Be(3);
        cap.Valores.Should().NotContain(v => v.Contains(a) && v.Contains(c),
            "la pertenencia al equipo se resuelve en SQL por el rango del Node, no con la lista de ids");
        await tx.RollbackAsync();
    }
}
```

- [ ] **Paso 3.** `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests --filter FullyQualifiedName~SqlServerProgresoDeRango`. **Esperado:** falla la segunda aserción, porque el parámetro `@subtreeMembers` lleva `["…A…","…B…","…C…"]`.

### Tarea 2: una consulta y el índice de activas

**Files:**
- Modify: `MLMConquerorGlobalEdition.RankEngine/Features/GetRankProgress/GetRankProgressHandler.cs:122-138`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MembershipSubscriptionConfiguration.cs:13-19`
- Create: migración `SuscripcionesActivasPorMiembro`

- [ ] **Paso 1.** Sustituir las líneas 122-138 por:

```csharp
        if (memberGenealogyNode is not null)
        {
            // Plan Q: una sola consulta, el rango del Node (CurrentUnder) unido a las suscripciones activas, igual que la
            // parte binaria. Antes se cargaban en memoria los ids de todo el equipo para un Contains: con 450.000 se agotaba.
            // Se cuentan suscripciones activas (no miembros), como antes.
            enrollmentQualifiedCount = await (
                from g in _db.GenealogyTree.AsNoTracking().CurrentUnder(memberGenealogyNode.Node)
                where g.MemberId != memberId
                join s in _db.MembershipSubscriptions.AsNoTracking() on g.MemberId equals s.MemberId
                where s.SubscriptionStatus == Domain.Entities.Membership.MembershipStatus.Active
                select s.Id).CountAsync(ct);
        }
```

- [ ] **Paso 2.** Índice filtrado: las dos consultas (inscripción y binario) unen el equipo con las activas por `MemberId`. Hace falta un nombre propio, porque `IX_MembershipSubscriptions_MemberId` ya existe sobre la misma columna.

```csharp
        // Plan Q: las suscripciones activas por miembro, para unir el equipo (millones de filas) con ellas sin leer la tabla.
        builder.HasIndex(new[] { nameof(MembershipSubscription.MemberId) }, "IX_MembershipSubscriptions_Activas")
            .HasFilter("[SubscriptionStatus] = 1 AND [IsDeleted] = 0");
```

- [ ] **Paso 3.** Crear y aplicar la migración:

```bash
dotnet ef migrations add SuscripcionesActivasPorMiembro --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.AdminAPI
dotnet ef database update --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.AdminAPI --connection "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True;"
```

**Esperado:** el `Up` generado solo tiene `CreateIndex` con el filtro.

- [ ] **Paso 4.** `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests` en verde, incluidas las de `GetRankProgressHandlerTests` (InMemory).
- [ ] **Paso 5.** Commit: `Plan Q: el progreso de rango cuenta los calificados del equipo en una sola consulta con indice de suscripciones activas`.

### Tarea 3: medición rápida local y objetivo de escala a 1 s

**Files:**
- Modify: `MLMConquerorGlobalEdition.RankEngine.Tests/Features/SqlServerProgresoDeRangoTests.cs`
- Modify: `MLMConquerorGlobalEdition.RankEngine.Tests/Escala/EscalaProgresoDeRangoTests.cs:36`

- [ ] **Paso 1.** Medición rápida contra la base local. AMB-700829 tiene 119.403 nodos en su equipo binario y es la raíz mayor de inscripción. Va con el rasgo `Medicion` para que no corra en la suite de siempre:

```csharp
    [SqlServerFact]
    [Trait("Categoria", "Medicion")]
    public async Task El_progreso_de_la_raiz_mayor_local_responde_en_menos_de_un_segundo()
    {
        await using var db = SqlServerReportes.Db();
        var handler = new GetRankProgressHandler(db, new Reloj(),
            new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db));
        await handler.Handle(new GetRankProgressQuery("AMB-700829"), CancellationToken.None);   // calienta
        var t = System.Diagnostics.Stopwatch.StartNew();
        var r = await handler.Handle(new GetRankProgressQuery("AMB-700829"), CancellationToken.None);
        t.Stop();
        r.IsSuccess.Should().BeTrue(r.Error);
        t.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
```

- [ ] **Paso 2.** En `EscalaProgresoDeRangoTests.cs:36`, el objetivo pasa a 1 s (el que se fija para 1.000.000): `t.Should().BeLessThan(TimeSpan.FromSeconds(1));`. Se mide en el Bloque 7.
- [ ] **Paso 3.** `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests --filter "FullyQualifiedName~SqlServerProgresoDeRango"` en verde. Commit: `Plan Q: medicion local del progreso de rango y objetivo de un segundo a escala`.

---

## Bloque 2: cola de deltas de pierna (P-R9)

**Hoy:** `LegPointMaintenance.ApplyIncrementalAsync` (`LegPointMaintenance.cs:28-51`) sube por todos los ascendientes con una CTE recursiva y actualiza cada fila dentro de la transacción de la colocación (`PlacementService.cs:163`, `BizCenter PlaceMemberHandler.cs:177`, `AdminPlaceMemberHandler.cs:133`, `SignupAPI PlaceMemberHandler.cs:147`). Mover y retirar llaman a `ApplySubtreeAsync(±1)` (`DualTeamNodes.cs:137`, `:145`, `:175`).

**Después:**
- Las seis llamadas **encolan** una fila en `DualLegDeltas`: O(1), sin tocar la línea.
- `ApplyDualLegDeltasJob` vacía la cola cada minuto.
- `DrainAsync` también lo llaman el residual diario, el bono presidencial y la instantánea, antes de leer.

**Por qué los ascendientes salen de la clave y no de la CTE.** Con N arranques distintos en la misma línea de 8.500 niveles, la CTE hace N × 8.500 búsquedas por índice. `dbo.DualAncestorKeys` calcula las claves de los ascendientes con aritmética de bytes, sin leer la tabla. Después se agrupan, y solo se buscan las claves distintas, unos 8.500 *seeks*, una vez por tanda.

### Tarea 4: `DualBinaryPath.Ancestor` y `SideBelow` (C#)

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/Services/DualBinaryPath.cs:100-103` (después de `Rebase`)
- Modify: `MLMConquerorGlobalEdition.Domain.Tests/DualBinaryPathTests.cs` (o el fichero de pruebas de `DualBinaryPath` que exista; `grep -l "DualBinaryPath" MLMConquerorGlobalEdition.Domain.Tests/*.cs`)

- [ ] **Paso 1.** Prueba en rojo:

```csharp
    [Fact]
    public void Ancestor_y_SideBelow_reconstruyen_el_camino_de_cualquier_clave()
    {
        var rnd = new Random(20261019);
        for (var caso = 0; caso < 200; caso++)
        {
            var claves = new List<byte[]> { DualBinaryPath.Root(rnd.Next(1, int.MaxValue)) };
            var lados = new List<TreeSide>();
            var prof = rnd.Next(0, 3000);
            for (var i = 0; i < prof; i++)
            {
                var lado = rnd.Next(2) == 0 ? TreeSide.Left : TreeSide.Right;
                lados.Add(lado);
                claves.Add(DualBinaryPath.Child(claves[^1], lado));
            }
            var hoja = claves[^1];
            for (var j = 0; j <= prof; j++)
            {
                DualBinaryPath.Ancestor(hoja, j).Should().Equal(claves[j]);
                if (j < prof) DualBinaryPath.SideBelow(hoja, j).Should().Be(lados[j]);
            }
        }
    }
```

- [ ] **Paso 2.** `dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter FullyQualifiedName~DualBinaryPath`. **Esperado:** no compila (no existen los métodos).

- [ ] **Paso 3.** Implementación:

```csharp
    /// <summary>
    /// Plan Q: la clave del ascendiente de <paramref name="key"/> a profundidad <paramref name="depth"/> (0 = la raiz,
    /// Depth(key) = la propia clave): los bits de camino hasta ahi y el terminador. Es lo mismo que calcula
    /// dbo.DualAncestorKeys en SQL; SqlServerAscendientesPorClaveTests exige que coincidan.
    /// </summary>
    public static byte[] Ancestor(ReadOnlySpan<byte> key, int depth)
    {
        var d = Depth(key);
        if (depth < 0 || depth > d) throw new ArgumentOutOfRangeException(nameof(depth));
        var k = key[..(RootBytes + depth / 8 + 1)].ToArray();
        var m = depth % 8;
        k[^1] = (byte)((k[^1] & (0xFF00 >> m)) | (0x80 >> m));   // conserva los m bits de arriba y pone el terminador
        return k;
    }

    /// <summary>Plan Q: el lado por el que baja el camino de <paramref name="key"/> desde su ascendiente de profundidad <paramref name="depth"/>.</summary>
    public static TreeSide SideBelow(ReadOnlySpan<byte> key, int depth)
    {
        if (depth < 0 || depth >= Depth(key)) throw new ArgumentOutOfRangeException(nameof(depth));
        return GetBit(key, depth) ? TreeSide.Right : TreeSide.Left;
    }
```

- [ ] **Paso 4.** En verde. Commit: `Plan Q: claves de los ascendientes y lado de bajada a partir de la clave compacta`.

### Tarea 5: tabla `DualLegDeltas` y funciones `dbo.Numeros` y `dbo.DualAncestorKeys`

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Tree/DualLegDelta.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/DualLegDeltaConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs:82` (DbSet junto a `DualTeamLegFrontiers`)
- Create: migración `ColaDeDeltasDePierna`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerAscendientesPorClaveTests.cs`

- [ ] **Paso 1.** Prueba en rojo. La función SQL y `DualBinaryPath` dan las mismas claves y lados, incluido el último nivel, que lleva el lado del propio delta:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerAscendientesPorClaveTests
{
    private sealed record Fila(int Depth, byte[] AncestorKey, int Side);

    [SqlServerFact]
    public async Task DualAncestorKeys_coincide_con_DualBinaryPath()
    {
        await using var db = SqlServerReportes.Db();
        var rnd = new Random(1019);
        foreach (var prof in new[] { 0, 1, 7, 8, 9, 15, 16, 17, 500, 8507 })
        {
            var k = DualBinaryPath.Root(rnd.Next(1, int.MaxValue));
            for (var i = 0; i < prof; i++) k = DualBinaryPath.Child(k, rnd.Next(2) == 0 ? TreeSide.Left : TreeSide.Right);
            var propio = rnd.Next(2);

            var filas = await db.Database
                .SqlQuery<Fila>($"SELECT Depth, AncestorKey, Side FROM dbo.DualAncestorKeys({k}, {propio}) ORDER BY Depth")
                .ToListAsync();

            filas.Should().HaveCount(prof + 1);
            for (var j = 0; j <= prof; j++)
            {
                filas[j].Depth.Should().Be(j);
                filas[j].AncestorKey.Should().Equal(DualBinaryPath.Ancestor(k, j));
                filas[j].Side.Should().Be(j == prof ? propio : (int)DualBinaryPath.SideBelow(k, j));
            }
        }
    }
}
```

- [ ] **Paso 2.** Ejecutar. **Esperado:** falla con `Invalid object name 'dbo.DualAncestorKeys'`.

- [ ] **Paso 3.** Entidad (sin `AuditChanges`: la escribe SQL en bloque y la borra el trabajador; no necesita auditoría ni borrado lógico):

```csharp
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

/// <summary>
/// Plan Q (P-R9): un cambio pendiente de las piernas del binario. Se suma a la pierna <see cref="Side"/> de
/// <see cref="StartMemberId"/> y, por el lado por el que se baja, a cada ascendiente que ese nodo tenga CUANDO SE APLICA.
/// Lo escribe y lo aplica LegPointMaintenance; ApplyDualLegDeltasJob vacia la cola y la borra.
/// </summary>
public class DualLegDelta
{
    public long Id { get; set; }
    public string StartMemberId { get; set; } = string.Empty;
    public TreeSide Side { get; set; }
    public decimal Points { get; set; }
    public int NodeCount { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class DualLegDeltaConfiguration : IEntityTypeConfiguration<DualLegDelta>
{
    public void Configure(EntityTypeBuilder<DualLegDelta> builder)
    {
        builder.ToTable("DualLegDeltas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartMemberId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Points).HasPrecision(18, 4);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
    }
}
```

En `AppDbContext.cs`, junto a la línea 82:

```csharp
    public DbSet<DualLegDelta> DualLegDeltas => Set<DualLegDelta>();
```

- [ ] **Paso 4.** `dotnet ef migrations add ColaDeDeltasDePierna …`. Al final del `Up` generado, añadir las dos funciones, y en el `Down`, `DROP FUNCTION IF EXISTS dbo.DualAncestorKeys; DROP FUNCTION IF EXISTS dbo.Numeros;` antes del `DropTable`. `dbo.Numeros` es una tabla de números en línea, sin datos, así que funciona igual en una base clonada con `DBCC CLONEDATABASE` (que no copia datos) y en una base nueva.

```csharp
            // Plan Q: 0..@hasta sin tabla (100.000 como maximo; la clave admite 13.567 niveles).
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.Numeros(@hasta int)
RETURNS TABLE AS RETURN
WITH e1(n) AS (SELECT 1 FROM (VALUES (1),(1),(1),(1),(1),(1),(1),(1),(1),(1)) v(n)),
     e5(n) AS (SELECT 1 FROM e1 a CROSS JOIN e1 b CROSS JOIN e1 c CROSS JOIN e1 d CROSS JOIN e1 e)
SELECT TOP (CASE WHEN @hasta IS NULL OR @hasta < 0 THEN 0 ELSE @hasta + 1 END)
       CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS int) AS N
FROM e5;");

            // Plan Q: las claves de los ascendientes de @k (profundidad 0..D, D = la propia clave) y el lado por el que se
            // baja desde cada uno; en D, el lado del delta (@ladoPropio). Mismo algoritmo que DualBinaryPath.Ancestor y
            // SideBelow (SqlServerAscendientesPorClaveTests). Byte (base 1) del bit j: 5 + j/8; bit dentro del byte: j%8.
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.DualAncestorKeys(@k varbinary(1700), @ladoPropio int)
RETURNS TABLE AS RETURN
WITH t AS (
    SELECT DATALENGTH(@k) AS L, CAST(SUBSTRING(@k, DATALENGTH(@k), 1) AS int) AS U),
d AS (
    SELECT (t.L - 5) * 8 + 7 - CASE WHEN t.U & 1 > 0 THEN 0 WHEN t.U & 2 > 0 THEN 1 WHEN t.U & 4 > 0 THEN 2
                                    WHEN t.U & 8 > 0 THEN 3 WHEN t.U & 16 > 0 THEN 4 WHEN t.U & 32 > 0 THEN 5
                                    WHEN t.U & 64 > 0 THEN 6 ELSE 7 END AS D
    FROM t WHERE t.L >= 5),
b AS (
    SELECT n.N AS J, d.D, CAST(SUBSTRING(@k, 5 + n.N / 8, 1) AS int) AS B, n.N % 8 AS M
    FROM d CROSS APPLY dbo.Numeros(d.D) n)
SELECT b.J AS Depth,
       CAST(SUBSTRING(@k, 1, 4 + b.J / 8)
            + CAST(CAST((b.B & (256 - POWER(2, 8 - b.M))) | POWER(2, 7 - b.M) AS binary(1)) AS varbinary(1))
            AS varbinary(1700)) AS AncestorKey,
       CASE WHEN b.J = b.D THEN @ladoPropio
            WHEN b.B & POWER(2, 7 - b.M) <> 0 THEN 1 ELSE 0 END AS Side
FROM b;");
```

- [ ] **Paso 5.** Aplicar la migración en local (`dotnet ef database update … --connection …`). La prueba del Paso 1 pasa a verde. Commit: `Plan Q: cola de deltas de pierna y ascendientes por la clave compacta en SQL`.

### Tarea 6: `LegPointMaintenance` encola y aplica

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/LegPointMaintenance.cs` (se reescribe; se conserva `ApplyInMemoryAsync`, líneas 124-147)
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/DualTeamGate.cs:57-64` (nuevo `EnterLegDrainAsync`)
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerColaDePiernasTests.cs`

**Por qué el resultado tras vaciar es exacto (DQ2).** Sea V el valor verdadero de las piernas si todo se hubiera aplicado en el acto, y M el valor guardado más los deltas pendientes, cada uno aplicado por los ascendientes que tenga su arranque cuando se aplique.
- Una colocación encola `+(p, 1)` en su padre: tras vaciar, M = V por la ruta actual.
- Un movimiento del subárbol R lee las piernas **guardadas** de R (que no incluyen sus pendientes internos), encola `−T` en el padre viejo y `+T` en el nuevo, y cambia la clave de todo R.
  - Los pendientes internos de R se aplicarán subiendo por la ruta nueva: a la línea vieja nunca llegan y la nueva los recibe. Línea vieja: `−T`, como en V (que habría sumado el pendiente v y restado T + v). Línea nueva: `T + v`, como en V.
  - El mismo argumento vale para un `−T` pendiente cuyo arranque se mueve después.
- Una retirada encola `−T` en el padre y borra el nodo. Un pendiente con arranque en el nodo borrado no encuentra fila y se descarta: la línea vieja queda en `−T`, como en V.
- La condición es que trabajador y movimientos no se crucen: el trabajador toma `dual-tree` **compartido** y los movimientos y retiradas lo toman **exclusivo**. Las colocaciones también lo toman compartido y ya no tocan la línea.

- [ ] **Paso 1.** Pruebas en rojo (`SqlServerColaDePiernasTests`). Usan `BinarioDb`, con prefijo propio y limpieza en `finally`, porque el vaciado confirma sus propias transacciones.

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>Plan Q (P-R9): colocar encola; el vaciado deja las piernas exactas, con movimientos y retiradas por medio.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerColaDePiernasTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [SqlServerFact]
    public async Task Colocar_no_toca_la_linea_y_el_vaciado_la_sube()
    {
        var x = BinarioDb.Prefijo("PQC");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));
                await LegPointMaintenance.DrainAsync(db);
                await BinarioDb.SembrarPerfilAsync(db, $"{x}N", $"{x}L");
                await BinarioDb.PuntosAsync(db, $"{x}N", 25);
                (await new PlacementService(db, new Reloj()).PlaceAsync($"{x}N", $"{x}L")).IsSuccess.Should().BeTrue();
            }
            await using (var db = SqlServerReportes.Db())
            {
                var p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");
                p.LeftLegCount.Should().Be(1, "la colocacion solo encola");
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}L")).Should().Be(1);

                await LegPointMaintenance.DrainAsync(db);

                p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");
                (p.LeftLegCount, p.LeftLegPoints).Should().Be((2, 25m));
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}L")).Should().Be(0);
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    /// <summary>
    /// DQ2: colocaciones con deltas pendientes DENTRO de una raiz provisional que despues se coloca, y una retirada con
    /// hijos flotantes con pendientes. Tras vaciar, cada pierna es igual al recuento de su rango.
    /// </summary>
    [SqlServerFact]
    public async Task Con_movimientos_y_retiradas_por_medio_el_vaciado_deja_las_piernas_exactas()
    {
        var x = BinarioDb.Prefijo("PQD");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));
                await LegPointMaintenance.DrainAsync(db);
                foreach (var (id, pat, pts) in new[] { ("A", "P", 0), ("B", "A", 10), ("C", "B", 20), ("D", "C", 40), ("E", "B", 5), ("F", "E", 7) })
                {
                    await BinarioDb.SembrarPerfilAsync(db, $"{x}{id}", $"{x}{pat}");
                    if (pts > 0) await BinarioDb.PuntosAsync(db, $"{x}{id}", pts);
                }
            }
            await using (var db = SqlServerReportes.Db())
            {
                var motor = new PlacementService(db, new Reloj());
                // B sin colocar: C, D, E y F cuelgan de la raiz provisional B, con sus deltas SIN aplicar.
                foreach (var (id, pat) in new[] { ("C", "B"), ("D", "C"), ("E", "B"), ("F", "E") })
                    (await motor.PlaceAsync($"{x}{id}", $"{x}{pat}")).IsSuccess.Should().BeTrue();
                // Se coloca B bajo A con pendientes dentro (DQ3: un delta con lo aplicado de B).
                (await motor.PlaceAsync($"{x}B", $"{x}A")).IsSuccess.Should().BeTrue();
            }
            await using (var db = SqlServerReportes.Db())
            {
                // Retirada de E con su hijo F flotando, sin haber vaciado.
                await using var tx = await db.Database.BeginTransactionAsync();
                await DualTeamGate.EnterTreeExclusiveAsync(db);
                (await DualTeamNodes.RemoveAsync(db, $"{x}E", DualTeamChildrenOnRemoval.Floating, DateTime.Now, "plan-q-prueba")).Should().BeTrue();
                await tx.CommitAsync();
            }
            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                await PiernasCoherentesAsync(db, x);
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    /// <summary>Cada nodo vivo del prefijo: sus contadores y puntos de pierna = el recuento de su rango por la clave.</summary>
    public static async Task PiernasCoherentesAsync(AppDbContext db, string prefijo)
    {
        var nodos = await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(prefijo)).ToListAsync();
        foreach (var n in nodos)
            foreach (var lado in new[] { TreeSide.Left, TreeSide.Right })
            {
                var ids = await DualSubtree.Leg(db, n, lado).AsNoTracking().Select(d => d.MemberId).ToListAsync();
                var pts = await db.MemberStatistics.AsNoTracking().Where(s => ids.Contains(s.MemberId))
                    .GroupBy(s => s.MemberId).Select(g => g.OrderBy(s => s.Id).First().PersonalPoints).SumAsync();
                var (c, p) = lado == TreeSide.Left ? (n.LeftLegCount, n.LeftLegPoints) : (n.RightLegCount, n.RightLegPoints);
                (c, p).Should().Be((ids.Count, (decimal)pts), $"{n.MemberId} pierna {lado}");
            }
    }
}
```

Añadir a `BinarioDb.LimpiarAsync` (`BinarioDb.cs:72`) el borrado de los deltas del prefijo, antes del de `DualTeamTree`:

```csharp
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualLegDeltas WHERE StartMemberId LIKE {patron}");
```

- [ ] **Paso 2.** Ejecutar. **Esperado:** no compila (no existen `DrainAsync` ni `DualLegDeltas` en las pruebas).

- [ ] **Paso 3.** `DualTeamGate`, después de `EnterTreeExclusiveAsync`:

```csharp
    public const string LegDrainResource = "dual-leg-deltas";

    /// <summary>
    /// Plan Q: el vaciado de la cola de piernas. "dual-tree" compartido (ningun movimiento a la vez: DQ2) y
    /// "dual-leg-deltas" exclusivo (un solo vaciador: el trabajo o una comision que vacia antes de leer).
    /// </summary>
    public static async Task EnterLegDrainAsync(AppDbContext db, CancellationToken ct = default,
        int lockTimeoutMs = DefaultLockTimeoutMs)
    {
        if (!Applies(db)) return;
        await LockAsync(db, TreeResource, "Shared", lockTimeoutMs, ct);
        await LockAsync(db, LegDrainResource, "Exclusive", lockTimeoutMs, ct);
    }
```

- [ ] **Paso 4.** `LegPointMaintenance` (se sustituyen las líneas 9-122; `ApplyInMemoryAsync` queda igual):

```csharp
/// <summary>
/// Piernas del binario (LeftLegPoints/RightLegPoints, LeftLegCount/RightLegCount y la copia MemberStatistics.DualTeamPoints).
/// Plan Q (P-R9): quien escribe el binario ENCOLA un delta en DualLegDeltas (O(1), sin tocar la linea ascendente) y
/// DrainAsync los aplica por tandas, agregados por ascendiente. Los ascendientes salen de la clave con
/// dbo.DualAncestorKeys, sin recursion. Semantica (DQ2): un delta se aplica por los ascendientes que su arranque tenga
/// AL APLICARSE; los movimientos encolan lo que leen de las piernas ya aplicadas. En InMemory se aplica en el acto.
/// </summary>
public static class LegPointMaintenance
{
    /// <summary>Arranques distintos por tanda: con la linea de 8.500 niveles, 200 × 8.500 = 1,7 millones de filas calculadas.</summary>
    public const int BatchSize = 200;

    /// <summary>Encola la hoja recien guardada: sus PersonalPoints y 1 nodo, en la pierna de su padre.</summary>
    public static async Task EnqueueLeafAsync(AppDbContext db, string memberId, DateTime now, string by, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational()) { await ApplyInMemoryAsync(db, memberId, leaf: true, sign: 1, ct); return; }
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO DualLegDeltas (StartMemberId, Side, Points, NodeCount, CreationDate, CreatedBy)
SELECT d.ParentMemberId, d.Side,
       ISNULL((SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id), 0),
       1, {now}, {by}
FROM DualTeamTree d
WHERE d.MemberId = {memberId} AND d.IsDeleted = 0 AND d.ParentMemberId IS NOT NULL;", ct);
    }

    /// <summary>
    /// Encola el subarbol de <paramref name="subtreeRootId"/> entero (sus propios puntos mas sus dos piernas YA APLICADAS, y
    /// 1 + sus contadores) con el signo dado, en la pierna de su padre ACTUAL. -1 antes de soltarlo y +1 despues de colgarlo.
    /// </summary>
    public static async Task EnqueueSubtreeAsync(AppDbContext db, string subtreeRootId, int sign, DateTime now, string by,
        CancellationToken ct = default)
    {
        if (sign is not (1 or -1)) throw new ArgumentOutOfRangeException(nameof(sign));
        if (!db.Database.IsRelational()) { await ApplyInMemoryAsync(db, subtreeRootId, leaf: false, sign, ct); return; }
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO DualLegDeltas (StartMemberId, Side, Points, NodeCount, CreationDate, CreatedBy)
SELECT d.ParentMemberId, d.Side,
       {sign} * (ISNULL((SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id), 0)
                 + d.LeftLegPoints + d.RightLegPoints),
       {sign} * (1 + d.LeftLegCount + d.RightLegCount), {now}, {by}
FROM DualTeamTree d
WHERE d.MemberId = {subtreeRootId} AND d.IsDeleted = 0 AND d.ParentMemberId IS NOT NULL;", ct);
    }

    /// <summary>
    /// Aplica la cola hasta vaciarla (o hasta <paramref name="maxBatches"/> tandas) y devuelve cuantos deltas aplico. Cada
    /// tanda va en su transaccion; si quien llama ya tiene una (pruebas), trabaja dentro de ella sin confirmar.
    /// </summary>
    public static async Task<int> DrainAsync(AppDbContext db, int maxBatches = int.MaxValue, CancellationToken ct = default,
        int lockTimeoutMs = DualTeamGate.DefaultLockTimeoutMs)
    {
        if (!db.Database.IsRelational()) return 0;
        var total = 0;
        for (var i = 0; i < maxBatches; i++)
        {
            var propia = db.Database.CurrentTransaction is null;
            await using var tx = propia ? await db.Database.BeginTransactionAsync(ct) : null;
            await DualTeamGate.EnterLegDrainAsync(db, ct, lockTimeoutMs);
            var n = (await db.Database.SqlQueryRaw<int>(ApplyBatchSql, new SqlParameter("@lote", BatchSize)).ToListAsync(ct)).Single();
            if (tx is not null) await tx.CommitAsync(ct);
            total += n;
            if (n == 0) break;
        }
        return total;
    }

    // Una tanda: se reclaman (DELETE … OUTPUT) los deltas de hasta @lote arranques, se agregan por arranque y lado, se
    // expanden a sus ascendientes por la clave, se agregan por ascendiente y se aplican con un UPDATE.
    private const string ApplyBatchSql = @"
SET NOCOUNT ON;
CREATE TABLE #lote (StartMemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL, Side int NOT NULL,
                    Points decimal(18,4) NOT NULL, NodeCount int NOT NULL);
DELETE q
OUTPUT deleted.StartMemberId, deleted.Side, deleted.Points, deleted.NodeCount INTO #lote
FROM DualLegDeltas q
WHERE q.StartMemberId IN (SELECT TOP (@lote) StartMemberId FROM DualLegDeltas GROUP BY StartMemberId ORDER BY MIN(Id));

SELECT a.AncestorKey, a.Side, SUM(l.Points) AS P, SUM(l.NodeCount) AS C
INTO #deltas
FROM (SELECT StartMemberId, Side, SUM(Points) AS Points, SUM(NodeCount) AS NodeCount FROM #lote GROUP BY StartMemberId, Side) l
JOIN DualTeamTree s ON s.MemberId = l.StartMemberId AND s.IsDeleted = 0 AND s.BinaryPath IS NOT NULL
CROSS APPLY dbo.DualAncestorKeys(s.BinaryPath, l.Side) a
GROUP BY a.AncestorKey, a.Side
OPTION (MAXDOP 1);

UPDATE d
SET LeftLegPoints  = d.LeftLegPoints  + x.LP,
    RightLegPoints = d.RightLegPoints + x.RP,
    LeftLegCount   = d.LeftLegCount   + x.LC,
    RightLegCount  = d.RightLegCount  + x.RC
FROM DualTeamTree d
JOIN (SELECT AncestorKey,
             SUM(CASE WHEN Side = 0 THEN P ELSE 0 END) AS LP, SUM(CASE WHEN Side = 1 THEN P ELSE 0 END) AS RP,
             SUM(CASE WHEN Side = 0 THEN C ELSE 0 END) AS LC, SUM(CASE WHEN Side = 1 THEN C ELSE 0 END) AS RC
      FROM #deltas GROUP BY AncestorKey) x
  ON d.BinaryPath = x.AncestorKey
WHERE d.IsDeleted = 0 AND d.BinaryPath IS NOT NULL
OPTION (MAXDOP 1);

UPDATE s
SET DualTeamPoints = CAST(d.LeftLegPoints + d.RightLegPoints AS int)
FROM MemberStatistics s
JOIN DualTeamTree d ON d.MemberId = s.MemberId AND d.IsDeleted = 0
WHERE d.BinaryPath IN (SELECT AncestorKey FROM #deltas)
OPTION (MAXDOP 1);

SELECT COUNT(*) AS [Value] FROM #lote;
DROP TABLE #deltas; DROP TABLE #lote;";
```

(`using Microsoft.Data.SqlClient;` ya está en el fichero.) Los deltas de un mismo arranque se reclaman todos en la misma tanda: así una tanda nunca deja la mitad de un arranque para la siguiente, y el número de arranques, que es lo que cuesta (`Σ profundidad`), queda acotado por `BatchSize`.

- [ ] **Paso 5.** `BinarioDb.SembrarAsync` (`BinarioDb.cs:30-32`) encola cada hoja y vacía, para que las piernas queden igual que antes:

```csharp
        // Las piernas (puntos y contadores) coherentes con lo sembrado: se encola cada hoja y se vacia la cola.
        foreach (var (id, padre, _) in nodos.Where(n => n.Padre is not null))
            await Repository.Services.Teams.LegPointMaintenance.EnqueueLeafAsync(db, id, DateTime.Now, "plan-p-prueba");
        await Repository.Services.Teams.LegPointMaintenance.DrainAsync(db);
```

- [ ] **Paso 6.** Commit (las pruebas siguen en rojo hasta la Tarea 7): `Plan Q: LegPointMaintenance encola los cambios de pierna y los aplica por tandas agregadas por ascendiente`.

### Tarea 7: los seis escritores encolan

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/PlacementService.cs:162-163`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Features/Placement/PlaceMember/PlaceMemberHandler.cs:177`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Features/Placement/AdminPlaceMember/AdminPlaceMemberHandler.cs:133`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Placement/Commands/PlaceMember/PlaceMemberHandler.cs:147`
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/DualTeamNodes.cs:137`, `:145`, `:175`

- [ ] **Paso 1.** En las cuatro colocaciones de hoja, `ApplyIncrementalAsync(_db, new[] { memberId }, ct)` pasa a:

```csharp
            // Plan Q (P-R9): solo se encola; ApplyDualLegDeltasJob sube los puntos y contadores por la linea.
            await LegPointMaintenance.EnqueueLeafAsync(_db, memberId, _clock.Now, audit.CreatedBy, ct);
```

(En cada manejador se usan su reloj y su actor: `_dateTime.Now`, y el correo o id del administrador que ya firma su `PlacementLog`.)

- [ ] **Paso 2.** En `DualTeamNodes.MoveSubtreeAsync` (`:137` y `:145`) y `RemoveAsync` (`:175`):

```csharp
            await LegPointMaintenance.EnqueueSubtreeAsync(db, rootId, -1, now, by, ct);   // :137
            await LegPointMaintenance.EnqueueSubtreeAsync(db, rootId, +1, now, by, ct);   // :145
            await LegPointMaintenance.EnqueueSubtreeAsync(db, memberId, -1, now, by, ct); // :175
```

- [ ] **Paso 3.** Retirar `ApplyIncrementalAsync` y `ApplySubtreeAsync` (ya no tienen usos: `grep -rn "ApplyIncrementalAsync\|ApplySubtreeAsync" --include=*.cs` solo debe dar este plan).
- [ ] **Paso 4.** Las pruebas de SQL Server que leen piernas después de escribir vacían antes de leer. Es una línea, `await LegPointMaintenance.DrainAsync(db);`, delante de la lectura, en:
  - `BizCenter.Tests/Binario/SqlServerRaizProvisionalTests.cs`
  - `BizCenter.Tests/Binario/SqlServerFlotantesTests.cs`
  - `BizCenter.Tests/Rendimiento/SqlServerMedicionPlanPTests.cs`
  - `AdminAPI.Tests/Features/Placement/SqlServerFlotantesTests.cs`
  - `AdminAPI.Tests/Controllers/SqlServerVisualizadorBinarioTests.cs`
  - `Signups.Tests/Features/Placement/SqlServerFlotantesTests.cs`
  - `BizCenter.Tests/Binario/SqlServerClaveCompactaTests.cs` (la prueba de contadores, «tras colocar una hoja»)

  Lista exacta: `grep -rln "LegPoints\|LegCount\|DualTeamPoints" --include=*.cs *Tests* | xargs grep -l "SqlServerFact"`. Las de InMemory no cambian: allí se aplica en el acto.
- [ ] **Paso 5.** `dotnet test` de BizCenter.Tests (`Binario`), AdminAPI.Tests y Signups.Tests, en serie. **Esperado:** todo en verde, incluidas las dos de `SqlServerColaDePiernasTests`.
- [ ] **Paso 6.** Commit: `Plan Q: las colocaciones, los movimientos y las retiradas del binario encolan sus cambios de pierna`.

### Tarea 8: `ApplyDualLegDeltasJob`

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Jobs/ApplyDualLegDeltasJob.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs:262` (DI) y `:456-460` (registro, después de `auto-placement`)
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/ApplyDualLegDeltasJobTests.cs`

- [ ] **Paso 1.** Prueba en rojo (InMemory). El trabajo no falla sin proveedor relacional y devuelve 0:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Jobs;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

public class ApplyDualLegDeltasJobTests
{
    [Fact]
    public async Task En_InMemory_no_hay_cola_y_termina_sin_error()
    {
        await using var db = ReportesDb.Nueva();
        var job = new ApplyDualLegDeltasJob(db, NullLogger<ApplyDualLegDeltasJob>.Instance);
        (await job.ExecuteAsync()).Should().Be(0);
    }
}
```

- [ ] **Paso 2.** Ejecutar. **Esperado:** no compila.

- [ ] **Paso 3.** El trabajo. Vive en `Repository` por la misma razón que `ApplyMemberStatisticDeltasJob`: todos los planificadores de Hangfire deben poder cargar su tipo. Corre en la cola `bizcenter`, la del servicio que coloca (`AutoPlacementJob`).

```csharp
using Hangfire;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.Repository.Jobs;

/// <summary>
/// Plan Q (P-R9): vacia DualLegDeltas cada minuto (LegPointMaintenance.DrainAsync). Un solo vaciador: el atributo evita
/// dos ejecuciones del trabajo, y el applock "dual-leg-deltas" lo excluye de las comisiones que vacian antes de leer.
/// </summary>
[Queue("bizcenter")]
public class ApplyDualLegDeltasJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<ApplyDualLegDeltasJob> _logger;

    public ApplyDualLegDeltasJob(AppDbContext db, ILogger<ApplyDualLegDeltasJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var n = await LegPointMaintenance.DrainAsync(_db, ct: ct);
        if (n > 0) _logger.LogInformation("ApplyDualLegDeltas: {Count} deltas de pierna aplicados.", n);
        return n;
    }
}
```

`Program.cs` de BizCenter:

```csharp
builder.Services.AddScoped<ApplyDualLegDeltasJob>();   // junto a AutoPlacementJob (:262)

// Plan Q (P-R9): las piernas del binario se aplican desde la cola cada minuto.
RecurringJob.AddOrUpdate<ApplyDualLegDeltasJob>(
    "apply-dual-leg-deltas",
    job => job.ExecuteAsync(CancellationToken.None),
    "* * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "bizcenter" });
```

- [ ] **Paso 4.** En verde. Commit: `Plan Q: trabajo recurrente que vacia la cola de piernas del binario`.

### Tarea 9: los lectores que pagan vacían antes de leer

**Files:**
- Modify: `MLMConquerorGlobalEdition.CommissionEngine/Features/CalculateDailyResidual/CalculateDailyResidualHandler.cs:34-37`
- Modify: `MLMConquerorGlobalEdition.CommissionEngine/Features/CalculatePresidentialBonus/CalculatePresidentialBonusHandler.cs:29-32`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Jobs/MemberStatisticSnapshotJob.cs:51-53`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerVaciarAntesDeLeerTests.cs`

- [ ] **Paso 1.** Prueba en rojo. Con un delta pendiente sobre un nodo, `CalculateDailyResidualHandler` lee `DualTeamPoints` ya aplicado. Se comprueba mirando la cola, que el manejador deja vacía para ese arranque:

```csharp
    [SqlServerFact]
    public async Task El_residual_diario_vacia_la_cola_antes_de_calcular()
    {
        var x = BinarioDb.Prefijo("PQV");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));
                db.DualLegDeltas.Add(new DualLegDelta { StartMemberId = $"{x}P", Side = TreeSide.Left, Points = 9, NodeCount = 0,
                                                        CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba" });
                await db.SaveChangesAsync();
            }
            await using (var db = SqlServerReportes.Db())
            {
                var h = new CalculateDailyResidualHandler(db, new Reloj(), new SinUsuario());
                await h.Handle(new CalculateDailyResidualCommand(), CancellationToken.None);
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}P")).Should().Be(0);
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }
```

`SinUsuario` es un `ICurrentUserService` mínimo de la prueba, con la firma que pida el constructor (ver `CalculateDailyResidualHandler.cs:23-31`). `BizCenter.Tests` necesita la referencia a `CommissionEngine` si no la tiene; si añadirla crea un ciclo, la prueba va en `CommissionEngine.Tests` con `SqlServerReportes.cs`, `ReportesDb.cs` y `BinarioDb.cs` enlazados como en la Tarea 1.

- [ ] **Paso 2.** En los tres, como primera línea del método:

```csharp
        // Plan Q (DQ1): las piernas son diferidas; lo que se paga se calcula con la cola vacia. Espera hasta 2 min a un movimiento.
        await LegPointMaintenance.DrainAsync(_db, ct: ct, lockTimeoutMs: 120_000);
```

- [ ] **Paso 3.** En verde. Commit: `Plan Q: el residual diario, el bono presidencial y la instantanea vacian la cola de piernas antes de leer`.

### Tarea 10: medición rápida local del bloque

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerMedicionPlanPTests.cs` (las mediciones de colocación a más de 8.000 niveles de la base local)

- [ ] **Paso 1.** Las mediciones locales del Plan P («colocar una hoja a 8.223 niveles», «10 a la vez») pasan a medir la colocación sin la subida y, aparte, el vaciado. Nuevos objetivos:
  - colocar una hoja a más de 8.000 niveles, **< 300 ms** (antes 1,6 s);
  - 10 a la vez, **p95 < 2 s**;
  - vaciar los 10 deltas, **< 5 s**.
- [ ] **Paso 2.** Ejecutar con la categoría de medición y anotar las cifras en «Resultados medidos». Commit: `Plan Q: medicion local de la colocacion sin subida y del vaciado de la cola`.

---

## Bloque 3: sin `HierarchyPath` en el binario

**Inventario (2026-10-19).**
- **Escritor en producción:** solo `DualTeamNodes`:
  - `RootAsync`, `:76`;
  - `ChildAsync`, `:89`;
  - `MoveSubtreeAsync`, `:111-129`;
  - `RewriteSubtreeSqlAsync`, `:280-290`;
  - `RewriteSubtreeInMemoryAsync`, `:331`.
- **Lectores en producción:** solo `DualTeamService.SearchBinarySubtreeAsync` (`:319-374`) y `GetDeepestNodeAsync` (`:377-412`). Trocean la ruta para devolver el camino bajo la raíz como lista de `{MemberId, FullName}`; ningún DTO expone la ruta y ningún `.razor` la usa.
- **Scripts que la escriben:** `Scripts/sql/plan-p/01-reparar-huecos-binario.sql:217` y `Scripts/sql/plan-p/escala/altas/g-binario.sql:34-36, 51-53, 80-82`.
- **Scripts que la leen:**
  - `Scripts/sql/plan-p/02-recalcular-piernas.sql:47`
  - `Scripts/sql/plan-p/escala/02-verificar-escala.sql:35-44`
  - `Scripts/sql/plan-m/04-control-binario.sql:24`
  - `Scripts/sql/perf-diag.ps1:28`
  - `Scripts/sql/leg-sponsors.ps1:9, 20, 25, 26`
  - `Scripts/sql/right-sponsors.ps1:15`
  - `Scripts/backfill-left.ps1:41, 45`
  - `Scripts/backfill-both-fast.ps1:45, 49`
  - `Scripts/backfill-right-fast.ps1:45, 49`
  - `Scripts/SignupLoadTest/Program.cs:546`
- **Modelo:**
  - `DualTeamEntity.cs:12`;
  - `ITreeNode.cs:7` (la interfaz no tiene consumidores genéricos);
  - `DualTeamPlacementRules.cs:55-59` (`Depth(path)` y `ChildPath`);
  - `DualTeamConfiguration.cs:13-19`.
- **Pruebas:** siembran `HierarchyPath` en `new DualTeamEntity { … }` en unas 25 pruebas, la afirman en unas 12 y la usan como filtro en `SqlServerDualSubtreeTests.cs:88-92`.

### Tarea 11: el camino bajo la raíz, por la clave

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Queries/DualAncestors.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/DualTeamService.cs:319-412`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerCaminoPorClaveTests.cs`

- [ ] **Paso 1.** Prueba en rojo. En un árbol P → L → LR → LRL, `GetDeepestNodeAsync(P, Left)` devuelve LRL con `Depth = 3` y el camino `[L, LR, LRL]`. `SearchBinarySubtreeAsync(P, "LRL")` devuelve el mismo camino y la pierna `Left`. Después se borra la ruta de esos nodos (`UPDATE DualTeamTree SET HierarchyPath = N'' WHERE MemberId LIKE @prefijo`) dentro de la transacción y se repite: el resultado no cambia. Hoy falla: con la ruta vacía, el camino sale vacío.

```csharp
    [SqlServerFact]
    public async Task El_camino_bajo_la_raiz_no_depende_de_la_ruta_de_texto()
    {
        var x = BinarioDb.Prefijo("PQK");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left),
            ($"{x}LR", $"{x}L", TreeSide.Right), ($"{x}LRL", $"{x}LR", TreeSide.Left));
        await BinarioDb.SembrarPerfilesAsync(db, $"{x}P", $"{x}L", $"{x}LR", $"{x}LRL");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DualTeamTree SET HierarchyPath = N'' WHERE MemberId LIKE {x + "%"}");
        var svc = new DualTeamService(db /* resto de dependencias como en los tests vecinos de DualTeamService */);

        var hondo = await svc.GetDeepestNodeAsync($"{x}P", TreeSide.Left);
        hondo!.MemberId.Should().Be($"{x}LRL");
        hondo.Depth.Should().Be(3);
        hondo.Path.Select(p => p.MemberId).Should().Equal($"{x}L", $"{x}LR", $"{x}LRL");

        var hallado = (await svc.SearchBinarySubtreeAsync($"{x}P", $"{x}LRL")).Single();
        hallado.Leg.Should().Be("Left");
        hallado.Path.Select(p => p.MemberId).Should().Equal($"{x}L", $"{x}LR", $"{x}LRL");
        await tx.RollbackAsync();
    }
```

Antes de escribirla, copiar la construcción de `DualTeamService` de la prueba vecina que ya lo instancia contra SQL Server (`grep -rn "new DualTeamService(" MLMConquerorGlobalEdition.BizCenter.Tests`) en lugar del comentario.

- [ ] **Paso 2.** `DualAncestors`:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan Q: el camino de <c>node</c> por debajo de <c>root</c> (sin root, con node), de arriba abajo, por la clave
/// compacta (dbo.DualAncestorKeys y UX_DualTeamTree_BinaryPath). Sustituye a trocear HierarchyPath. En InMemory se sube
/// por ParentMemberId.
/// </summary>
public static class DualAncestors
{
    private sealed record Paso(int Depth, string MemberId);

    public static async Task<List<string>> PathBelowAsync(AppDbContext db, DualTeamEntity root, DualTeamEntity node, CancellationToken ct = default)
    {
        if (db.Database.IsSqlServer())
        {
            var desde = (root.Depth ?? 0) + 1;
            var filas = await db.Database.SqlQuery<Paso>($@"
SELECT a.Depth, d.MemberId
FROM dbo.DualAncestorKeys({node.BinaryPath}, 0) a
JOIN DualTeamTree d ON d.BinaryPath = a.AncestorKey AND d.IsDeleted = 0 AND d.BinaryPath IS NOT NULL
WHERE a.Depth >= {desde}").ToListAsync(ct);
            return filas.OrderBy(f => f.Depth).Select(f => f.MemberId).ToList();
        }

        var camino = new List<string>();
        var actual = node;
        while (actual is not null && actual.MemberId != root.MemberId)
        {
            camino.Add(actual.MemberId);
            actual = actual.ParentMemberId is null ? null
                : await db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == actual.ParentMemberId, ct);
        }
        camino.Reverse();
        return camino;
    }

    /// <summary>La pierna de <paramref name="root"/> en la que esta <paramref name="node"/> (por la clave).</summary>
    public static TreeSide LegOf(DualTeamEntity root, DualTeamEntity node) =>
        Domain.Services.DualBinaryPath.SideBelow(node.BinaryPath!, root.Depth ?? 0);
}
```

- [ ] **Paso 3.** En `DualTeamService`:
  - `SearchBinarySubtreeAsync` proyecta la entidad del nodo en vez de `d.HierarchyPath` (`select new { Node = d, m.FirstName, m.LastName }`).
  - El camino de cada coincidencia sale de `DualAncestors.PathBelowAsync(_db, rootNode, mt.Node, ct)` y la pierna de `DualAncestors.LegOf(rootNode, mt.Node).ToString()`.
  - `GetDeepestNodeAsync` hace lo mismo con su nodo.
  - Se borran `TreePrefix.Of(rootNode.HierarchyPath)`, `SegmentCount` y la consulta `legByGateway`.
  - Los nombres se siguen resolviendo en una sola consulta con todos los ids de todos los caminos.
  - `Depth = camino.Count`.
- [ ] **Paso 4.** En verde. Commit: `Plan Q: la busqueda y el mas profundo del binario sacan el camino de la clave compacta`.

### Tarea 12: `DualTeamNodes` deja de escribir la ruta

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/DualTeamNodes.cs:72-93, 100-158, 248-338`
- Modify: `MLMConquerorGlobalEdition.Domain/Services/DualTeamPlacementRules.cs:55-59` (se borran `Depth(string)` y `ChildPath`)
- Modify: `MLMConquerorGlobalEdition.Domain.Tests` (las pruebas de `Depth(path)` y `ChildPath` se borran con ellos)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/RutasConBarraFinalTests.cs:20-28` (se quita la entrada `DualTeamNodes.cs = 4`)

- [ ] **Paso 1.** Prueba en rojo, en `SqlServerClaveCompactaTests`. Tras colocar una hoja y mover una raíz provisional, la ruta de esos nodos queda vacía:

```csharp
    [SqlServerFact]
    public async Task Ni_colocar_ni_mover_escriben_la_ruta_de_texto()
    {
        var x = BinarioDb.Prefijo("PQR");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));
                await BinarioDb.SembrarPerfilAsync(db, $"{x}B", $"{x}A");
                await BinarioDb.SembrarPerfilAsync(db, $"{x}C", $"{x}B");
                var motor = new PlacementService(db, new Reloj());
                (await motor.PlaceAsync($"{x}C", $"{x}B")).IsSuccess.Should().BeTrue();   // B raiz provisional
                (await motor.PlaceAsync($"{x}B", $"{x}A")).IsSuccess.Should().BeTrue();   // se mueve con C
            }
            await using (var db = SqlServerReportes.Db())
                (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM DualTeamTree WHERE MemberId LIKE {x + "%"} AND HierarchyPath <> N''").SingleAsync())
                    .Should().Be(0);
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }
```

- [ ] **Paso 2.** Cambios en `DualTeamNodes`. Hasta la migración de la Tarea 13 la columna sigue siendo `NOT NULL`, así que se escribe `string.Empty`:
  - `RootAsync` y `ChildAsync` ponen `HierarchyPath = string.Empty`.
  - `MoveSubtreeAsync` ya no calcula `newPath`: la tupla del `switch` pasa a ser `(newParentId, newSide, newKey)`.
  - `RewriteSubtreeSqlAsync` quita la línea `HierarchyPath = @nueva + SUBSTRING(…)` (`:282`) y los parámetros `@nueva` y `@vieja` (`:289-290`).
  - `RewriteSubtreeInMemoryAsync` quita `:331`.
- [ ] **Paso 3.** En verde (la prueba del Paso 1 y las de `Binario`). Commit: `Plan Q: el binario deja de escribir la ruta de texto`.

### Tarea 13: eliminar la columna

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Tree/DualTeamEntity.cs:7,12` (fuera `HierarchyPath` y fuera `ITreeNode`)
- Modify: `MLMConquerorGlobalEdition.Domain/Interfaces/ITreeNode.cs`: **se mantiene**, porque `GenealogyEntity` la implementa
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/DualTeamConfiguration.cs:13-19`
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/DualTeamNodes.cs` (las asignaciones `string.Empty` de la Tarea 12)
- Create: migración `RetiraRutaDelBinario`
- Create: `Scripts/sql/plan-q/03-recuperar-espacio-de-la-ruta.sql`
- Modify: las pruebas que siembran o afirman la ruta del binario (lista exacta: `grep -rln "DualTeamEntity" --include=*.cs *Tests* | xargs grep -ln "HierarchyPath"`, revisando que cada uso sea del binario y no de `GenealogyEntity`)
- Delete: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerDualSubtreeTests.cs:85-95` (la prueba que demostraba el error 8152 con `StartsWith` sobre la ruta)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/EscalaBinarioTests.cs:69, 80` (el informe deja de medir los MB de ruta)

- [ ] **Paso 1.** Quitar la propiedad y su configuración. Compilar la solución: los errores de compilación son la lista de lo que queda por cambiar. En las pruebas, quitar `HierarchyPath = …` de cada `new DualTeamEntity` y cada aserción sobre la ruta del binario. Donde la aserción comprobaba la posición (`PlaceMemberHandlerTests:129, 165, 232-233`, `AdminPlaceMemberHandlerTests:91, 96, 166, 220`, `AdminRemovePlacementHandlerTests:114, 117`, `SignupAmbassadorHandlerTests:811`, `SqlServerColocacionConcurrenteTests:111-113`, `SqlServerRaizProvisionalTests:84, 161`), pasa a comprobar `ParentMemberId`, `Side` y `Depth`, y en SQL Server `BinaryPath == DualBinaryPath.Child(padre.BinaryPath, lado)`.
- [ ] **Paso 2.** `dotnet ef migrations add RetiraRutaDelBinario …`. **Esperado:** `Up` = `DropColumn("HierarchyPath", "DualTeamTree")`. Al principio del `Up`, una guarda que falla cerrada si algún objeto de la base (vista, función o procedimiento) aún menciona la columna:

```csharp
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.sql_modules m
           WHERE m.definition LIKE N'%DualTeamTree%' AND m.definition LIKE N'%HierarchyPath%')
    THROW 51100, N'Un objeto SQL aun lee DualTeamTree.HierarchyPath (Plan Q, Tarea 13).', 1;");
```

  El `Down` vuelve a crear la columna `nvarchar(max) NOT NULL DEFAULT N''`, vacía. Volver atrás de verdad es restaurar la copia del despliegue (Q-R10).
- [ ] **Paso 3.** `03-recuperar-espacio-de-la-ruta.sql`. `DROP COLUMN` es solo de metadatos, así que el espacio (23,5 a 65,8 GB a escala) se libera por lotes. Es idempotente: una segunda pasada no encuentra nada que liberar.

```sql
/*
Plan Q, Tarea 13. Libera el espacio de la columna DualTeamTree.HierarchyPath, ya eliminada por RetiraRutaDelBinario.
DBCC CLEANTABLE por lotes (cada lote, su transaccion). Idempotente. Ejecutar con los servicios en marcha o parados.
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-q/03-recuperar-espacio-de-la-ruta.sql
*/
SET NOCOUNT ON;
IF N'$(Confirmo)' NOT IN (N'LOCAL', N'ESCALA')
    THROW 51101, N'Ejecutar con -v Confirmo=LOCAL (o ESCALA en la base de escala).', 1;
IF NOT ((N'$(Confirmo)' = N'LOCAL'  AND DB_NAME() = N'MLMConquerorGlobalEditionDb')
     OR (N'$(Confirmo)' = N'ESCALA' AND DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala'))
    THROW 51102, N'Confirmo no corresponde a esta base.', 1;
IF COL_LENGTH(N'dbo.DualTeamTree', N'HierarchyPath') IS NOT NULL
    THROW 51103, N'La columna sigue existiendo: aplicar antes la migracion RetiraRutaDelBinario.', 1;

DECLARE @antes bigint = (SELECT SUM(used_page_count) * 8 / 1024 FROM sys.dm_db_partition_stats WHERE object_id = OBJECT_ID(N'dbo.DualTeamTree'));
DECLARE @db sysname = DB_NAME();
DBCC CLEANTABLE (@db, N'dbo.DualTeamTree', 20000) WITH NO_INFOMSGS;
ALTER INDEX ALL ON dbo.DualTeamTree REORGANIZE WITH (LOB_COMPACTION = ON);
DECLARE @despues bigint = (SELECT SUM(used_page_count) * 8 / 1024 FROM sys.dm_db_partition_stats WHERE object_id = OBJECT_ID(N'dbo.DualTeamTree'));
DECLARE @msg nvarchar(200) = CONCAT(N'DualTeamTree: ', @antes, N' MB -> ', @despues, N' MB');
RAISERROR(@msg, 0, 1) WITH NOWAIT;
```

- [ ] **Paso 4.** Aplicar en local, en este orden: la migración (`dotnet ef database update … --connection …`) y después el script. Anotar los MB de antes y después. **Esperado en local:** la tabla baja en unos 2.290 MB, que es el tamaño de las rutas medido en el Plan P.
- [ ] **Paso 5.** Todas las suites en verde, en serie. Commit: `Plan Q: se elimina la ruta de texto del binario con su script de recuperacion de espacio`.

### Tarea 14: los scripts y la regla

**Files:**
- Modify: `Scripts/sql/plan-p/01-reparar-huecos-binario.sql` (guarda de retirada al principio)
- Modify: `Scripts/sql/plan-p/02-recalcular-piernas.sql:47` (nivel por `Depth`)
- Modify: `Scripts/sql/plan-m/04-control-binario.sql:24` (`BinaryPath` en lugar de la ruta)
- Modify: `Scripts/sql/perf-diag.ps1:28`, `Scripts/sql/leg-sponsors.ps1:9-26`, `Scripts/sql/right-sponsors.ps1:15`, `Scripts/backfill-left.ps1:41-45`, `Scripts/backfill-both-fast.ps1:45-49`, `Scripts/backfill-right-fast.ps1:45-49`, `Scripts/SignupLoadTest/Program.cs:546`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SinRutaEnElBinarioTests.cs:17-60`

- [ ] **Paso 1.** La regla, en rojo hasta terminar el paso 2. Desde ahora **cualquier** mención de `HierarchyPath` en una sentencia que nombre `DualTeamTree` o `DualTeamEntity` es un fallo, no solo un filtro. Se amplía el recorrido a `.ps1` y `.cs` de `Scripts/` y a `DeploymentScripts/`, igual que `EscritoresDelBinarioTests`.
  - **Excepciones**, cada una con su motivo:
    - `Migrations/`, que es historia;
    - `DeploymentScripts/migrate-rds.sql`, desactualizado y marcado así desde el Plan P;
    - `Scripts/sql/plan-p/01-reparar-huecos-binario.sql`, retirado por su guarda.
  - **Expresión nueva:**

```csharp
    private static readonly Regex RutaDelBinario = new(@"\bHierarchyPath\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
```

  Se aplica a cada sentencia sin comentarios que también cumpla `\bDualTeam(Tree|Entity)\b` (la condición de la línea 22, que ya existe).
- [ ] **Paso 2.** Los scripts:
  - `01-reparar-huecos-binario.sql`, primera instrucción tras las guardas de base:

    ```sql
    IF COL_LENGTH(N'dbo.DualTeamTree', N'HierarchyPath') IS NULL
        THROW 51104, N'Retirado por el Plan Q: el binario ya no tiene ruta de texto. La reparacion se hizo en el Plan P.', 1;
    ```

  - `02-recalcular-piernas.sql:47`: `Nivel = d.Depth` en lugar de contar barras.
  - `04-control-binario.sql:24`: lista `c.BinaryPath` y `c.Depth`.
  - En los `.ps1` y en `SignupLoadTest/Program.cs`, la pertenencia `d.HierarchyPath LIKE N'%/X/%'` pasa a esta expresión, que usa la función de la Tarea 5. Es lenta, pero son herramientas de desarrollo:

    ```sql
    EXISTS (SELECT 1 FROM dbo.DualAncestorKeys(d.BinaryPath, 0) a
            WHERE a.AncestorKey = (SELECT x.BinaryPath FROM DualTeamTree x WHERE x.MemberId = N'X' AND x.IsDeleted = 0))
    ```

    La profundidad por barras pasa a `d.Depth`.
- [ ] **Paso 3.** La regla en verde. Commit: `Plan Q: los scripts dejan la ruta del binario y la regla prohibe cualquier uso de HierarchyPath en el binario`.

### Tarea 15: medición rápida local del bloque

- [ ] **Paso 1.** Medir con `SqlServerMedicionPlanPTests` (base local, 119.433 nodos y 8.223 niveles) la colocación de una raíz provisional con 50 en su equipo a más de 8.000 niveles. El ensayo del Plan P dio 1,8 s. Se espera menos, porque la ruta de cada uno de los 50 llegaba a 90.000 caracteres. Anotar en «Resultados medidos».
- [ ] **Paso 2.** Commit: `Plan Q: medicion local del movimiento sin ruta de texto`.

---

## Bloque 4: alta acotada

**Hoy** (inventario del 2026-10-19):
- **Cola de rango:** `SignupAmbassadorHandler.cs:312-330` y `SignupMemberHandler.cs:195-213` escriben una fila de `RankEvaluationQueue` por ascendiente, troceando la ruta del patrocinador.
- **Avisos:** `SignupAmbassadorHandler.cs:352-368` y `SignupMemberHandler.cs:235-251` lanzan un `_push.SendAsync(… DOWNLINE_ENROLLED …)` por ascendiente. Cada uno abre su ámbito, busca tokens e inserta una fila en `MemberNotifications`, aunque nadie lee esa tabla: no hay ningún lector.
- **Deltas:** `SignupActivationService.cs:69-106` escribe un `MemberStatisticDelta` por ascendiente.
- **Trabajo de deltas:** `ApplyMemberStatisticDeltasJob.cs:62-201`:
  - hace un `MERGE` por miembro distinto, con una ida y vuelta por cada uno;
  - vuelve a encolar una evaluación de rango por cada miembro tocado.
- **Facturación:** `DownstreamTriggers.cs:53-99` encola un rango por ascendiente.

### Tarea 16: `GenealogyUpline`, la cota en un solo sitio

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Services/GenealogyUpline.cs`
- Create: `MLMConquerorGlobalEdition.Domain.Tests/GenealogyUplineTests.cs`
- Modify: `MLMConquerorGlobalEdition.RankEngine/Features/EvaluateRank/EvaluateRankHandler.cs:24-28, 255-257`

- [ ] **Paso 1.** Prueba en rojo:

```csharp
using MLMConquerorGlobalEdition.Domain.Services;

namespace MLMConquerorGlobalEdition.Domain.Tests;

public class GenealogyUplineTests
{
    [Fact]
    public void Devuelve_los_mas_cercanos_primero_sin_el_propio_y_con_tope()
    {
        var ruta = "/" + string.Join("/", Enumerable.Range(0, 15).Select(i => $"M{i}")) + "/";
        GenealogyUpline.Closest(ruta, "M14").Should().Equal("M13", "M12", "M11", "M10", "M9", "M8", "M7", "M6", "M5", "M4");
        GenealogyUpline.Closest(ruta, null, 3).Should().Equal("M14", "M13", "M12");
        GenealogyUpline.Closest("/A/", "A").Should().BeEmpty();
        GenealogyUpline.Closest(null, "A").Should().BeEmpty();
    }
}
```

- [ ] **Paso 2.** Implementación:

```csharp
namespace MLMConquerorGlobalEdition.Domain.Services;

/// <summary>
/// Plan Q (DQ5): la linea de inscripcion que recibe avisos y evaluaciones de rango inmediatas, acotada. La misma cota que I3
/// del Plan P (avisos de rango). Mas alla, la fase 2 de RankEvaluationSweepJob evalua a todos cada noche.
/// </summary>
public static class GenealogyUpline
{
    public const int EnrollmentLevels = 10;

    /// <summary>
    /// Los ascendientes que nombra una ruta de inscripcion "/A/B/C/", del mas cercano al mas lejano, sin
    /// <paramref name="selfMemberId"/> y como mucho <paramref name="levels"/>.
    /// </summary>
    public static IReadOnlyList<string> Closest(string? hierarchyPath, string? selfMemberId, int levels = EnrollmentLevels)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPath) || levels <= 0) return Array.Empty<string>();
        return hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.Equals(s, selfMemberId, StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .Take(levels)
            .ToList();
    }
}
```

- [ ] **Paso 3.** `EvaluateRankHandler`:
  - `public const int UplineNotificationLevels = GenealogyUpline.EnrollmentLevels;` (`:28`);
  - las líneas 255-257 pasan a `GenealogyUpline.Closest(genealogyNode?.HierarchyPath, memberId)`;
  - se borra `ParseAncestors` si queda sin usos.
- [ ] **Paso 4.** `dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter FullyQualifiedName~GenealogyUpline` y `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests` en verde. Commit: `Plan Q: la cota de la linea de inscripcion vive en GenealogyUpline`.

### Tarea 17: el alta encola y avisa a 10 niveles

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs:312-330, 352-368`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupMember/SignupMemberHandler.cs:195-213, 235-251`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/HighVolume/DownstreamTriggers.cs:73-78, 189-198`
- Modify: `MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupAmbassadorHandlerTests.cs` (y la de `SignupMember` si afirma por ascendiente)

- [ ] **Paso 1.** Prueba en rojo (InMemory, en `SignupAmbassadorHandlerTests`). Un patrocinador a 15 niveles de la raíz:
  - el alta deja exactamente 10 filas de `RankEvaluationQueue` con `TriggerEvent = Enrollment`, para el patrocinador y sus 9 ascendientes más cercanos;
  - el `Mock<IPushNotificationService>` recibe 10 llamadas `SendAsync(…, NotificationEvents.DownlineEnrolled, …)`.

  Se siembra la línea con `ReportesDb`, o con el ayudante de genealogía que ya usan esas pruebas (`grep -n "GenealogyTree.Add" MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupAmbassadorHandlerTests.cs`). **Esperado:** hoy salen 15 y 15.
- [ ] **Paso 2.** En los dos manejadores, los dos bucles recorren `GenealogyUpline.Closest(sponsorNode.HierarchyPath, null)` en lugar del `Split` completo:

```csharp
        // Plan Q (DQ5): la linea de patrocinio hasta GenealogyUpline.EnrollmentLevels; mas alla evalua el barrido nocturno.
        foreach (var uplineId in GenealogyUpline.Closest(sponsorNode.HierarchyPath, null))
```

- [ ] **Paso 3.** `DownstreamTriggers.ParseUplineIds` (`:189-198`) pasa a `GenealogyUpline.Closest(hierarchyPath, memberId)`, y su prueba (`Billing.Tests`, `grep -rn "DownstreamTriggers" MLMConquerorGlobalEdition.Billing.Tests`) pasa a esperar 10 como mucho.
- [ ] **Paso 4.** Ajustar las pruebas existentes que esperaban una fila o un aviso por ascendiente (líneas que cuentan `RankEvaluationQueue` o llamadas a `SendAsync` en esas suites). En verde. Commit: `Plan Q: el alta y la facturacion encolan rango y avisan a la linea de patrocinio hasta 10 niveles`.

### Tarea 18: un delta por alta (`Scope = SponsorLine`)

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberStatisticDeltaScope.cs`
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberStatisticDelta.cs` (propiedad `Scope`)
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberStatisticDeltaConfiguration.cs:24-32` (`Scope` con valor por defecto 0)
- Create: migración `DeltaPorLineaDePatrocinio` (columna y función `dbo.GenealogyAncestors`)
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Services/SignupActivationService.cs:69-106, 153-154`

- [ ] **Paso 1.** Prueba en rojo (`Signups.Tests`, InMemory, junto a las de `SignupActivationService`). Activar un alta cuyo patrocinador está a 12 niveles deja **una** fila de `MemberStatisticDeltas`:
  - `MemberId` = el patrocinador;
  - `Scope = SponsorLine`;
  - `EnrollmentPointsDelta` = los puntos de calificación del pedido;
  - `EnrollmentTeamSizeDelta = 1` y `QualifiedSponsoredMembersDelta = 1`.

  **Esperado:** hoy salen 12 filas.
- [ ] **Paso 2.** El enum y la propiedad:

```csharp
namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>Plan Q: a quien se aplica un MemberStatisticDelta.</summary>
public enum MemberStatisticDeltaScope
{
    /// <summary>Solo a MemberId (las filas anteriores al Plan Q: una por ascendiente).</summary>
    Member = 0,
    /// <summary>MemberId es el patrocinador: el delta se aplica a el y a cada ascendiente de su Node; QualifiedSponsoredMembersDelta solo a el.</summary>
    SponsorLine = 1
}
```

```csharp
    /// <summary>Plan Q: a quien se aplica (ver <see cref="MemberStatisticDeltaScope"/>).</summary>
    public MemberStatisticDeltaScope Scope { get; set; }
```

- [ ] **Paso 3.** `dotnet ef migrations add DeltaPorLineaDePatrocinio …`. Al final del `Up`:

```csharp
            // Plan Q: los ascendientes de un Node de inscripcion, el mismo incluido (Distancia 0), sin leer la tabla.
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.GenealogyAncestors(@node hierarchyid)
RETURNS TABLE AS RETURN
SELECT n.N AS Distancia, @node.GetAncestor(n.N) AS Node
FROM dbo.Numeros(@node.GetLevel() - 1) n;");
```

  En el `Down`: `DROP FUNCTION IF EXISTS dbo.GenealogyAncestors;`.
- [ ] **Paso 4.** `SignupActivationService.cs:69-106` pasa a:

```csharp
            if (sponsorNode is not null)
            {
                // Plan Q (Bloque 4): UNA fila por alta. ApplyMemberStatisticDeltasJob la expande en SQL por los
                // ascendientes del Node del patrocinador (el mismo delta que antes, en cada uno). Antes, una fila por
                // ascendiente en esta transaccion: 29,4 millones de filas con 1.000.000 de altas (Plan P, Bloque D).
                await _db.MemberStatisticDeltas.AddAsync(new MemberStatisticDelta
                {
                    MemberId                       = member.SponsorMemberId,
                    Scope                          = MemberStatisticDeltaScope.SponsorLine,
                    EnrollmentPointsDelta          = totalQualificationPoints,
                    EnrollmentTeamSizeDelta        = 1,
                    QualifiedSponsoredMembersDelta = 1,
                    SourceMemberId                 = member.MemberId,
                    IsApplied                      = false,
                    CreatedBy                      = actorEmail,
                    CreationDate                   = now
                }, ct);
            }
```

  Se borra `ParseHierarchyPath` (`:153-154`), que queda sin usos.
- [ ] **Paso 5.** Aplicar la migración en local. Commit (el trabajo se cambia en la Tarea 19): `Plan Q: la activacion encola un solo delta de estadisticas por alta para toda la linea de patrocinio`.

### Tarea 19: el trabajo de deltas, por conjuntos

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Jobs/ApplyMemberStatisticDeltasJob.cs:62-201`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerDeltasPorLineaTests.cs`
- Modify: `MLMConquerorGlobalEdition.Signups.Tests/Jobs/ApplyMemberStatisticDeltasJobTests.cs` (InMemory: casos de `SponsorLine`)

- [ ] **Paso 1.** Prueba en rojo, `[SqlServerFact]` dentro de una transacción que se deshace. Una línea de 12 niveles (L0 a L11), cada uno con su `MemberStatistics`, y un delta `SponsorLine` sobre L11 con 6 puntos. Tras el trabajo:
  - los 12 tienen `EnrollmentPoints` +6 y `EnrollmentTeamSize` +1;
  - solo L11 tiene `QualifiedSponsoredMembers` +1;
  - hay una evaluación de rango pendiente para L11 a L2 (10) y ninguna para L1 ni L0;
  - el delta queda `IsApplied`.

```csharp
    [SqlServerFact]
    public async Task Un_delta_de_linea_llega_a_todos_los_ascendientes_y_encola_rango_a_10_niveles()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ids = Enumerable.Range(0, 12).Select(_ => SqlServerReportes.NuevoId("DL")).ToArray();
        for (var i = 0; i < ids.Length; i++)
        {
            ReportesDb.Miembro(db, ids[i], i == 0 ? null : ids[i - 1]);
            ReportesDb.Nodo(db, ids[i], i == 0 ? null : ids[i - 1], ReportesDb.Ruta(ids[..(i + 1)]), i + 1);
            ReportesDb.Puntos(db, ids[i], 100);
        }
        db.MemberStatisticDeltas.Add(new MemberStatisticDelta
        {
            MemberId = ids[11], Scope = MemberStatisticDeltaScope.SponsorLine, EnrollmentPointsDelta = 6,
            EnrollmentTeamSizeDelta = 1, QualifiedSponsoredMembersDelta = 1, SourceMemberId = "NUEVO",
            CreatedBy = "plan-q-prueba", CreationDate = DateTime.Now
        });
        await db.SaveChangesAsync();

        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();

        var stats = await db.MemberStatistics.AsNoTracking().Where(s => ids.Contains(s.MemberId)).ToDictionaryAsync(s => s.MemberId);
        ids.Should().OnlyContain(i => stats[i].EnrollmentPoints == 106 && stats[i].EnrollmentTeamSize == 1);
        ids.Select(i => stats[i].QualifiedSponsoredMembers).Should().Equal(Enumerable.Repeat(0, 11).Append(1));
        var encolados = await db.RankEvaluationQueue.AsNoTracking()
            .Where(q => ids.Contains(q.EvaluateMemberId) && !q.IsProcessed).Select(q => q.EvaluateMemberId).ToListAsync();
        encolados.Should().BeEquivalentTo(ids[2..]);
        await tx.RollbackAsync();
    }
```

(`ReportesDb.Puntos` crea `MemberStatistics` con `EnrollmentPoints = 100`; si la base local ya tiene deltas pendientes, el trabajo también los aplica dentro de la transacción, que se deshace al final.)

- [ ] **Paso 2.** El trabajo. En SQL Server, cada tanda es **un** comando por conjuntos. La rama InMemory (`:104-127`) se mantiene para `Scope = Member` y expande `SponsorLine` subiendo por `Node.GetAncestor(k)` en C#.

```csharp
    private const string ApplyBatchSql = @"
SET NOCOUNT ON;
CREATE TABLE #lote (Id bigint PRIMARY KEY, MemberId nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL, Scope int NOT NULL,
                    P int NOT NULL, T int NOT NULL, Q int NOT NULL, CreatedBy nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL);
INSERT INTO #lote
SELECT TOP (@lote) Id, MemberId, Scope, EnrollmentPointsDelta, EnrollmentTeamSizeDelta, QualifiedSponsoredMembersDelta, CreatedBy
FROM MemberStatisticDeltas WHERE IsApplied = 0 ORDER BY Id;

-- Scope 0: la fila es del propio MemberId (filas anteriores al Plan Q). Scope 1: MemberId es el patrocinador y el delta
-- sube por los ascendientes de su Node vigente (el mismo criterio que CurrentOfAsync).
SELECT x.MemberId, x.Distancia, l.P, l.T,
       CASE WHEN l.Scope = 0 OR x.Distancia = 0 THEN l.Q ELSE 0 END AS Q, l.CreatedBy
INTO #exp
FROM #lote l
CROSS APPLY (
    SELECT l.MemberId AS MemberId, 0 AS Distancia WHERE l.Scope = 0
    UNION ALL
    SELECT a.MemberId, an.Distancia
    FROM (SELECT TOP 1 g.Node FROM GenealogyTree g
          WHERE g.MemberId = l.MemberId AND g.IsDeleted = 0 AND g.Node IS NOT NULL
          ORDER BY g.CreationDate DESC, g.Id DESC) s
    CROSS APPLY dbo.GenealogyAncestors(s.Node) an
    JOIN GenealogyTree a ON a.Node = an.Node AND a.IsDeleted = 0
    WHERE l.Scope = 1
) x;

SELECT MemberId, SUM(P) AS P, SUM(T) AS T, SUM(Q) AS Q, MIN(CreatedBy) AS CreatedBy
INTO #agg FROM #exp GROUP BY MemberId;

MERGE INTO MemberStatistics WITH (HOLDLOCK) AS t
USING #agg AS s ON t.MemberId = s.MemberId
WHEN MATCHED THEN UPDATE SET
    EnrollmentPoints          = t.EnrollmentPoints          + s.P,
    EnrollmentTeamSize        = t.EnrollmentTeamSize        + s.T,
    QualifiedSponsoredMembers = t.QualifiedSponsoredMembers + s.Q
WHEN NOT MATCHED THEN INSERT (MemberId, PersonalPoints, ExternalCustomerPoints, DualTeamSize,
        EnrollmentTeamSize, DualTeamPoints, EnrollmentPoints, QualifiedSponsoredMembers, QualifiedSponsoredExternalCustomers,
        EnrollmentTeamGrowth, DualteamGrowth, EnrollmentTeamPointsGrowth, DualTeamPointsGrowth,
        CurrentWeekIncomeGrowth, CurrentMonthIncomeGrowth, CurrentYearIncomeGrowth, CreationDate, CreatedBy)
    VALUES (s.MemberId, 0, 0, 0, s.T, 0, s.P, s.Q, 0, 0, 0, 0, 0, 0, 0, 0, @now, s.CreatedBy);

-- Evaluacion de rango inmediata solo hasta @niveles de distancia del patrocinador (DQ5).
INSERT INTO RankEvaluationQueue (TriggerMemberId, EvaluateMemberId, TriggerEvent, TriggerDate, IsProcessed, ProcessedAt,
                                 ProcessedBy, ErrorMessage, RetryCount, CreationDate, CreatedBy)
SELECT e.MemberId, e.MemberId, @enrollment, @now, 0, NULL, NULL, NULL, 0, @now, N'delta-apply'
FROM (SELECT DISTINCT MemberId FROM #exp WHERE Distancia < @niveles) e
WHERE NOT EXISTS (SELECT 1 FROM RankEvaluationQueue q
                  WHERE q.IsProcessed = 0 AND q.TriggerEvent = @enrollment AND q.EvaluateMemberId = e.MemberId);

UPDATE d SET IsApplied = 1, AppliedAt = @now FROM MemberStatisticDeltas d JOIN #lote l ON l.Id = d.Id;
SELECT COUNT(*) AS [Value] FROM #lote;
DROP TABLE #agg; DROP TABLE #exp; DROP TABLE #lote;";
```

  El bucle de `ExecuteAsync` en SQL Server:

```csharp
            var n = (await _db.Database.SqlQueryRaw<int>(ApplyBatchSql,
                    new SqlParameter("@lote", BatchSize), new SqlParameter("@now", _dateTime.Now),
                    new SqlParameter("@enrollment", (int)RankEvaluationTrigger.Enrollment),
                    new SqlParameter("@niveles", GenealogyUpline.EnrollmentLevels))
                .ToListAsync(ct)).Single();
            if (n == 0) break;
            totalApplied += n;
```

  Con la base actual hay un solo `MemberStatistics` por miembro; si hubiera varios, el `MERGE` los actualizaría todos, igual que el `MERGE` de hoy.
- [ ] **Paso 3.** En verde (la prueba nueva y `ApplyMemberStatisticDeltasJobTests`). Commit: `Plan Q: el trabajo de deltas expande la linea de patrocinio en SQL y aplica las estadisticas con un MERGE por tanda`.

### Tarea 20: medición rápida local del bloque

- [ ] **Paso 1.** Prueba de medición `[SqlServerFact]` con rasgo `Medicion`, contra la base local, en una transacción que se deshace. Simula 1.000 altas bajo el miembro más profundo de la inscripción local (la rama más larga). Comprueba que:
  - deja 1.000 filas de deltas (antes, 1.000 × profundidad);
  - el trabajo las aplica en menos de 10 s;
  - `EnrollmentTeamSize` de la raíz sube 1.000.
- [ ] **Paso 2.** Anotar en «Resultados medidos». Commit: `Plan Q: medicion local del alta acotada y del trabajo de deltas por conjuntos`.

---

## Bloque 5: reportes con 1.000.000

### Tarea 21: un solo nodo vivo por miembro en `GenealogyTree`

**Files:**
- Create: `Scripts/sql/plan-q/01-nodos-de-inscripcion-repetidos.sql`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/GenealogyConfiguration.cs:24`
- Create: migración `UnNodoPorMiembro`
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/GenealogySubtree.cs:71-82`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportesDb.cs:150-163` (`NodoObsoletoAsync`)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/NodosVigentesEnLosEquiposTests.cs`

**Hecho del 2026-10-19 (solo lectura en local):** 119.446 filas y 119.446 miembros distintos, ninguna borrada. El nodo obsoleto (R8 del Plan J) no existe en local. Lo único que escribe nodos es el alta (`SignupAmbassadorHandler.cs:272`, `SignupMemberHandler.cs:193`) y `RootAmbassadorSeeder.cs:108`, siempre para un miembro nuevo.

- [ ] **Paso 1.** El script, con `SoloInforme`. Marca como borrados los nodos que no son el vigente: el más reciente por `(CreationDate, Id)`, el mismo criterio que `CurrentOfAsync`.

```sql
/*
Plan Q, Tarea 21. Deja un solo nodo vivo por miembro en GenealogyTree: los demas (R8 del Plan J) se marcan como borrados
(IsDeleted = 1, DeletedBy = 'plan-q-01'), por lotes de 5.000, cada lote en su transaccion. Idempotente.
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -v SoloInforme=1 -i Scripts/sql/plan-q/01-nodos-de-inscripcion-repetidos.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(Confirmo)' NOT IN (N'LOCAL', N'ESCALA')
    THROW 51105, N'Ejecutar con -v Confirmo=LOCAL (o ESCALA en la base de escala).', 1;
IF NOT ((N'$(Confirmo)' = N'LOCAL'  AND DB_NAME() = N'MLMConquerorGlobalEditionDb')
     OR (N'$(Confirmo)' = N'ESCALA' AND DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala'))
    THROW 51106, N'Confirmo no corresponde a esta base.', 1;
IF N'$(SoloInforme)' NOT IN (N'0', N'1')
    THROW 51107, N'SoloInforme debe ser 0 o 1.', 1;

SELECT Id INTO #obsoletos
FROM (SELECT Id, ROW_NUMBER() OVER (PARTITION BY MemberId ORDER BY CreationDate DESC, Id DESC) AS rn
      FROM GenealogyTree WHERE IsDeleted = 0) v
WHERE v.rn > 1;
DECLARE @n int = @@ROWCOUNT;
DECLARE @msg nvarchar(200) = CONCAT(N'Nodos no vigentes a marcar: ', @n);
RAISERROR(@msg, 0, 1) WITH NOWAIT;
IF N'$(SoloInforme)' = N'1' RETURN;

DECLARE @hechos int = 1;
WHILE @hechos > 0
BEGIN
    BEGIN TRAN;
    UPDATE TOP (5000) g
    SET IsDeleted = 1, DeletedAt = GETDATE(), DeletedBy = N'plan-q-01'
    FROM GenealogyTree g JOIN #obsoletos o ON o.Id = g.Id
    WHERE g.IsDeleted = 0;
    SET @hechos = @@ROWCOUNT;
    COMMIT;
END
SET @msg = CONCAT(N'Quedan miembros con mas de un nodo vivo: ',
    (SELECT COUNT(*) FROM (SELECT MemberId FROM GenealogyTree WHERE IsDeleted = 0 GROUP BY MemberId HAVING COUNT(*) > 1) x));
RAISERROR(@msg, 0, 1) WITH NOWAIT;
```

- [ ] **Paso 2.** Prueba en rojo, de modelo, en `NodoDeGenealogiaModeloTests`: el modelo tiene un índice único `UX_GenealogyTree_MemberId_Vivo` sobre `MemberId` con filtro `[IsDeleted] = 0`.
- [ ] **Paso 3.** Configuración, junto a la línea 24:

```csharp
        // Plan Q (DQ9): un solo nodo vivo por miembro. Con el, el nodo vigente es el unico y CurrentUnder no necesita la
        // antijoin (medida en el Plan P: el conteo del equipo de inscripcion tardaba 2,8 s con 1.000.000).
        builder.HasIndex(new[] { nameof(GenealogyEntity.MemberId) }, "UX_GenealogyTree_MemberId_Vivo")
            .IsUnique().HasFilter("[IsDeleted] = 0");
```

  `dotnet ef migrations add UnNodoPorMiembro …`, con la guarda al principio del `Up`:

```csharp
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM GenealogyTree WHERE IsDeleted = 0 GROUP BY MemberId HAVING COUNT(*) > 1)
    THROW 51108, N'GenealogyTree tiene miembros con mas de un nodo vivo: ejecutar antes Scripts/sql/plan-q/01-nodos-de-inscripcion-repetidos.sql (Plan Q, Tarea 21).', 1;");
```

- [ ] **Paso 4.** `CurrentUnder` (`GenealogySubtree.cs:78-82`) pasa a ser el rango, sin antijoin:

```csharp
    /// <summary>
    /// Los nodos vigentes que cuelgan de <paramref name="root"/>, el incluido. Plan Q: con UX_GenealogyTree_MemberId_Vivo
    /// cada miembro tiene un solo nodo vivo, asi que el vigente es el unico y basta el rango (<see cref="Under"/>). Se
    /// mantiene el nombre: es lo que usan Mi equipo, clientes, eventos, puntos y comisiones.
    /// </summary>
    public static IQueryable<GenealogyEntity> CurrentUnder(this IQueryable<GenealogyEntity> tree, HierarchyId? root) =>
        tree.Under(root);
```

- [ ] **Paso 5.** `ReportesDb.NodoObsoletoAsync` sigue creando el nodo con fecha antigua, pero además lo marca `IsDeleted = true`, que es lo que hace el script. Las pruebas de `NodosVigentesEnLosEquiposTests` que exigían «el obsoleto no cuenta» siguen valiendo con esa siembra. La que exigía que **dos nodos vivos** se resolvieran por fecha pasa a exigir que el segundo nodo vivo falle con el índice único (2601).
- [ ] **Paso 6.** En local: script con `SoloInforme=1` (**esperado:** 0) y con `SoloInforme=0`; después la migración. Suites `Reportes` y `Rendimiento` de BizCenter en verde. Commit: `Plan Q: un solo nodo vivo por miembro en la genealogia y CurrentUnder por el rango`.

### Tarea 22: límites de rejilla y orden por estructura (P7)

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Grid/TeamGridLimits.cs`
- Modify: `MLMConquerorGlobalEdition.SharedKernel/PagedResult.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/DualTeamService.cs:97-119`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentTeamService.cs:67-73, 117-164`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Grid/GridDataContracts.cs:47` (`GridPagedResult<T>.Limited`)
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Grid/ServerGridAdaptor.cs:93-166` (parámetro `OnLimited`)
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Team/DualTeamMyTeam.razor:45-54` y `EnrollmentTeamMyTeam.razor` (aviso)
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/LimitesDeRejillaTests.cs`

- [ ] **Paso 1.** Pruebas en rojo (InMemory):
  - `Con_mas_de_20000_el_orden_pedido_pasa_a_estructura_y_se_avisa`: un equipo dual con los contadores en 30.000 (como en `MiEquipoDualTotalPorContadoresTests`) y una petición ordenada por `FullName`. El resultado sale con `Limited = true` y ordenado por `BinaryPath`.
  - `Con_20000_o_menos_se_respeta_el_orden`: con los contadores en 2, `Limited = false` y el orden es por nombre.
  - Las mismas dos para inscripción, con `MemberStatistics.EnrollmentTeamSize` como tamaño.
- [ ] **Paso 2.** Implementación:

```csharp
namespace MLMConquerorGlobalEdition.Repository.Grid;

/// <summary>
/// Plan Q (DQ7, P7 del Plan P): por encima de <see cref="FreeMaxRows"/>, Mi equipo solo ordena por estructura (Node o
/// BinaryPath, un recorrido del indice) y solo busca por nombre o id por prefijo (MemberNameSearch). Ordenar o buscar por
/// subcadena un rango de un millon de filas tarda de 2 a 18 s (Plan P, Bloque D).
/// </summary>
public static class TeamGridLimits
{
    public const int FreeMaxRows = 20_000;

    /// <summary>True si el equipo supera el limite: quien llama quita los ordenes pedidos y ordena por estructura.</summary>
    public static bool Limit(GridDataRequest request, int teamSize)
    {
        if (teamSize <= FreeMaxRows) return false;
        request.Sorts = new List<GridSort>();
        return true;
    }
}
```

  `PagedResult<T>` gana `public bool Limited { get; set; }` (el equipo superó `TeamGridLimits.FreeMaxRows`: orden por estructura y búsqueda por prefijo).

  En `DualTeamService.GetMyTeamGridAsync`:
  - el tamaño es `myNode.LeftLegCount + myNode.RightLegCount`;
  - si `TeamGridLimits.Limit(request, tamaño)`, la consulta va `MyTeamRows(myNode).OrderBy(r => r.BinaryPath)` y no se añade el orden por fecha de las líneas 105-107;
  - si no, el orden por defecto pasa de `EnrollDate desc` a `BinaryPath` (DQ7);
  - el resultado lleva `Limited`.

  En `EnrollmentTeamService.GetMyTeamGridAsync` lo mismo:
  - el tamaño sale de `MemberStatistics.EnrollmentTeamSize` del propio miembro (es diferido, pero basta para decidir un límite);
  - el orden por estructura es `g.Node`, que se aplica en `BuildMyTeamQueryable` **antes** de la proyección (`from g in … CurrentUnder(root) orderby g.Node …`), porque la vista no lleva `Node`;
  - `SanitizeGridRequest` deja de añadir `EnrollDate desc` cuando no hay orden.
- [ ] **Paso 3.** La interfaz.
  - **Contrato:** `GridPagedResult<T>` gana `public bool Limited { get; set; }`.
  - **Adaptador:** `ServerGridAdaptor` gana un parámetro, y después de leer el `payload` (`:155-157`) añade `if (OnLimited is not null) await OnLimited(payload?.Data?.Limited ?? false).ConfigureAwait(false);`.

```csharp
    /// <summary>
    /// Opcional (Plan Q). Se llama tras cada lectura con <c>true</c> si el servidor limito el orden y la busqueda por el
    /// tamano del equipo (TeamGridLimits), para que el anfitrion lo explique.
    /// </summary>
    [Parameter] public Func<bool, Task>? OnLimited { get; set; }
```

  - **Las dos rejillas** pasan `OnLimited="AlLimitar"` al adaptador, y muestran sobre la rejilla `@if (_limitado) { <p class="text-muted small">@L["Team.LargeTeamLimited"]</p> }`, con:

```csharp
    private bool _limitado;
    private Task AlLimitar(bool limitado)
    {
        if (_limitado == limitado) return Task.CompletedTask;
        _limitado = limitado;
        return InvokeAsync(StateHasChanged);
    }
```

  - **Textos:** la clave `Team.LargeTeamLimited` va a los recursos de idiomas que ya usan esas rejillas. En español: «Tu equipo es muy grande: se ordena por la estructura del árbol y la búsqueda encuentra nombres o códigos que empiezan por lo que escribes.» En inglés: «Your team is very large: it is sorted by tree structure and search matches names or IDs that start with your text.»
- [ ] **Paso 4.** En verde. Commit: `Plan Q: Mi equipo ordena por estructura por defecto y limita orden y busqueda en equipos de mas de 20.000`.

### Tarea 23: búsqueda por nombre por prefijo (`MemberNameSearch`)

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberProfile.cs:20` (dos propiedades calculadas)
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs:36-37`
- Create: `MLMConquerorGlobalEdition.Repository/Queries/MemberNameSearch.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/DualSubtree.cs` (nuevo `SearchUnder`)
- Modify: `DualTeamService.cs:97-119, 319-337` y `EnrollmentTeamService.cs:117-164`
- Create (Tarea 25): migración `BusquedaYNivelDeAlta`, compartida con el nivel de alta
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerBusquedaPorNombreTests.cs`

- [ ] **Paso 1.** Pruebas en rojo:
  - **SQL Server:** en un equipo sembrado con «Lucía Díaz», «Luciano Pérez» y «Ana Lucía Gómez»:
    - «lucia diaz» encuentra a «Lucía Díaz» (sin tildes ni mayúsculas);
    - «diaz» la encuentra por el apellido;
    - «luci» encuentra a «Lucía Díaz» y a «Luciano Pérez»;
    - «ucia» no encuentra a nadie (es prefijo, no subcadena).
  - **Plan:** la consulta de `DualSubtree.SearchUnder` hace un *Index Seek* sobre `IX_MemberProfiles_SearchName`. Se comprueba con `PlanDeEjecucion` de `Rendimiento`, como las pruebas de plan del Plan M.
  - **InMemory:** el mismo resultado, con la caída a `StartsWith` sobre nombre y apellido.
- [ ] **Paso 2.** Entidad y configuración. Las columnas son calculadas y persistidas, con intercalación sin tildes ni mayúsculas:

```csharp
    /// <summary>Plan Q: «nombre apellido», calculada y persistida (CI_AI), para buscar por prefijo con indice.</summary>
    public string? SearchName { get; private set; }
    /// <summary>Plan Q: «apellido nombre», para encontrar por el apellido.</summary>
    public string? SearchNameReversed { get; private set; }
```

```csharp
        builder.Property(m => m.SearchName)
            .HasComputedColumnSql("CAST(CONCAT([FirstName], N' ', [LastName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI", stored: true);
        builder.Property(m => m.SearchNameReversed)
            .HasComputedColumnSql("CAST(CONCAT([LastName], N' ', [FirstName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI", stored: true);
        builder.HasIndex(m => m.SearchName).HasDatabaseName("IX_MemberProfiles_SearchName").IncludeProperties(m => m.MemberId);
        builder.HasIndex(m => m.SearchNameReversed).HasDatabaseName("IX_MemberProfiles_SearchNameReversed").IncludeProperties(m => m.MemberId);
```

- [ ] **Paso 3.** El componente:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan Q (DQ8): busqueda de miembros por prefijo de nombre, apellido o id, sin tildes ni mayusculas, con indice. Full-Text
/// no esta instalado (FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 0). En InMemory, StartsWith sobre los campos.
/// </summary>
public static class MemberNameSearch
{
    /// <summary>El patron LIKE de un termino: comodines escapados y '%' al final.</summary>
    public static string Pattern(string term) =>
        term.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";

    public static IQueryable<MemberProfile> Apply(AppDbContext db, IQueryable<MemberProfile> perfiles, string term)
    {
        var t = term.Trim();
        if (db.Database.IsSqlServer())
        {
            var p = Pattern(t);
            return perfiles.Where(m => EF.Functions.Like(m.SearchName!, p)
                                    || EF.Functions.Like(m.SearchNameReversed!, p)
                                    || EF.Functions.Like(m.MemberId, p));
        }
        var s = t.ToLowerInvariant();
        return perfiles.Where(m => (m.FirstName + " " + m.LastName).ToLower().StartsWith(s)
                                || (m.LastName + " " + m.FirstName).ToLower().StartsWith(s)
                                || m.MemberId.ToLower().StartsWith(s));
    }
}
```

  `DualSubtree.SearchUnder` sirve para el binario. Parte de los perfiles que casan, unos pocos, y comprueba el rango de la clave; así el optimizador, recompilado por la etiqueta, busca primero por el índice de nombre:

```csharp
    /// <summary>Plan Q: los nodos bajo <paramref name="node"/> cuyo miembro casa con <paramref name="term"/> por prefijo.</summary>
    public static IQueryable<DualTeamEntity> SearchUnder(AppDbContext db, DualTeamEntity node, string term)
    {
        if (!db.Database.IsSqlServer())
        {
            var ids = MemberNameSearch.Apply(db, db.MemberProfiles, term).Select(m => m.MemberId);
            return Under(db, node).Where(d => ids.Contains(d.MemberId));
        }
        var (lo, hi) = DualBinaryPath.SubtreeBounds(Key(node));
        return db.DualTeamTree
            .FromSqlRaw(@"SELECT d.* FROM MemberProfiles m
JOIN DualTeamTree d ON d.MemberId = m.MemberId AND d.IsDeleted = 0
WHERE m.IsDeleted = 0 AND (m.SearchName LIKE @t OR m.SearchNameReversed LIKE @t OR m.MemberId LIKE @t)
  AND d.BinaryPath > @lo AND d.BinaryPath < @hi AND d.MemberId <> @self",
                new SqlParameter("@t", SqlDbType.NVarChar, 210) { Value = MemberNameSearch.Pattern(term) },
                Param("@lo", lo), Param("@hi", hi), new SqlParameter("@self", node.MemberId))
            .TagWith(RecompileTag);
    }
```

  `Key`, `Param` y `RecompileTag` ya existen en `DualSubtree`. Si los nombres del fichero actual difieren, se usan los suyos.
- [ ] **Paso 4.** Uso:
  - **`DualTeamService.GetMyTeamGridAsync`.** Con `request.Search` y el equipo por encima del límite, la base es `Project(DualSubtree.SearchUnder(_db, myNode, request.Search))` y se vacía `request.Search` antes de `ToGridResultAsync`. Con el equipo dentro del límite, se mantiene la búsqueda de siempre por subcadena en todos los campos.
  - **`SearchBinarySubtreeAsync`.** Sustituye su `where … ToLower().Contains(s)` por `SearchUnder` siempre (es la búsqueda del visualizador).
  - **`EnrollmentTeamService.BuildMyTeamQueryable`.** Recibe el término cuando el equipo supera el límite y une con `MemberNameSearch.Apply(_db, _db.MemberProfiles, término)` en lugar de con todos los perfiles.
- [ ] **Paso 5.** Commit tras la migración de la Tarea 25 (las columnas van en ella): `Plan Q: busqueda de miembros por prefijo de nombre, apellido o id con indice y sin tildes`.

### Tarea 24: Clientes y Eventos, por el rango

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentTeamService.cs:603-619` (`BuildCustomerQueryable`, ámbito de equipo)
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Events/TeamEventRegistrationsService.cs:77-96, 116-142`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs` (índice filtrado de clientes)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerClientesTests.cs` y `SqlServerEventosTests.cs` (pruebas de plan)

- [ ] **Paso 1.** Pruebas en rojo, de plan, con `PlanDeEjecucion`:
  - el ámbito de equipo de Clientes parte de `IX_MemberProfiles_Clientes`, un *seek* sobre los clientes, y no recorre el rango del equipo;
  - el total de Eventos no contiene `IsDescendantOf` en el SQL generado (`ToQueryString()`).

  Los resultados de las pruebas existentes no cambian.
- [ ] **Paso 2.** Clientes. Índice filtrado:

```csharp
        // Plan Q: los clientes (MemberType = ExternalMember = 1) son pocos; el reporte de equipo parte de ellos.
        builder.HasIndex(new[] { nameof(MemberProfile.MemberId) }, "IX_MemberProfiles_Clientes")
            .HasFilter("[MemberType] = 1 AND [IsDeleted] = 0")
            .IncludeProperties(m => new { m.EnrollDate, m.SponsorMemberId });
```

  En `BuildCustomerQueryable` (ámbito de equipo), el orden de la consulta pasa a partir de los clientes:

```csharp
            from m in _db.MemberProfiles.AsNoTracking()
            where m.MemberType == MemberType.ExternalMember && m.MemberId != memberId
            join g in _db.GenealogyTree.AsNoTracking().Under(root) on m.MemberId equals g.MemberId
            select …   // la misma proyeccion de :603-619
```

  `Under` y `CurrentUnder` son ya lo mismo (Tarea 21). Se usa `Under` porque es explícito que no hay antijoin.
- [ ] **Paso 3.** Eventos. En `registeredOnly` (`:77-96`) y en `GetTotalsAsync` (`:116-142`), el `let node = … TOP 1 … IsDescendantOf(root)` pasa a una unión con el rango:

```csharp
              join n in _db.GenealogyTree.AsNoTracking().Under(root) on m.MemberId equals n.MemberId
```

  En «Todo el equipo» (`registeredOnly = false`), con más de `TeamGridLimits.FreeMaxRows`:
  - el orden por defecto (`LastName asc` de `Sanitize`, `:178-184`) pasa a `Node`, aplicado antes de la proyección como en la Tarea 22, y el resultado lleva `Limited`;
  - el tamaño es `MemberStatistics.EnrollmentTeamSize` del que mira.
- [ ] **Paso 4.** En verde. Commit (tras la migración de la Tarea 25): `Plan Q: Clientes parte de los clientes y Eventos une con el rango del equipo en lugar del TOP 1 con IsDescendantOf`.

### Tarea 25: Actividad agrupada en SQL con el nivel de alta guardado

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberProfile.cs` (`SignupMembershipLevelId`)
- Create: `MLMConquerorGlobalEdition.Repository/Queries/SignupMembershipLevel.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SelectProducts/SelectProductsHandler.cs:105-111`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Services/SignupActivationService.cs:36-60`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs:23` (índice de fecha de alta con columnas incluidas)
- Create: migración `BusquedaYNivelDeAlta` (columnas de las Tareas 23 y 25 e índices de las Tareas 23, 24 y 25)
- Create: `Scripts/sql/plan-q/02-nivel-de-alta.sql`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentActivityService.cs:50-166`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/EnrollmentActivityServiceTests.cs` y `SqlServerActividadTests.cs`

- [ ] **Paso 1.** Pruebas en rojo.
  - **Regla N2:** `SignupMembershipLevelTests` (InMemory). Un miembro con dos pedidos de membresía: el primero (por `OrderDate`, luego `CreationDate`) con VIP y Lifestyle, y el segundo con Turbo. `RefreshAsync` deja 2. Sin pedidos de membresía, deja `null`.
  - **Actividad:** en `SqlServerActividadTests`, el reporte da las mismas filas que hoy (las pruebas existentes no cambian) y el SQL que manda no contiene `IsDescendantOf` ni una lista de ids (el capturador de la Tarea 1).
- [ ] **Paso 2.** Nivel de alta:

```csharp
    /// <summary>
    /// Plan Q (DQ10): el nivel del alta (regla N2 del Plan J: el nivel mas alto del primer pedido con productos de membresia).
    /// Lo mantiene SignupMembershipLevel.RefreshAsync; null si el miembro no tiene pedido de membresia.
    /// </summary>
    public int? SignupMembershipLevelId { get; set; }
```

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan Q (DQ10): el unico sitio que calcula y guarda MemberProfiles.SignupMembershipLevelId. Regla N2 del Plan J: el
/// primer pedido del miembro con productos de membresia (por OrderDate y CreationDate), su nivel mas alto; no se filtra por
/// estado del pedido. Lo llaman la eleccion de productos del alta y la activacion; 02-nivel-de-alta.sql rellena el pasado.
/// </summary>
public static class SignupMembershipLevel
{
    public static async Task<int?> ComputeAsync(AppDbContext db, string memberId, CancellationToken ct = default)
    {
        var porPedido = await (
            from o in db.Orders.AsNoTracking()
            where o.MemberId == memberId
            join od in db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
            join p in db.Products.AsNoTracking() on od.ProductId equals p.Id
            where p.MembershipLevelId != null
            group p.MembershipLevelId!.Value by new { o.Id, o.OrderDate, o.CreationDate } into g
            select new { g.Key.OrderDate, g.Key.CreationDate, Nivel = g.Max() }).ToListAsync(ct);
        return porPedido.OrderBy(x => x.OrderDate).ThenBy(x => x.CreationDate).Select(x => (int?)x.Nivel).FirstOrDefault();
    }

    /// <summary>Recalcula y asigna en el perfil seguido (quien llama guarda).</summary>
    public static async Task RefreshAsync(AppDbContext db, Domain.Entities.Member.MemberProfile member, CancellationToken ct = default) =>
        member.SignupMembershipLevelId = await ComputeAsync(db, member.MemberId, ct);
}
```

  - **`SelectProductsHandler`:** antes del `SaveChangesAsync` de la línea 111, tras añadir los detalles (`:105`), `await SignupMembershipLevel.RefreshAsync(_db, member, ct);`. Si el manejador no tiene el perfil seguido, se carga por `order.MemberId`. Como `ComputeAsync` consulta la base y los detalles nuevos aún no están guardados, se llama **después** de un primer `SaveChangesAsync` y se guarda otra vez.
  - **Activación:** `SignupActivationService.ActivateAsync`, tras marcar el pedido como completado (`:45-47`), hace `await SignupMembershipLevel.RefreshAsync(_db, member, ct);`.
- [ ] **Paso 3.** Índice de Actividad (sustituye al de la línea 23):

```csharp
        builder.HasIndex(m => m.EnrollDate).HasDatabaseName("IX_MemberProfiles_EnrollDate")
            .IncludeProperties(m => new { m.SponsorMemberId, m.MemberType, m.SignupMembershipLevelId });
```

  `dotnet ef migrations add BusquedaYNivelDeAlta …`. Contiene las columnas de las Tareas 23 y 25, el índice de clientes de la Tarea 24 y este. Aplicar en local.
- [ ] **Paso 4.** `02-nivel-de-alta.sql` rellena el pasado por lotes de 20.000 perfiles, en orden de `Id`, cada lote en su transacción. Es idempotente: solo toca las filas con `SignupMembershipLevelId IS NULL`.

```sql
/*
Plan Q, Tarea 25. Rellena MemberProfiles.SignupMembershipLevelId con la regla N2 del Plan J (SignupMembershipLevel).
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-q/02-nivel-de-alta.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(Confirmo)' NOT IN (N'LOCAL', N'ESCALA')
    THROW 51109, N'Ejecutar con -v Confirmo=LOCAL (o ESCALA en la base de escala).', 1;
IF NOT ((N'$(Confirmo)' = N'LOCAL'  AND DB_NAME() = N'MLMConquerorGlobalEditionDb')
     OR (N'$(Confirmo)' = N'ESCALA' AND DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala'))
    THROW 51110, N'Confirmo no corresponde a esta base.', 1;
IF COL_LENGTH(N'dbo.MemberProfiles', N'SignupMembershipLevelId') IS NULL
    THROW 51111, N'Falta la migracion BusquedaYNivelDeAlta.', 1;

DECLARE @ultimo nvarchar(450) = N'', @n int = 1, @total int = 0;
CREATE TABLE #ids (Id nvarchar(450) COLLATE DATABASE_DEFAULT PRIMARY KEY, MemberId nvarchar(20) COLLATE DATABASE_DEFAULT NOT NULL);
WHILE @n > 0
BEGIN
    TRUNCATE TABLE #ids;
    INSERT INTO #ids SELECT TOP (20000) Id, MemberId FROM MemberProfiles WHERE Id > @ultimo ORDER BY Id;
    SET @n = @@ROWCOUNT;
    IF @n = 0 BREAK;
    SELECT @ultimo = MAX(Id) FROM #ids;
    BEGIN TRAN;
    UPDATE m SET SignupMembershipLevelId = x.Nivel
    FROM MemberProfiles m
    JOIN #ids i ON i.Id = m.Id
    CROSS APPLY (SELECT TOP 1 MAX(p.MembershipLevelId) AS Nivel
                 FROM Orders o
                 JOIN OrderDetails od ON od.OrderId = o.Id
                 JOIN Products p ON p.Id = od.ProductId AND p.IsDeleted = 0
                 WHERE o.MemberId = m.MemberId AND p.MembershipLevelId IS NOT NULL
                 GROUP BY o.Id, o.OrderDate, o.CreationDate
                 ORDER BY o.OrderDate, o.CreationDate) x
    WHERE m.SignupMembershipLevelId IS NULL;
    SET @total += @@ROWCOUNT;
    COMMIT;
END
DECLARE @msg nvarchar(200) = CONCAT(N'Perfiles con nivel de alta asignado: ', @total);
RAISERROR(@msg, 0, 1) WITH NOWAIT;
```

  **Contraste (solo lectura) tras ejecutarlo en local:** para 2.000 miembros al azar, `SignupMembershipLevel.ComputeAsync` coincide con la columna. Va como prueba `[SqlServerFact]` en `SqlServerActividadTests`.
- [ ] **Paso 5.** `EnrollmentActivityService.GetGridAsync`, una sola consulta agrupada y paginada en SQL. Sustituye las líneas 74-165 (la lectura del nodo del que mira, `:58-63`, no cambia):

```csharp
        // Plan Q: las altas del rango cuyo patrocinador cuelga del que mira, agrupadas por patrocinador EN SQL. El nodo del
        // patrocinador es unico (UX_GenealogyTree_MemberId_Vivo); el nivel de alta viene guardado (SignupMembershipLevelId).
        // Antes: TOP 1 correlacionado con IsDescendantOf por alta y el pivote en memoria (mas de 30 s con 1.000.000).
        var porPatrocinador =
            from m in _db.MemberProfiles.AsNoTracking()
            where m.EnrollDate >= start && m.EnrollDate < end && m.SponsorMemberId != null
            join g in _db.GenealogyTree.AsNoTracking().Under(root) on m.SponsorMemberId equals g.MemberId
            group new { m.MemberType, m.SignupMembershipLevelId } by new { g.MemberId, g.Level } into grp
            select new
            {
                grp.Key.MemberId,
                grp.Key.Level,
                Customers   = grp.Count(x => x.MemberType == MemberType.ExternalMember),
                Ambassadors = grp.Count(x => x.MemberType != MemberType.ExternalMember),
                Level1      = grp.Count(x => x.SignupMembershipLevelId == 1),
                Level2      = grp.Count(x => x.SignupMembershipLevelId == 2),
                Level3      = grp.Count(x => x.SignupMembershipLevelId == 3),
                Level4      = grp.Count(x => x.SignupMembershipLevelId == 4),
                OtherLevel  = grp.Count(x => x.SignupMembershipLevelId == null
                                          || (x.SignupMembershipLevelId != 1 && x.SignupMembershipLevelId != 2
                                              && x.SignupMembershipLevelId != 3 && x.SignupMembershipLevelId != 4))
            };

        var query =
            from a in porPatrocinador
            join p in _db.MemberProfiles.AsNoTracking() on a.MemberId equals p.MemberId
            select new EnrollmentActivityRowView
            {
                MemberId             = a.MemberId,
                FullName             = p.FirstName + " " + p.LastName,
                Level                = a.Level - me.Level,
                Customers            = a.Customers,
                Ambassadors          = a.Ambassadors,
                Level1               = a.Level1,
                Level2               = a.Level2,
                Level3               = a.Level3,
                Level4               = a.Level4,
                OtherLevel           = a.OtherLevel,
                EnrollmentTeamPoints = _db.MemberStatistics.Where(s => s.MemberId == a.MemberId)
                                          .OrderBy(s => s.Id).Select(s => s.EnrollmentPoints).FirstOrDefault()
            };

        if (request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(EnrollmentActivityRowView.Ambassadors), Direction = "desc" });

        return Result<PagedResult<EnrollmentActivityRowView>>.Success(
            await query.ToGridResultAsync(request, SearchableFields, ct: ct));
```

  - **Cambios respecto a hoy**, que la prueba de equivalencia del Paso 1 vigila:
    - `Level` era `Min(SponsorLevel)` y ahora es el nivel del único nodo del patrocinador (son iguales con un nodo por miembro);
    - la búsqueda, el orden y la página van en SQL;
    - las columnas de la vista deben ser asignables. Si `EnrollmentActivityRowView` tiene `set` privados o calculados, se ajusta la vista: ver `Repository/Services/Teams/EnrollmentActivityDtos.cs` o el fichero donde esté (`grep -rn "class EnrollmentActivityRowView"`).
  - Se borran los dos `ToDictionary` y el pivote en memoria (`:93-160`), y `Empty` deja de usarse salvo en la salida sin nodo.
- [ ] **Paso 6.** Ejecutar el script en local. Suites `Reportes` en verde. Commit con las Tareas 23 y 24: `Plan Q: nivel de alta guardado, busqueda por prefijo, clientes y actividad agrupada en SQL con su migracion y su relleno`.

### Tarea 26: Mi equipo de inscripción, total y página

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentTeamService.cs:117-132`

- [ ] **Paso 1.** Prueba en rojo, de plan (`SqlServerPlanesDeEjecucionTests`). La página 1.000 del equipo de la raíz mayor local, sin búsqueda y con el orden por defecto, es un *Index Seek* sobre `IX_GenealogyTree_Node` sin `Sort`, y el conteo no contiene la antijoin (`NOT EXISTS`) en el SQL generado.
- [ ] **Paso 2.** Con las Tareas 21 y 22 ya está; si el plan aún muestra `Sort`, el `orderby g.Node` se mueve a justo después de `Under(root)` y antes del `join`. En verde. Commit: `Plan Q: Mi equipo de inscripcion pagina por Node sin ordenar el rango`.

### Tarea 27: ficha de lo que cambia para el usuario

**Files:**
- Modify: este plan (sección «Riesgos»: Q-R5 a Q-R8 ya lo recogen)

- [ ] **Paso 1.** Revisar con las rejillas abiertas en local (BizCenter, usuario con equipo grande, AMB-700829):
  - el aviso de equipo grande sale;
  - la búsqueda «lucia» encuentra por nombre y apellido;
  - Actividad, Clientes y Eventos dan las mismas cifras que en `master` (comparar a mano tres filas de cada uno).

  Anotar el resultado en las notas del bloque.

### Tarea 28: medición rápida local del bloque

- [ ] **Paso 1.** `SqlServerMedicionPlanMTests` (Actividad, Clientes, Eventos y Mi equipo sobre la raíz mayor local) y `SqlServerMedicionPlanPTests` (búsqueda en el binario), con su rasgo. Anotar en «Resultados medidos». Commit: `Plan Q: medicion local de los reportes`.

---

## Bloque 6: migraciones desde cero

**Hecho.** `20260405130705_HelpdeskModule` se generó contra una instantánea vacía: crea 64 tablas, entre ellas `AspNetRoles`, `AspNetUsers`, `Tickets` y todas las de `InitialCreate`. En las bases reales no se ejecutó: `fix_helpdesk_tables.sql`, en la raíz del repositorio, creó con `IF NOT EXISTS` solo las tablas del helpdesk que faltaban y volvió a insertar el historial. `20260416232000_RepairTicketsTableSchema` completó después `Tickets`, que `InitialCreate` había creado con 18 columnas. En una base vacía, `HelpdeskModule` falla con 2714 en su primera tabla.

**Decisión (DQ11).** Se reproduce en la base vacía lo que pasó en las reales: las operaciones de `HelpdeskModule` que tocan una tabla que ya existía antes de la migración no se ejecutan. Una base que ya tiene `HelpdeskModule` en su historial no la vuelve a ejecutar, así que nada cambia para ella.

### Tarea 29: prueba de base vacía (en rojo)

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Migraciones/MigracionesDesdeCeroTests.cs`

- [ ] **Paso 1.** La prueba. Es opcional, porque tarda minutos: solo corre con `MLM_MIGRACIONES_DESDE_CERO=1`. Solo crea y borra dos bases con nombres exactos en la instancia local.

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Migraciones;

public sealed class DesdeCeroFactAttribute : FactAttribute
{
    public DesdeCeroFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MLM_MIGRACIONES_DESDE_CERO") != "1")
            Skip = "Define MLM_MIGRACIONES_DESDE_CERO=1: crea y borra MLMConquerorGlobalEditionDb_DesdeCero y _Modelo en la instancia local.";
        else if (!SqlServerReportes.Reachable)
            Skip = "SQL Server no alcanzable.";
    }
}

/// <summary>
/// Plan Q, Bloque 6: una base vacia se construye con las migraciones y queda igual que el modelo (EnsureCreated). No toca
/// la base local ni su historial: solo las dos bases de esta prueba, que se borran al final.
/// </summary>
public class MigracionesDesdeCeroTests
{
    private const string DesdeCero = "MLMConquerorGlobalEditionDb_DesdeCero";
    private const string Modelo = "MLMConquerorGlobalEditionDb_Modelo";
    private static readonly string[] Permitidas = { DesdeCero, Modelo };

    private static string Cadena(string bd) =>
        $"Server=.;Database={bd};Integrated Security=true;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true;";

    private static AppDbContext Contexto(string bd) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Cadena(bd), o => o.CommandTimeout(600)).Options);

    [DesdeCeroFact]
    public async Task Una_base_vacia_se_construye_con_las_migraciones_e_igual_al_modelo()
    {
        await BorrarAsync(DesdeCero);
        await BorrarAsync(Modelo);
        try
        {
            await using (var db = Contexto(DesdeCero)) await db.Database.MigrateAsync();
            await using (var db = Contexto(Modelo)) await db.Database.EnsureCreatedAsync();

            var migrada = await EsquemaAsync(DesdeCero);
            var modelo = await EsquemaAsync(Modelo);
            migrada.Except(modelo).Should().BeEmpty("lo que las migraciones crean de mas o distinto del modelo");
            modelo.Except(migrada).Should().BeEmpty("lo que el modelo tiene y las migraciones no crean");
            (await ObjetosAsync(DesdeCero)).Should().Contain(new[]
                { "DualChildKey", "Numeros", "DualAncestorKeys", "GenealogyAncestors", "DualTeamRootOrdinal" });
        }
        finally
        {
            await BorrarAsync(DesdeCero);
            await BorrarAsync(Modelo);
        }
    }

    /// <summary>Columnas (tipo, longitud, nulos) e indices (unico, filtro) de las tablas de usuario, sin el historial.</summary>
    private static async Task<HashSet<string>> EsquemaAsync(string bd)
    {
        const string sql = @"
SELECT CONCAT(c.TABLE_NAME, '.', c.COLUMN_NAME, ':', c.DATA_TYPE, ':', c.CHARACTER_MAXIMUM_LENGTH, ':', c.IS_NULLABLE)
FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_NAME <> '__EFMigrationsHistory'
UNION ALL
SELECT CONCAT(t.name, ' ix ', i.name, ':', i.is_unique, ':', i.filter_definition)
FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.name IS NOT NULL AND t.name <> '__EFMigrationsHistory';";
        await using var c = new SqlConnection(Cadena(bd));
        await c.OpenAsync();
        await using var cmd = new SqlCommand(sql, c);
        await using var r = await cmd.ExecuteReaderAsync();
        var s = new HashSet<string>();
        while (await r.ReadAsync()) s.Add(r.GetString(0));
        return s;
    }

    private static async Task<List<string>> ObjetosAsync(string bd)
    {
        await using var c = new SqlConnection(Cadena(bd));
        await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT name FROM sys.objects WHERE type IN ('IF', 'FN', 'TF', 'SO')", c);
        await using var r = await cmd.ExecuteReaderAsync();
        var l = new List<string>();
        while (await r.ReadAsync()) l.Add(r.GetString(0));
        return l;
    }

    private static async Task BorrarAsync(string bd)
    {
        if (!Permitidas.Contains(bd)) throw new InvalidOperationException($"{bd} no es una base de esta prueba.");
        await using var c = new SqlConnection("Server=.;Database=master;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;");
        await c.OpenAsync();
        await using var cmd = new SqlCommand(
            $"IF DB_ID(N'{bd}') IS NOT NULL BEGIN ALTER DATABASE [{bd}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{bd}]; END", c);
        await cmd.ExecuteNonQueryAsync();
    }
}
```

- [ ] **Paso 2.** `$env:MLM_MIGRACIONES_DESDE_CERO = "1"; dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter FullyQualifiedName~MigracionesDesdeCero`. **Esperado:** falla en `MigrateAsync` con `There is already an object named 'AspNetRoles' in the database` (2714).
- [ ] **Paso 3.** Commit: `Plan Q: prueba que construye una base vacia con las migraciones y la compara con el modelo (en rojo)`.

### Tarea 30: `HelpdeskModule` solo crea lo que no existía

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/Infraestructura/SoloSiLaTablaEsNueva.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/Infraestructura/MigracionesSqlServerConGuardas.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs:219-221` (se registra el generador)
- Modify: `MLMConquerorGlobalEdition.Repository/Migrations/20260405130705_HelpdeskModule.cs:14-15` (al principio de `Up`) y el final de `Up` (justo antes de `:2728`)

- [ ] **Paso 1.** La marca:

```csharp
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace MLMConquerorGlobalEdition.Repository.Migrations.Infraestructura;

/// <summary>
/// Plan Q (DQ11): una migracion que se genero contra una instantanea vacia (HelpdeskModule) vuelve a crear tablas que ya
/// crearon las anteriores. En las bases reales se resolvio a mano con fix_helpdesk_tables.sql (solo las tablas nuevas). Aqui
/// se hace lo mismo de forma automatica: al empezar se guarda la lista de tablas que ya existen, y cada operacion marcada
/// que toca una de ellas no se ejecuta. MigracionesSqlServerConGuardas pone la guarda.
/// </summary>
public static class SoloSiLaTablaEsNueva
{
    public const string Anotacion = "PlanQ:SoloSiLaTablaEsNueva";
    public const string TablaDePrevias = "__PlanQ_TablasPrevias";

    public static void Empezar(MigrationBuilder mb) => mb.Sql($@"
IF OBJECT_ID(N'dbo.{TablaDePrevias}') IS NOT NULL DROP TABLE dbo.{TablaDePrevias};
SELECT name COLLATE DATABASE_DEFAULT AS Nombre INTO dbo.{TablaDePrevias} FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo');");

    /// <summary>Marca todas las operaciones de tabla que haya ahora en <paramref name="mb"/> (llamar al final del Up generado).</summary>
    public static void Marcar(MigrationBuilder mb)
    {
        foreach (var op in mb.Operations)
            if (Tabla(op) is not null) op.AddAnnotation(Anotacion, true);
    }

    public static void Terminar(MigrationBuilder mb) => mb.Sql($"DROP TABLE IF EXISTS dbo.{TablaDePrevias};");

    public static string? Tabla(MigrationOperation op) => op switch
    {
        CreateTableOperation c => c.Name,
        CreateIndexOperation i => i.Table,
        AddForeignKeyOperation f => f.Table,
        InsertDataOperation d => d.Table,
        _ => null
    };
}
```

- [ ] **Paso 2.** El generador. Solo cambia algo cuando la lista trae operaciones marcadas, es decir, solo en `HelpdeskModule`:

```csharp
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;

namespace MLMConquerorGlobalEdition.Repository.Migrations.Infraestructura;

/// <summary>
/// Plan Q (DQ11): el generador de SQL Server de siempre, salvo para las operaciones marcadas con
/// SoloSiLaTablaEsNueva.Anotacion: cada comando suyo se envuelve en
/// IF NOT EXISTS (tabla en __PlanQ_TablasPrevias) EXEC(N'…'). EXEC aplaza la compilacion: un INSERT o un indice sobre
/// columnas que la tabla vieja no tiene no rompe el lote aunque no se ejecute.
/// </summary>
public sealed class MigracionesSqlServerConGuardas : SqlServerMigrationsSqlGenerator
{
    public MigracionesSqlServerConGuardas(MigrationsSqlGeneratorDependencies dependencies, ICommandBatchPreparer commandBatchPreparer)
        : base(dependencies, commandBatchPreparer)
    {
    }

    public override IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations, IModel? model = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        if (!operations.Any(o => o.FindAnnotation(SoloSiLaTablaEsNueva.Anotacion) is not null))
            return base.Generate(operations, model, options);

        var resultado = new List<MigrationCommand>();
        foreach (var op in operations)
        {
            var comandos = base.Generate(new[] { op }, model, options);
            if (op.FindAnnotation(SoloSiLaTablaEsNueva.Anotacion) is null || SoloSiLaTablaEsNueva.Tabla(op) is not { } tabla)
            {
                resultado.AddRange(comandos);
                continue;
            }
            foreach (var c in comandos)
            {
                var texto = $"IF NOT EXISTS (SELECT 1 FROM dbo.[{SoloSiLaTablaEsNueva.TablaDePrevias}] WHERE Nombre = N'{tabla}')\n"
                          + $"    EXEC(N'{c.CommandText.Replace("'", "''")}');\n";
                var comando = Dependencies.CommandBuilderFactory.Create().Append(texto).Build();
                resultado.Add(new MigrationCommand(comando, Dependencies.CurrentContext.Context, Dependencies.Logger, c.TransactionSuppressed));
            }
        }
        return resultado;
    }
}
```

  En `AppDbContext.OnConfiguring`, dentro del `if` de SQL Server que ya existe (`:219-221`):

```csharp
        if (optionsBuilder.Options.FindExtension<SqlServerOptionsExtension>() is not null)
        {
            new SqlServerDbContextOptionsBuilder(optionsBuilder).UseHierarchyId();
            // Plan Q (DQ11): guardas para HelpdeskModule en una base vacia; con cualquier otra migracion es el de siempre.
            optionsBuilder.ReplaceService<IMigrationsSqlGenerator, MigracionesSqlServerConGuardas>();
        }
```

- [ ] **Paso 3.** En `HelpdeskModule.Up`: la primera línea es `SoloSiLaTablaEsNueva.Empezar(migrationBuilder);`, y al final, antes de cerrar `Up`, `SoloSiLaTablaEsNueva.Marcar(migrationBuilder); SoloSiLaTablaEsNueva.Terminar(migrationBuilder);`. Se añade un comentario de cabecera con el motivo y el enlace a este plan. No se toca nada más del fichero, ni del `Designer`, ni del `Down`.
- [ ] **Paso 4.** Volver a ejecutar la prueba de la Tarea 29.
  - **Si `MigrateAsync` falla después**, en otra migración, se aplica la misma regla: se marca solo si el fallo es «el objeto ya existe porque lo creó `HelpdeskModule` o una anterior». Cualquier otro fallo se anota en «Desviaciones» con su arreglo concreto antes de seguir.
  - **Si la comparación con el modelo da diferencias**, cada una se corrige con una migración nueva o pasa a una lista de excepciones con nombre y motivo, dentro de la prueba.
- [ ] **Paso 5.** Comprobar que la base local no cambia:
  - `dotnet ef migrations list … --connection "Server=.;Database=MLMConquerorGlobalEditionDb;…"` lista todas como aplicadas;
  - `SELECT COUNT(*) FROM __EFMigrationsHistory` da lo mismo antes y después;
  - `dotnet ef migrations script --idempotent … -o C:\temp\idempotente.sql` contiene las guardas solo en el bloque de `HelpdeskModule`.
- [ ] **Paso 6.** En verde. Commit: `Plan Q: HelpdeskModule solo crea las tablas que no existian y una base vacia se construye con las migraciones`.

### Tarea 31: `migrate-rds.sql` y la base de escala

**Files:**
- Modify: `DeploymentScripts/migrate-rds.sql` (se regenera)
- Modify: `Scripts/sql/plan-p/escala/00-crear-bd-escala.sql` (comentario de la desviación del Plan P)

- [ ] **Paso 1.** Regenerar el script idempotente, sin conexión (solo genera texto). Sustituye la nota de «desactualizado» del Plan P:

```bash
dotnet ef migrations script --idempotent --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.AdminAPI -o DeploymentScripts/migrate-rds.sql
```

- [ ] **Paso 2.** En `00-crear-bd-escala.sql`, el comentario que explica el clonado por el error 2714 se actualiza: ya no es obligatorio, pero se mantiene porque clonar el esquema es instantáneo y copia los catálogos.
- [ ] **Paso 3.** Commit: `Plan Q: script idempotente de migraciones regenerado y nota de la base de escala al dia`.

---

## Bloque 7: medición final compartida

Generar la base de escala cuesta unos 45 min con 500.000 y 2 h 40 con 1.000.000. Por eso se mide **una vez** al final, con todo el código del plan: cada tamaño se genera, se mide entero y se borra. No hace falta regenerar entre bloques: las pruebas rápidas de cada bloque van contra la base local.

### Tarea 32: el generador, al día con el Plan Q

**Files:**
- Modify: `Scripts/sql/plan-p/escala/altas/g-binario.sql:34-36, 51-53, 80-82` (sin `HierarchyPath`; el padre se busca por `MemberId`)
- Modify: `Scripts/sql/plan-p/escala/altas/e-inscripcion.sql:56-68` (`escala.Pares` con `Distancia`)
- Modify: `Scripts/sql/plan-p/escala/altas/f-ascendientes.sql:25-58`
- Modify: `Scripts/sql/plan-p/escala/01-generar-altas.ps1` (pasos nuevos al final)
- Modify: `Scripts/sql/plan-p/escala/02-verificar-escala.sql:35-44, 104-108`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/EscritoresDelBinarioTests.cs:22-50` (revisar que los ficheros revisados siguen existiendo)

- [ ] **Paso 1.** `escala.Pares` gana `Distancia`: 1 para el patrocinador y 10 para el décimo ascendiente. `STRING_SPLIT(…, N'/', 1)` da el ordinal (SQL Server 2022):

```sql
SELECT g.MemberId AS T, CAST(s.value AS nvarchar(20)) COLLATE DATABASE_DEFAULT AS E,
       CAST(CASE WHEN s.value = p.MemberId THEN 1 ELSE 0 END AS bit) AS Directo,
       (LEN(g.HierarchyPath) - LEN(REPLACE(g.HierarchyPath, N'/', N''))) - CAST(s.ordinal AS int) AS Distancia,
       a.Alta, a.Activa, a.Email, a.Puntos
INTO escala.Pares
FROM GenealogyTree g
JOIN escala.Altas a ON a.MemberId = g.MemberId
JOIN escala.Altas p ON p.n = a.GenPadre
CROSS APPLY STRING_SPLIT(g.HierarchyPath, N'/', 1) s
WHERE s.value <> N'' AND s.value <> g.MemberId;
```

  En «/A/B/C/» (C, el alta) hay 4 barras y los ordinales son A = 2, B = 3, C = 4, así que B da 1 y A da 2.
- [ ] **Paso 2.** `f-ascendientes.sql` deja lo que deja un alta del Plan Q:
  - **Cola y Avisos:** `WHERE Distancia <= 10`.
  - **Deltas:** una fila por alta, la del patrocinador.

```sql
    INSERT INTO MemberStatisticDeltas WITH (TABLOCK) (MemberId, Scope, EnrollmentPointsDelta, EnrollmentTeamSizeDelta,
                                                      QualifiedSponsoredMembersDelta, IsApplied, AppliedAt, SourceMemberId, CreationDate, CreatedBy)
    SELECT E, 1, Puntos, 1, 1, 1, DATEADD(second, 30, Activa), T, Activa, Email
    FROM escala.Pares WHERE Directo = 1;
```

  `MemberStatistics` (`d-cobros.sql:97-104`) ya escribe `EnrollmentTeamSize` desde `escala.Equipo`: no cambia.
- [ ] **Paso 3.** `g-binario.sql`: se quita la columna `HierarchyPath` de los tres `INSERT` y su `CONCAT`. `DualLegDeltas` queda vacía: el generador escribe las piernas finales con `04-contadores-y-colas.sql`, como hoy.
- [ ] **Paso 4.** `01-generar-altas.ps1`, después de `04-contadores-y-colas.sql` y antes de `sp_updatestats`:

```powershell
& sqlcmd -S . -d MLMConquerorGlobalEditionDb_Escala -E -C -b -I -v Confirmo=ESCALA -v SoloInforme=0 -i (Join-Path $PSScriptRoot '..\..\plan-q\01-nodos-de-inscripcion-repetidos.sql')
& sqlcmd -S . -d MLMConquerorGlobalEditionDb_Escala -E -C -b -I -v Confirmo=ESCALA -i (Join-Path $PSScriptRoot '..\..\plan-q\02-nivel-de-alta.sql')
```

  Con el mismo manejo de errores que las líneas vecinas: si `sqlcmd` falla, el orquestador se detiene.
- [ ] **Paso 5.** `02-verificar-escala.sql`:
  - fuera la comprobación de rutas del binario (`:35-44`); se conserva la de clave y profundidad con `dbo.DualChildKey`;
  - las filas por alta esperadas pasan a: `RankEvaluationQueue` y `MemberNotifications` = Σ min(ascendientes, 10), y `MemberStatisticDeltas` = 1 por alta con patrocinador;
  - nuevas comprobaciones: 0 miembros con dos nodos vivos, 0 perfiles con alta pero sin `SignupMembershipLevelId`, y `DualLegDeltas` vacía.
- [ ] **Paso 6.** Commit: `Plan Q: el generador de escala deja lo que deja un alta del Plan Q y la verificacion lo comprueba`.

### Tarea 33: mediciones nuevas y objetivos

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/EscalaBinarioTests.cs`

- [ ] **Paso 1.** Cambios en las pruebas de escala:
  - **`Colocar_una_hoja_a_unos_8500_niveles` (`:158-176`):** objetivo < 300 ms. La subida ya no va en la colocación.
  - **`Diez_colocaciones_simultaneas_en_la_misma_linea_profunda` (`:197-239`):** el objetivo sigue siendo p95 < 2 s, que es el pedido.
  - **Nueva `Vaciar_los_deltas_de_1000_colocaciones_en_la_linea_profunda`:**
    - encola 1.000 deltas de hoja sobre nodos abiertos a más de 8.000 niveles, dentro de una transacción que se deshace;
    - mide `LegPointMaintenance.DrainAsync`;
    - objetivo: menos de 60 s en total, y las piernas de ESC-M suben exactamente 1.000.
  - **`Mover_un_subarbol_de_50000_nodos_y_cuanto_bloquea` (`:247-305`):** el objetivo del movimiento sigue en < 30 s. Las colocaciones a la vez siguen sin poder fallar; si fallan, se anota (Q-R3).
  - **`Mi_equipo_binario_con_busqueda_por_nombre` (`:129-137`):** la búsqueda «Lucia Diaz» va ahora por prefijo. Objetivo < 2 s.
  - **`Mi_equipo_binario_primera_pagina_y_pagina_1000` (`:106-127`):** con el orden por defecto, que ahora es la clave. Objetivo < 2 s.
  - **`Informe_de_la_base_de_escala` (`:58-84`):** informa del tamaño de `DualTeamTree` sin ruta.
- [ ] **Paso 2.** Commit: `Plan Q: objetivos de escala del Plan Q y medicion del vaciado de la cola`.

### Tarea 34: generar, medir y borrar (500.000 y 1.000.000)

- [ ] **Paso 1.** Aplicar en local todas las migraciones y scripts del plan (si no se hizo ya bloque a bloque). El clon de escala parte del esquema de la local.
- [ ] **Paso 2.** Para `N` en 500.000 y 1.000.000, uno detrás de otro:

```powershell
powershell -File Scripts/sql/plan-p/escala/01-generar-altas.ps1 -Nodos N -Salida C:\temp\escala-N-plan-q.txt -Recrear
$env:MLM_ESCALA_SQLSERVER = "Server=.;Database=MLMConquerorGlobalEditionDb_Escala;Integrated Security=true;TrustServerCertificate=True;Encrypt=True"
dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --settings MLMConquerorGlobalEdition.BizCenter.Tests/Medicion.runsettings --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
dotnet test MLMConquerorGlobalEdition.RankEngine.Tests --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
sqlcmd -S . -d master -E -C -b -Q "ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE MLMConquerorGlobalEditionDb_Escala;"
```

- [ ] **Paso 3.** Rellenar la tabla de «Resultados medidos» (abajo) con las medianas y, por cada rojo, su propuesta en «Lo que no cumple».
- [ ] **Paso 4.** Commit: `Plan Q: mediciones del binario y los reportes con 500.000 y 1.000.000 altas tras el Plan Q`.

### Resultados medidos

Las columnas «antes» de 400.000 y 1.000.000 vienen del Bloque D del Plan P. La de 500.000 antes, de la Tarea 0. ✅ cumple el objetivo; ❌ no lo cumple.

| Medición | Objetivo | 400.000 antes | 1.000.000 antes | 500.000 antes | 500.000 después | 1.000.000 después |
|---|---|---|---|---|---|---|
| Progreso de rango de ESC-M | < 1 s | > 30 s ❌ | > 30 s ❌ | | | |
| Colocar una hoja a 8.507 niveles | < 300 ms | 1.549 | 3.457 ❌ | | | |
| 10 colocaciones a la vez en la línea profunda (p95) | < 2 s | 14.249 ❌ | 29.682 ❌ | | | |
| Vaciar 1.000 deltas en la línea profunda | < 60 s | — | — | — | | |
| Mover ESC-P (50.000) | < 30 s | 126.621 ❌ | 227.134 ❌ | | | |
| Colocaciones durante el movimiento (fallan) | 0 de 15 | 13 ❌ | 13 ❌ | | | |
| Mi equipo binario, primera página | < 2 s | 2.089 ❌ | 1.692 ✅ ¹ | | | |
| Mi equipo binario, página 1.000 | < 2 s | 2.792 ❌ | 2.323 ❌ ¹ | | | |
| Mi equipo binario, búsqueda por nombre | < 2 s | 4.313 ❌ | 18.510 ❌ | | | |
| Mi equipo de inscripción: conteo / p1 / p1000 | < 2 s | 468 / 1.049 / 1.868 ✅ | 2.776 / 4.545 / 7.082 ❌ | | | |
| Actividad, 3 meses | < 2 s | 5.344 ❌ | > 30 s ❌ | | | |
| Clientes de todo el equipo | < 2 s | 4.883 ❌ | 24.231 ❌ | | | |
| Eventos (todo el equipo, 200 inscritos) | < 2 s | 1.457 ✅ | 7.163 ❌ | | | |
| Cola sin caché (residual, sin cambio) | — | 8.631 | 12.479 | | | |
| Tamaño de DualTeamTree | — | 29,7 GB | 77,9 GB | | | |
| Filas por tabla de ascendientes (cola, deltas, avisos) | — | 11,7 M | 29,4 M | | | |

¹ Tras el Plan B del conteo (Plan P, Bloque D).

---

## Cierre

### Tarea 35: guion de despliegue y verificación final

**Files:**
- Create: `Scripts/sql/plan-q/00-despliegue-plan-q.md`

- [ ] **Paso 1.** Verificación completa:
  - `dotnet build`;
  - `dotnet test` de todas las suites, en serie;
  - las reglas `SinRutaEnElBinarioTests`, `EscritoresDelBinarioTests` y `SinPrefijoEnInscripcionTests` en verde;
  - la prueba desde cero con `MLM_MIGRACIONES_DESDE_CERO=1`;
  - `02-verificar-escala.sql` a 0 en la última base de escala antes de borrarla.
- [ ] **Paso 2.** El guion (abajo) en el fichero. Commit: `Plan Q: guion de despliegue de la escala del binario y los reportes`.

## Despliegue: ventana única, servicios parados

| Paso | Qué | Notas |
|---|---|---|
| 1 | Parar los servicios, incluido Hangfire | Nadie escribe ni migra al arrancar |
| 2 | Copia de seguridad (`COPY_ONLY`, comprimida) | Volver atrás de `RetiraRutaDelBinario` es restaurarla (Q-R10) |
| 3 | `01-nodos-de-inscripcion-repetidos.sql` con `SoloInforme=1` | En producción, **parar aquí si sale más de 0** y llevar el número a Nick (Q2) |
| 4 | El mismo script con `SoloInforme=0` | Solo si el paso 3 lo pide |
| 5 | Migraciones, en orden: `SuscripcionesActivasPorMiembro`, `ColaDeDeltasDePierna`, `RetiraRutaDelBinario`, `DeltaPorLineaDePatrocinio`, `UnNodoPorMiembro`, `BusquedaYNivelDeAlta` | `dotnet ef database update … --connection …`. Las guardas 51100 y 51108 detienen sin tocar nada |
| 6 | `02-nivel-de-alta.sql` | Por lotes; idempotente |
| 7 | Verificación: 0 miembros con dos nodos vivos, `DualLegDeltas` vacía, columnas e índices nuevos presentes | Solo lectura |
| 8 | Arrancar los servicios | El trabajo `apply-dual-leg-deltas` empieza a correr |
| 9 | `03-recuperar-espacio-de-la-ruta.sql`, con los servicios en marcha | Fuera de la ventana; puede tardar con mucha ruta |

Los deltas antiguos de `MemberStatisticDeltas` que queden sin aplicar (`Scope = 0`) los aplica el trabajo nuevo igual que antes.

## Riesgos

| Id | Riesgo | Mitigación |
|---|---|---|
| Q-R1 | **Piernas diferidas (DQ1).** Lo que se muestra (Mi equipo, visualizador, estadísticas, progreso de rango, evaluación de rango) puede ir hasta 1 minuto más el atraso de la cola por detrás. La regla de colocación no lee piernas. | Las comisiones y la instantánea vacían la cola antes de leer (Tarea 9). La evaluación de rango corre cada 5 minutos, por detrás del vaciado de cada minuto. Si hiciera falta exactitud en una pantalla, su manejador llama a `DrainAsync(maxBatches: 1)`. |
| Q-R2 | **Coste del vaciado.** Una tanda calcula Σ profundidad de sus arranques: 200 × 8.500 = 1,7 millones de filas, y un pico de altas en la línea profunda puede acumular atraso. | `BatchSize` acota cada tanda. Se mide «vaciar 1.000» en el Bloque 7. Si no cumple, la mejora es agrupar en C# los arranques de la misma línea antes de expandir: solo hace falta el ascendiente común más profundo y las ramas por debajo. |
| Q-R3 | **Un movimiento grande sigue tomando el árbol en exclusivo** (punto 9 del Plan P). Sin la ruta ni la subida, mover 50.000 debería bajar de 127 s a unos 15 a 40 s; mientras dura, las colocaciones esperan hasta 15 s y fallan con `PLACEMENT_BUSY`. | Medir en el Bloque 7. Si sigue por encima de 15 s, la siguiente tanda debe encolar el movimiento como trabajo de fondo con aviso al usuario, o reescribir las claves por tandas fuera del candado exclusivo. |
| Q-R4 | **Cola sin caché** (punto 7 del Plan P): 8,6 y 12,5 s. | Sin cambio. La caché `DualTeamLegFrontiers` es obligatoria; `04` la reconstruye y los movimientos solo vacían las afectadas. |
| Q-R5 | **Evaluación de rango de los ascendientes a más de 10 niveles** (DQ5): pasa de unos 5 minutos a la fase 2 nocturna de `RankEvaluationSweepJob`. Un rango alcanzado el último día de un periodo podría constar al día siguiente. | Pregunta Q1. Si Nick lo rechaza, se sube `GenealogyUpline.EnrollmentLevels` o se encola por encima de la cota solo a quien esté a menos de X puntos de su siguiente rango. |
| Q-R6 | **`DOWNLINE_ENROLLED` solo a 10 niveles.** Los más lejanos dejan de recibir el aviso. | Nadie lee la tabla; el aviso es informativo. Misma cota que I3. |
| Q-R7 | **La búsqueda pasa a ser por prefijo de palabra** y sin tildes en equipos grandes. «ucia» ya no encuentra «Lucía»; correo, teléfono y país solo se buscan en equipos de 20.000 o menos. | Se avisa en la rejilla (Tarea 22). En equipos pequeños se mantiene además la búsqueda de siempre. |
| Q-R8 | **Orden limitado por encima de 20.000** (DQ7). | Aviso en la rejilla. El orden por estructura es el que el Plan P midió en 50 y 406 ms. |
| Q-R9 | **Nodos de inscripción repetidos en producción.** La migración `UnNodoPorMiembro` falla cerrada (51108) si los hay. | Script `01` con `SoloInforme` (pregunta Q2). Marca como borrados, no borra: es reversible. |
| Q-R10 | **Eliminar `HierarchyPath` no se deshace con la migración.** El `Down` vuelve a crear la columna vacía. | Volver atrás es restaurar la copia del paso 2 del despliegue. Ningún lector la necesita (Tareas 11 a 14, con la regla). |
| Q-R11 | **La base vacía puede diferir de la real.** `HelpdeskModule` reproduce `fix_helpdesk_tables.sql`, pero las bases reales pudieron recibir otros arreglos a mano. | La prueba compara la base vacía con el modelo, no con la local. Se puede comparar además con la local en solo lectura y anotar las diferencias. |
| Q-R12 | **Escrituras por ascendiente que no vienen del alta** siguen sin cota: `UplineAggregator` (puntos de facturación), `CancelMembershipHandler` y `ProcessScheduledCancellationsJob` (restas por ascendiente). | Fuera del alcance (DQ12). El mismo patrón del Bloque 4 (un delta `SponsorLine` con signo) las resolvería; queda para otro plan. |
| Q-R13 | **La fase 2 del barrido nocturno evalúa a todos los embajadores activos**, sin medir con 1.000.000. | Sin medir. Si Q1 se acepta, pasa a ser la vía de los ascendientes lejanos: medirlo antes de producción con la base de escala. |
| Q-R14 | **`SignupMembershipLevelId` debe mantenerse.** Un camino nuevo que cree el pedido de alta sin llamar a `SignupMembershipLevel` dejaría al miembro en «Otro» en Actividad. | Un único componente, llamado desde la elección de productos y la activación. `02-verificar-escala.sql` y la prueba de contraste lo vigilan. |
| Q-R15 | **Actividad con 1.000.000** puede seguir por encima de 2 s si la ventana de 3 meses abarca casi todo el equipo (el peor caso sintético). | Si no cumple: una tabla resumen diaria por patrocinador, nivel y tipo, que alimenta el alta. Se decide con la cifra. |
| Q-R16 | **Progreso de rango < 1 s con 1.000.000** puede no cumplirse: une un millón de nodos con las activas. | Si no cumple: los dos contadores de calificados pasan a la instantánea nocturna (`MemberStatisticSnapshotJob`) y el progreso los lee de ahí, con la fecha de cálculo. |

## Preguntas para Nick

- **Q1. Evaluación de rango a más de 10 niveles (DQ5).** Tras un alta, los ascendientes del 11.º nivel en adelante se evaluarían en el barrido nocturno en lugar de en 5 minutos. ¿Es aceptable, o hay algún corte (fin de mes, campaña) en el que un rango tenga que constar el mismo día para alguien lejano en la línea?
- **Q2. Producción.** ¿Se puede ejecutar allí, en solo lectura, `01-nodos-de-inscripcion-repetidos.sql` con `SoloInforme=1`, para saber si hay miembros con más de un nodo de inscripción antes de la ventana?
