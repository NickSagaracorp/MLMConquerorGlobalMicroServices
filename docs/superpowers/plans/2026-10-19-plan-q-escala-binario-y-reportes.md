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
- **Bloque 2. Cola de deltas de pierna (decisión P-R9).** Colocar inserta la hoja y una fila en `DualLegDeltas` (punto de arranque, lado, puntos y nodos). Un trabajo de Hangfire (`ApplyDualLegDeltasJob`, cada minuto, como `ApplyMemberStatisticDeltasJob`) agrega los deltas por ascendiente y los aplica de una vez: los ascendientes salen de la clave compacta con la función `dbo.DualAncestorKeys`, sin recursión. Mover o retirar encola el delta negativo en la línea vieja y el positivo en la nueva. Las comisiones y la instantánea vacían la cola antes de leer piernas, y la cola de rango antes de evaluar.
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

- **DQ1. Cola de deltas (P-R9).** Se elige la cola, como recomendaba el Plan P. Las piernas pasan a ser **consistentes en diferido**, con hasta 1 minuto más el atraso de la cola. Los lectores que pagan dinero (residual diario, bono presidencial e instantánea de estadísticas) vacían la cola antes de leer, así que siguen siendo exactos. La evaluación de rango también vacía antes (acotado a 5 tandas; Q-R1).
- **DQ2. Semántica de un delta pendiente.** Un delta `(arranque S, lado s, puntos, nodos)` se aplica a la pierna `s` de S y a cada ascendiente **que S tenga cuando se aplica**, no cuando se encoló. Los movimientos encolan lo que leen de las piernas ya aplicadas. Con esas dos reglas el resultado tras vaciar la cola es el mismo que el de aplicarlo todo en el acto (demostración en la Tarea 6). El trabajador toma `dual-tree` compartido y los movimientos lo toman exclusivo, así que nunca se cruzan. Una retirada **borra** en su transacción los pendientes cuyo arranque es el nodo retirado: si no, al volver a colocar al mismo miembro antes del vaciado, encontrarían su nodo nuevo y contarían dos veces (revisión C1, Q-R17).
- **DQ3. Raíz provisional.** Colocar a B (raíz provisional con equipo) bajo A encola **un** delta: `+(propios de B + piernas de B)` con arranque en el padre nuevo. Es el mismo valor que hoy sube `ApplySubtreeAsync(+1)`.
- **DQ4. `HierarchyPath` del binario se elimina** (no se deja nula). Al terminar el Bloque 3 no queda ningún lector, así que mantenerla nula solo ocuparía sitio en el modelo.
- **DQ5. Cota de la línea de inscripción: 10 niveles** para la cola de rango del alta, la recolocación en cola que hace el trabajo de deltas, `DownstreamTriggers` y `DOWNLINE_ENROLLED`. Los ascendientes más lejanos se evalúan en la fase 2 de `RankEvaluationSweepJob` (cada noche evalúa a todos los embajadores activos). Ver Q1.
- **DQ6. Los contadores de inscripción siguen siendo exactos.** `EnrollmentPoints`, `EnrollmentTeamSize` y `QualifiedSponsoredMembers` reciben el mismo delta que hoy en cada ascendiente. Lo que cambia es que la expansión se hace en el trabajo y no en el alta.
- **DQ7. P7 (orden de Mi equipo).** El orden por defecto es por estructura: `Node` en inscripción y `BinaryPath` en el binario. Con un equipo de más de 20.000 (`TeamGridLimits.FreeMaxRows`), el orden pedido se sustituye por el de estructura y la búsqueda se limita a nombre o id por prefijo. La rejilla lo avisa (`PagedResult.Limited`).
- **DQ8. Búsqueda por el comienzo del nombre completo**, sin distinguir tildes ni mayúsculas (en SQL Server), sobre `SearchName` («nombre apellido»), `SearchNameReversed` («apellido nombre») y el id. **No** es prefijo de cada palabra: «luci» encuentra «Lucía Díaz» y «Luciano Pérez», pero no «Ana Lucía Gómez» (ni su nombre completo ni el invertido empiezan por «luci»). En InMemory (pruebas) la caída a `StartsWith` no ignora las tildes. En equipos de 20.000 o menos se mantiene además la búsqueda actual por subcadena en todos los campos.
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
dotnet test MLMConquerorGlobalEdition.CommissionEngine.Tests   # Tareas 9 y 13 (vaciar antes de pagar)
dotnet test MLMConquerorGlobalEdition.Billing.Tests            # Tareas 13 y 17 (DownstreamTriggers)
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

- [x] **Paso 1.** Generar (unos 45 min, interpolando entre 29 min con 400.000 y 2 h 40 con 1.000.000):

```powershell
git rev-parse HEAD   # debe ser be932cc o un commit solo de documentos
powershell -File Scripts/sql/plan-p/escala/01-generar-altas.ps1 -Nodos 500000 -Salida C:\temp\escala-500k-antes.txt -Recrear
```

- [x] **Paso 2.** Medir con las tres suites de escala, en serie:

```powershell
$env:MLM_ESCALA_SQLSERVER = "Server=.;Database=MLMConquerorGlobalEditionDb_Escala;Integrated Security=true;TrustServerCertificate=True;Encrypt=True"
dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --settings MLMConquerorGlobalEdition.BizCenter.Tests/Medicion.runsettings --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
dotnet test MLMConquerorGlobalEdition.RankEngine.Tests --filter "FullyQualifiedName~Escala" --logger "console;verbosity=detailed"
```

**Esperado:** los mismos rojos que con 400.000 y 1.000.000, con cifras intermedias.

- [x] **Paso 3.** Copiar las líneas `[ESCALA] … -> mediana N ms` a la columna «500.000 antes» de «Resultados medidos» (Bloque 7). Borrar la base:

```powershell
sqlcmd -S . -d master -E -C -b -Q "ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE MLMConquerorGlobalEditionDb_Escala;"
```

- [x] **Paso 4.** Commit (solo el plan): `Plan Q: linea base del binario y los reportes con 500.000 altas antes de los cambios`.

---

## Bloque 1: progreso de rango en SQL

`GetRankProgressHandler.cs:127-137` carga en memoria los `MemberId` de todo el equipo de inscripción (450.000 o 1.050.000) y los manda como lista a un `Contains`. Con 400.000 y con 1.000.000 se agota a los 30 s. La parte binaria (`:141-150`) ya es una sola consulta.

### Tarea 1: prueba en rojo

**Files:**
- Modify: `MLMConquerorGlobalEdition.RankEngine.Tests/MLMConquerorGlobalEdition.RankEngine.Tests.csproj:33-36` (enlazar los ayudantes de SQL Server)
- Create: `MLMConquerorGlobalEdition.RankEngine.Tests/Features/SqlServerProgresoDeRangoTests.cs`

- [x] **Paso 1.** Enlazar los ayudantes, junto al enlace de `EscalaDb.cs` que ya existe:

```xml
  <!-- Plan Q: [SqlServerFact], la base local y la siembra de los reportes, en un solo sitio. -->
  <ItemGroup>
    <Compile Include="..\MLMConquerorGlobalEdition.BizCenter.Tests\Reportes\SqlServerReportes.cs" Link="SqlServer\SqlServerReportes.cs" />
    <Compile Include="..\MLMConquerorGlobalEdition.BizCenter.Tests\Reportes\ReportesDb.cs" Link="SqlServer\ReportesDb.cs" />
  </ItemGroup>
```

- [x] **Paso 2.** La prueba. Fija el resultado (tres suscripciones activas en el equipo: A una y C dos; B cancelada; la del propio R no cuenta). Además exige que ningún parámetro lleve el id de un miembro del equipo, que es lo que hoy hace el `Contains`: EF 10 traduce `lista.Contains(x)` por defecto con **un parámetro por elemento** (`@subtreeMembers1`, `@subtreeMembers2`, …), no con un único parámetro JSON (eso solo pasa con `EF.Parameter(lista)`, como en el Plan M). Por eso la aserción mira cada valor por separado: basta con que uno lleve el id de A o el de C.

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
        // EF 10 manda la lista como un parametro por id (o como un JSON con EF.Parameter): cualquiera de las dos formas
        // deja el id de A o el de C en algun valor. La consulta nueva solo manda el Node y el id del propio R.
        cap.Valores.Should().NotContain(v => v.Contains(a) || v.Contains(c),
            "la pertenencia al equipo se resuelve en SQL por el rango del Node, no con la lista de ids");
        await tx.RollbackAsync();
    }
}
```

- [x] **Paso 3.** `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests --filter FullyQualifiedName~SqlServerProgresoDeRango`. **Esperado:** falla la segunda aserción, porque los parámetros `@subtreeMembers1`, `@subtreeMembers2` y `@subtreeMembers3` llevan los ids de A, B y C.

> **Desviación (al implementar).** Con A hijo directo de R la prueba no podía ponerse en verde: `EnrollmentTeamPointsService.GetEnrollmentBranchPointsAsync` manda la lista de **hijos directos** (A y B) en un `Contains` (acotada por los patrocinados directos, no por el equipo; fuera de este bloque). La prueba cuelga A de B, así que el único hijo directo es B y A y C solo aparecen si se manda la lista del equipo. Comprobado: en rojo con el manejador viejo solo por la segunda aserción (el recuento ya daba 3).

### Tarea 2: una consulta y el índice de activas

**Files:**
- Modify: `MLMConquerorGlobalEdition.RankEngine/Features/GetRankProgress/GetRankProgressHandler.cs:122-138`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MembershipSubscriptionConfiguration.cs:13-19`
- Create: migración `SuscripcionesActivasPorMiembro`

- [x] **Paso 1.** Sustituir las líneas 122-138 por:

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

- [x] **Paso 2.** Índice filtrado: las dos consultas (inscripción y binario) unen el equipo con las activas por `MemberId`. Hace falta un nombre propio, porque `IX_MembershipSubscriptions_MemberId` ya existe sobre la misma columna. El filtro es **solo** el estado: `MembershipSubscription` no tiene filtro global de `IsDeleted` (no está entre los `HasQueryFilter` de `Repository/Configurations`) y ninguna de las dos consultas (`GetRankProgressHandler.cs:141-150` y la nueva) pide `IsDeleted = 0`. Con `[IsDeleted] = 0` en el filtro, SQL Server no podría usar el índice para ellas (revisión I4). Se cuenta lo mismo que hoy.

```csharp
        // Plan Q: las suscripciones activas por miembro, para unir el equipo (millones de filas) con ellas sin leer la tabla.
        // Solo el estado: las consultas no filtran IsDeleted (la entidad no tiene filtro global), y un filtro con
        // IsDeleted dejaria el indice sin uso.
        builder.HasIndex(new[] { nameof(MembershipSubscription.MemberId) }, "IX_MembershipSubscriptions_Activas")
            .HasFilter("[SubscriptionStatus] = 1");
```

- [x] **Paso 3.** Crear y aplicar la migración:

```bash
dotnet ef migrations add SuscripcionesActivasPorMiembro --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.AdminAPI
dotnet ef database update --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.AdminAPI --connection "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True;"
```

**Esperado:** el `Up` generado solo tiene `CreateIndex` con el filtro.

> **Desviación (al implementar).** Con solo el índice filtrado, EF daba por cubierta la FK `MemberId` y el `Up` borraba `IX_MembershipSubscriptions_MemberId`. Se declara también `builder.HasIndex(s => s.MemberId);` en la configuración y el `Up` queda con el único `CreateIndex`. `dotnet ef migrations add` no admite `--connection` (no se conecta); el `database update` sí lo lleva.

- [x] **Paso 4.** `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests` en verde, incluidas las de `GetRankProgressHandlerTests` (InMemory).
- [x] **Paso 5.** Commit: `Plan Q: el progreso de rango cuenta los calificados del equipo en una sola consulta con indice de suscripciones activas`.

### Tarea 3: medición rápida local y objetivo de escala a 1 s

**Files:**
- Modify: `MLMConquerorGlobalEdition.RankEngine.Tests/Features/SqlServerProgresoDeRangoTests.cs`
- Modify: `MLMConquerorGlobalEdition.RankEngine.Tests/Escala/EscalaProgresoDeRangoTests.cs:36`

- [x] **Paso 1.** Medición rápida contra la base local. AMB-700829 tiene 119.403 nodos en su equipo binario y es la raíz mayor de inscripción. Va con el rasgo `Medicion` para que no corra en la suite de siempre:

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

- [x] **Paso 2.** En `EscalaProgresoDeRangoTests.cs:36`, el objetivo pasa a 1 s (el que se fija para 1.000.000): `t.Should().BeLessThan(TimeSpan.FromSeconds(1));`. Se mide en el Bloque 7.
- [x] **Paso 3.** `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests --filter "FullyQualifiedName~SqlServerProgresoDeRango"` en verde. Commit: `Plan Q: medicion local del progreso de rango y objetivo de un segundo a escala`.

> **Medido (2026-10-19, base local, AMB-700829 con 119.404 nodos de inscripción):** seis ejecuciones de la prueba: 1.474 ms (la primera tras compilar, en rojo), 717, 731, 767, 826 y 653 ms. La consulta de calificados de inscripción sola tarda 58 a 116 ms y usa `IX_MembershipSubscriptions_Activas`; el resto del tiempo es del resto del manejador.
>
> **Desviación (al implementar).** La prueba escribe el tiempo con `ITestOutputHelper` (`[MEDICION] … ms`), para tener la cifra además de la aserción. El comentario de `EscalaProgresoDeRangoTests` pasa también a «menos de 1 s».

---

## Bloque 2: cola de deltas de pierna (P-R9)

**Hoy:** `LegPointMaintenance.ApplyIncrementalAsync` (`LegPointMaintenance.cs:28-51`) sube por todos los ascendientes con una CTE recursiva y actualiza cada fila dentro de la transacción de la colocación (`PlacementService.cs:163`, `BizCenter PlaceMemberHandler.cs:177`, `AdminPlaceMemberHandler.cs:133`, `SignupAPI PlaceMemberHandler.cs:147`). Mover y retirar llaman a `ApplySubtreeAsync(±1)` (`DualTeamNodes.cs:137`, `:145`, `:175`).

**Después:**
- Las siete llamadas (cuatro colocaciones de hoja y tres de `DualTeamNodes`) **encolan** una fila en `DualLegDeltas`: O(1), sin tocar la línea.
- `ApplyDualLegDeltasJob` vacía la cola cada minuto.
- `DrainAsync` también lo llaman el residual diario, el bono presidencial y la instantánea, antes de leer.

**Por qué los ascendientes salen de la clave y no de la CTE.** Con N arranques distintos en la misma línea de 8.500 niveles, la CTE hace N × 8.500 búsquedas por índice. `dbo.DualAncestorKeys` calcula las claves de los ascendientes con aritmética de bytes, sin leer la tabla. Después se agrupan, y solo se buscan las claves distintas, unos 8.500 *seeks*, una vez por tanda.

### Tarea 4: `DualBinaryPath.Ancestor` y `SideBelow` (C#)

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/Services/DualBinaryPath.cs:100-103` (después de `Rebase`)
- Modify: `MLMConquerorGlobalEdition.Domain.Tests/DualBinaryPathTests.cs` (el único fichero de pruebas de `DualBinaryPath`)

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
        // Revision I8: cada tanda agrupa por arranque, reclama por arranque (DELETE ... WHERE StartMemberId IN) y una
        // retirada borra por arranque. Sin indice, con atraso, cada una recorre la cola entera.
        builder.HasIndex(x => new { x.StartMemberId, x.Id }).HasDatabaseName("IX_DualLegDeltas_StartMemberId_Id");
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
- Una retirada encola `−T` en el padre, **borra los pendientes con arranque en el nodo retirado** y borra el nodo. La línea vieja queda en `−T`, como en V (que habría sumado v y restado T + v). Borrarlos es obligatorio, no basta con que «no encuentren fila» al vaciar: si el mismo miembro se vuelve a colocar antes del vaciado (el admin lo retira y lo recoloca, con sus hijos flotantes), su nodo nuevo tiene el mismo `MemberId` y el pendiente subiría por la línea nueva, además del `+T'` con el que vuelve su equipo (revisión C1; prueba `Retirar_y_recolocar_antes_de_vaciar_no_cuenta_dos_veces`).
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
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));   // ya vacia (Paso 5)
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
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));   // ya vacia (Paso 5)
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

    /// <summary>
    /// Revision C1: F se coloca bajo E (pendiente con arranque en E), el admin retira a E con F flotando y E se vuelve a
    /// colocar (F vuelve con el) ANTES de vaciar. El pendiente de F no puede subir por el nodo nuevo de E: F ya vuelve con
    /// el +T de su subarbol. Tras vaciar, F cuenta una sola vez.
    /// </summary>
    [SqlServerFact]
    public async Task Retirar_y_recolocar_antes_de_vaciar_no_cuenta_dos_veces()
    {
        var x = BinarioDb.Prefijo("PQE");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}B", $"{x}P", TreeSide.Left));   // ya vacia
                foreach (var (id, pat, pts) in new[] { ("E", "B", 5), ("F", "E", 7) })
                {
                    await BinarioDb.SembrarPerfilAsync(db, $"{x}{id}", $"{x}{pat}");
                    await BinarioDb.PuntosAsync(db, $"{x}{id}", pts);
                }
            }
            await using (var db = SqlServerReportes.Db())
            {
                var motor = new PlacementService(db, new Reloj());
                (await motor.PlaceAsync($"{x}E", $"{x}B")).IsSuccess.Should().BeTrue();
                (await motor.PlaceAsync($"{x}F", $"{x}E")).IsSuccess.Should().BeTrue();   // pendiente con arranque en E
            }
            await using (var db = SqlServerReportes.Db())
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                await DualTeamGate.EnterTreeExclusiveAsync(db);
                (await DualTeamNodes.RemoveAsync(db, $"{x}E", DualTeamChildrenOnRemoval.Floating, DateTime.Now, "plan-q-prueba")).Should().BeTrue();
                await tx.CommitAsync();
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}E"))
                    .Should().Be(0, "la retirada descarta los pendientes con arranque en el nodo retirado");
            }
            await using (var db = SqlServerReportes.Db())
            {
                var r = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}E", $"{x}B");   // vuelve con F
                r.IsSuccess.Should().BeTrue(r.Error);
            }
            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                await PiernasCoherentesAsync(db, x);
                var p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");
                (p.LeftLegCount, p.LeftLegPoints).Should().Be((3, 12m), "B (0), E (5) y F (7), cada uno una vez");
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

- [ ] **Paso 2.** Ejecutar. **Esperado:** no compila: no existen `LegPointMaintenance.DrainAsync` ni `EnqueueLeafAsync` (`DualLegDeltas` sí existe desde la Tarea 5).

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
    /// <summary>Arranques distintos por tanda, como mucho.</summary>
    public const int BatchSize = 200;

    /// <summary>
    /// Revision I1: filas de ascendientes calculadas por tanda, como mucho (la suma de profundidad + 1 de sus arranques).
    /// Con la linea de 8.500 niveles caben unos 47 arranques por tanda en vez de 200 (1,7 millones de filas, que no
    /// cabian en el tiempo de un comando). Un arranque mas hondo que el tope va solo en su tanda.
    /// </summary>
    public const int MaxAncestorRowsPerBatch = 400_000;

    /// <summary>
    /// Revision I9: filas por UPDATE. Por debajo de 5.000, el umbral a partir del cual SQL Server escala los candados de
    /// fila de una sentencia a la tabla entera (y bloquearia colocaciones y el MERGE del trabajo de estadisticas).
    /// </summary>
    public const int UpdateChunk = 4_000;

    /// <summary>Revision I1: segundos de cada comando de una tanda, ademas de la espera del candado (el de EF es 30 s).</summary>
    public const int BatchCommandSeconds = 300;

    /// <summary>Revision C2: reintentos de una tanda si SQL Server la elige victima de un interbloqueo (1205).</summary>
    public const int MaxDeadlockRetries = 3;

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
    /// Aplica la cola hasta vaciar lo encolado al empezar (o hasta <paramref name="maxBatches"/> tandas) y devuelve cuantos
    /// deltas aplico. Cada tanda va en su transaccion; si quien llama ya tiene una (pruebas), trabaja dentro de ella sin
    /// confirmar y sin reintentar.
    /// </summary>
    public static async Task<int> DrainAsync(AppDbContext db, int maxBatches = int.MaxValue, CancellationToken ct = default,
        int lockTimeoutMs = DualTeamGate.DefaultLockTimeoutMs)
    {
        if (!db.Database.IsRelational()) return 0;

        // Revision I1: el tiempo de EF por comando es 30 s; una tanda espera el candado (hasta lockTimeoutMs) y despues
        // calcula. Con 30 s, un comando agotado se deshacia y la misma tanda se repetia sin fin (ORDER BY MIN(Id)).
        var antes = db.Database.GetCommandTimeout();
        db.Database.SetCommandTimeout(lockTimeoutMs / 1000 + BatchCommandSeconds);
        try
        {
            // Revision I2: solo lo encolado hasta ahora. Con altas entrando sin parar, «hasta que no quede nada» podia no
            // terminar nunca; lo que entre mientras tanto lo vacia la siguiente pasada (el trabajo corre cada minuto).
            var hasta = (await db.Database.SqlQueryRaw<long>("SELECT ISNULL(MAX(Id), 0) AS [Value] FROM DualLegDeltas")
                .ToListAsync(ct)).Single();
            if (hasta == 0) return 0;

            var total = 0;
            for (var i = 0; i < maxBatches; i++)
            {
                var n = await ApplyBatchAsync(db, hasta, lockTimeoutMs, ct);
                total += n;
                if (n == 0) break;
            }
            return total;
        }
        finally
        {
            db.Database.SetCommandTimeout(antes);
        }
    }

    /// <summary>Una tanda en su transaccion, reintentada si es victima de un interbloqueo (revision C2).</summary>
    private static async Task<int> ApplyBatchAsync(AppDbContext db, long hasta, int lockTimeoutMs, CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            var propia = db.Database.CurrentTransaction is null;
            await using var tx = propia ? await db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                await DualTeamGate.EnterLegDrainAsync(db, ct, lockTimeoutMs);
                var n = (await db.Database.SqlQueryRaw<int>(ApplyBatchSql,
                        new SqlParameter("@lote", BatchSize),
                        new SqlParameter("@maxFilas", MaxAncestorRowsPerBatch),
                        new SqlParameter("@trozo", UpdateChunk),
                        new SqlParameter("@hasta", hasta))
                    .ToListAsync(ct)).Single();
                if (tx is not null) await tx.CommitAsync(ct);
                return n;
            }
            catch (SqlException ex) when (ex.Number == 1205 && propia && intento < MaxDeadlockRetries)
            {
                // SQL Server ya deshizo la tanda; el await using cierra la transaccion y se repite entera.
            }
        }
    }

    // Una tanda:
    //  1. elige los arranques (hasta @lote, en orden de llegada, y hasta @maxFilas filas de ascendientes; el primero siempre
    //     entra) entre los deltas con Id <= @hasta;
    //  2. reclama (DELETE ... OUTPUT) todos sus deltas con Id <= @hasta;
    //  3. los agrega por arranque y lado, los expande a sus ascendientes por la clave y los agrega por ascendiente;
    //  4. aplica por trozos de @trozo filas (cada UPDATE por debajo del umbral de escalado de candados).
    // SET XACT_ABORT ON: cualquier error deshace la tanda entera (los deltas reclamados vuelven a la cola).
    private const string ApplyBatchSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
DROP TABLE IF EXISTS #arranques; DROP TABLE IF EXISTS #lote; DROP TABLE IF EXISTS #deltas; DROP TABLE IF EXISTS #x;

CREATE TABLE #arranques (StartMemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
WITH a AS (
    SELECT TOP (@lote) q.StartMemberId, MIN(q.Id) AS Primero
    FROM DualLegDeltas q
    WHERE q.Id <= @hasta
    GROUP BY q.StartMemberId
    ORDER BY MIN(q.Id)),
f AS (
    SELECT a.StartMemberId,
           SUM(ISNULL(d.Depth, 0) + 1) OVER (ORDER BY a.Primero ROWS UNBOUNDED PRECEDING) AS Acumulado,
           ROW_NUMBER() OVER (ORDER BY a.Primero) AS Rn
    FROM a
    LEFT JOIN DualTeamTree d ON d.MemberId = a.StartMemberId AND d.IsDeleted = 0)
INSERT INTO #arranques (StartMemberId)
SELECT StartMemberId FROM f WHERE Rn = 1 OR Acumulado <= @maxFilas;

CREATE TABLE #lote (StartMemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL, Side int NOT NULL,
                    Points decimal(18,4) NOT NULL, NodeCount int NOT NULL);
DELETE q
OUTPUT deleted.StartMemberId, deleted.Side, deleted.Points, deleted.NodeCount INTO #lote
FROM DualLegDeltas q
JOIN #arranques r ON r.StartMemberId = q.StartMemberId
WHERE q.Id <= @hasta;

SELECT a.AncestorKey,
       SUM(CASE WHEN a.Side = 0 THEN l.Points    ELSE 0 END) AS LP,
       SUM(CASE WHEN a.Side = 1 THEN l.Points    ELSE 0 END) AS RP,
       SUM(CASE WHEN a.Side = 0 THEN l.NodeCount ELSE 0 END) AS LC,
       SUM(CASE WHEN a.Side = 1 THEN l.NodeCount ELSE 0 END) AS RC
INTO #deltas
FROM (SELECT StartMemberId, Side, SUM(Points) AS Points, SUM(NodeCount) AS NodeCount FROM #lote GROUP BY StartMemberId, Side) l
JOIN DualTeamTree s ON s.MemberId = l.StartMemberId AND s.IsDeleted = 0 AND s.BinaryPath IS NOT NULL
CROSS APPLY dbo.DualAncestorKeys(s.BinaryPath, l.Side) a
GROUP BY a.AncestorKey
OPTION (MAXDOP 1);

SELECT d.Id AS NodeId, d.MemberId, x.LP, x.RP, x.LC, x.RC, ROW_NUMBER() OVER (ORDER BY d.Id) AS Rn
INTO #x
FROM #deltas x
JOIN DualTeamTree d ON d.BinaryPath = x.AncestorKey AND d.IsDeleted = 0
OPTION (MAXDOP 1);
CREATE CLUSTERED INDEX cx ON #x (Rn);

DECLARE @desde int = 1, @filas int = (SELECT COUNT(*) FROM #x);
WHILE @desde <= @filas
BEGIN
    UPDATE d
    SET LeftLegPoints  = d.LeftLegPoints  + x.LP,
        RightLegPoints = d.RightLegPoints + x.RP,
        LeftLegCount   = d.LeftLegCount   + x.LC,
        RightLegCount  = d.RightLegCount  + x.RC
    FROM DualTeamTree d
    JOIN #x x ON x.NodeId = d.Id
    WHERE x.Rn >= @desde AND x.Rn < @desde + @trozo;

    UPDATE s
    SET DualTeamPoints = CAST(d.LeftLegPoints + d.RightLegPoints AS int)
    FROM MemberStatistics s
    JOIN #x x ON x.MemberId = s.MemberId AND x.Rn >= @desde AND x.Rn < @desde + @trozo
    JOIN DualTeamTree d ON d.Id = x.NodeId;

    SET @desde += @trozo;
END

SELECT COUNT(*) AS [Value] FROM #lote;
DROP TABLE #x; DROP TABLE #deltas; DROP TABLE #lote; DROP TABLE #arranques;";
```

(`using Microsoft.Data.SqlClient;` ya está en el fichero.)
- Los deltas de un mismo arranque se reclaman todos en la misma tanda: una tanda nunca deja la mitad de un arranque para la siguiente.
- Lo que cuesta una tanda es Σ (profundidad + 1) de sus arranques; `@maxFilas` lo acota (revisión I1) además de `BatchSize`.
- Un arranque sin nodo vivo (retirado) entra con 1 fila y sus deltas se reclaman y se descartan, como en DQ2.
- Las mismas cuentas (`Σ` por ascendiente) que antes: lo único que cambia es que el `UPDATE` va por trozos de 4.000 filas dentro de la misma transacción, así que la tanda sigue siendo atómica (revisión I9).

- [ ] **Paso 5.** `BinarioDb.SembrarAsync` (`BinarioDb.cs:30-32`) encola cada hoja y vacía, para que las piernas queden igual que antes:

```csharp
        // Las piernas (puntos y contadores) coherentes con lo sembrado: se encola cada hoja y se vacia la cola.
        foreach (var (id, padre, _) in nodos.Where(n => n.Padre is not null))
            await Repository.Services.Teams.LegPointMaintenance.EnqueueLeafAsync(db, id, DateTime.Now, "plan-p-prueba");
        await Repository.Services.Teams.LegPointMaintenance.DrainAsync(db);
```

- [ ] **Paso 6.** Commit (las pruebas siguen en rojo hasta la Tarea 7): `Plan Q: LegPointMaintenance encola los cambios de pierna y los aplica por tandas agregadas por ascendiente`.

### Tarea 7: los siete escritores encolan

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/PlacementService.cs:162-163`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Features/Placement/PlaceMember/PlaceMemberHandler.cs:177`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Features/Placement/AdminPlaceMember/AdminPlaceMemberHandler.cs:133`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Placement/Commands/PlaceMember/PlaceMemberHandler.cs:147`
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/DualTeamNodes.cs:137`, `:145`, `:174-175`
- Modify: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/Placement/SqlServerFlotantesTests.cs:74` y `ComprobarAsync`/`LimpiarAsync`
- Modify: `MLMConquerorGlobalEdition.Signups.Tests/Features/Placement/SqlServerFlotantesTests.cs:75` y `ComprobarAsync`/`LimpiarAsync`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerMedicionPlanPTests.cs:145`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerRaizProvisionalTests.cs`, `SqlServerFlotantesTests.cs`

- [ ] **Paso 1.** Las cuatro colocaciones de hoja, cada una con su reloj y su actor (los mismos que ya pasan a `ChildAsync` en la línea de encima):

```csharp
            // PlacementService.cs:163 (el nodo se crea con _clock.Now y audit.CreatedBy en :156)
            // Plan Q (P-R9): solo se encola; ApplyDualLegDeltasJob sube los puntos y contadores por la linea.
            await LegPointMaintenance.EnqueueLeafAsync(_db, memberId, _clock.Now, audit.CreatedBy, ct);

            // BizCenter PlaceMemberHandler.cs:177 (ChildAsync con now y _currentUser.UserId en :174)
            await LegPointMaintenance.EnqueueLeafAsync(_db, command.MemberToPlaceId, now, _currentUser.UserId, ct);

            // AdminPlaceMemberHandler.cs:133 (ChildAsync con now y _currentUser.UserId en :130)
            await LegPointMaintenance.EnqueueLeafAsync(_db, command.MemberToPlaceId, now, _currentUser.UserId, ct);

            // SignupAPI PlaceMemberHandler.cs:147 (ChildAsync con now y command.MemberId en :144)
            await LegPointMaintenance.EnqueueLeafAsync(_db, command.MemberId, now, command.MemberId, ct);
```

- [ ] **Paso 2.** En `DualTeamNodes.MoveSubtreeAsync` (`:137` y `:145`) y `RemoveAsync` (`:174-175`). En la retirada, además, se borran los pendientes con arranque en el nodo retirado, en la misma transacción (que ya tiene `dual-tree` en exclusivo, así que ningún vaciado corre a la vez):

```csharp
            await LegPointMaintenance.EnqueueSubtreeAsync(db, rootId, -1, now, by, ct);   // :137
            await LegPointMaintenance.EnqueueSubtreeAsync(db, rootId, +1, now, by, ct);   // :145
```

```csharp
        // RemoveAsync, :174-175
        if (node.ParentMemberId is not null)
            await LegPointMaintenance.EnqueueSubtreeAsync(db, memberId, -1, now, by, ct);
        // Plan Q (revision C1): los pendientes con arranque en este nodo no estan en el -T que se acaba de encolar (sale
        // de las piernas ya aplicadas). En el valor verdadero se habrian sumado y restado; aqui se descartan. Si se
        // dejaran, al volver a colocar al mismo miembro antes del vaciado subirian por su nodo nuevo, y su equipo ya
        // vuelve con su propio +T: contaria dos veces.
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualLegDeltas WHERE StartMemberId = {memberId}", ct);
```

- [ ] **Paso 3.** Retirar `ApplyIncrementalAsync` y `ApplySubtreeAsync`. Hoy, además de las siete llamadas de los Pasos 1 y 2 y de `BinarioDb.SembrarAsync` (Tarea 6, Paso 5), los llaman tres pruebas, que se convierten en el Paso 4. Después, `grep -rn "ApplyIncrementalAsync\|ApplySubtreeAsync" --include=*.cs` no debe dar nada.
- [ ] **Paso 4.** Las pruebas de SQL Server que escriben piernas o las leen después de escribir.
  - Las tres que llamaban a `ApplyIncrementalAsync` encolan cada hoja y vacían:

```csharp
        // AdminAPI.Tests/Features/Placement/SqlServerFlotantesTests.cs:74 y Signups.Tests/Features/Placement/SqlServerFlotantesTests.cs:75
        foreach (var id in new[] { $"{x}X", $"{x}C", $"{x}D" })
            await LegPointMaintenance.EnqueueLeafAsync(db, id, DateTime.Now, "plan-p-prueba");
        await LegPointMaintenance.DrainAsync(db);

        // BizCenter.Tests/Rendimiento/SqlServerMedicionPlanPTests.cs:145-146, dentro de la transaccion de la prueba (que ya
        // tiene dual-tree en exclusivo: el vaciado pide el compartido al mismo dueno y no se bloquea)
        await LegPointMaintenance.EnqueueLeafAsync(db, x.MemberId, DateTime.Now, "medicion");
        await DualTeamNodes.ReattachFloatingChildrenAsync(db, x.MemberId, flotantes, DateTime.Now, "medicion");
        await LegPointMaintenance.DrainAsync(db);   // el -T de la retirada, la hoja y el +T del equipo que vuelve, antes de leer
```

  - En las dos `SqlServerFlotantesTests` de AdminAPI.Tests y Signups.Tests, `ComprobarAsync` empieza por `await LegPointMaintenance.DrainAsync(db);` (lee piernas tras la colocación del manejador), y `LimpiarAsync` borra la cola del prefijo antes que los nodos: `await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualLegDeltas WHERE StartMemberId LIKE {patron}");`.
  - En `BizCenter.Tests/Binario/SqlServerRaizProvisionalTests.cs` y `SqlServerFlotantesTests.cs`, una línea, `await LegPointMaintenance.DrainAsync(db);`, delante de cada lectura de `LeftLegPoints`, `LeftLegCount` o sus derechos (`SqlServerRaizProvisionalTests.cs:62`, `:87`, `:163`; `SqlServerFlotantesTests.cs:56`).
  - `grep -rln "LegPoints\|LegCount\|DualTeamPoints" --include=*.cs *Tests* | xargs grep -l "SqlServerFact\|EscalaFact"` da además `BizCenter.Tests/Binario/EscalaBinarioTests.cs` y `AdminAPI.Tests/Controllers/SqlServerVisualizadorBinarioTests.cs`: solo leen contadores que ya estaban (la base de escala los escribe con `04-contadores-y-colas.sql`; el visualizador elige la raíz por ellos), así que no cambian. `SqlServerClaveCompactaTests.cs` no lee piernas y tampoco cambia.
  - Las de InMemory no cambian: allí se aplica en el acto.
- [ ] **Paso 5.** `dotnet test` de BizCenter.Tests (`Binario`), AdminAPI.Tests y Signups.Tests, en serie. **Esperado:** todo en verde, incluidas las tres de `SqlServerColaDePiernasTests`.
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
- Modify: `MLMConquerorGlobalEdition.RankEngine/Jobs/ProcessRankQueueJob.cs:49-50` (Q-R1: la evaluación de rango lee piernas)
- Modify: `MLMConquerorGlobalEdition.CommissionEngine.Tests/MLMConquerorGlobalEdition.CommissionEngine.Tests.csproj` (enlazar `SqlServerReportes.cs`)
- Create: `MLMConquerorGlobalEdition.CommissionEngine.Tests/Features/SqlServerVaciarAntesDeLeerTests.cs`
- Create: `MLMConquerorGlobalEdition.RankEngine.Tests/Jobs/SqlServerColaDeRangoVaciaPiernasTests.cs`

La prueba del residual va en `CommissionEngine.Tests`, que ya referencia `CommissionEngine` (`BizCenter.Tests` no). No hace falta `BinarioDb`: un delta cuyo arranque no tiene nodo vivo se reclama y se descarta al vaciar (Tarea 6), así que basta con mirar si sigue en la cola.

- [ ] **Paso 1.** Enlazar el ayudante de SQL Server en `CommissionEngine.Tests` (el de `RankEngine.Tests` ya lo enlaza la Tarea 1):

```xml
  <!-- Plan Q: [SqlServerFact] y la base local, en un solo sitio. -->
  <ItemGroup>
    <Compile Include="..\MLMConquerorGlobalEdition.BizCenter.Tests\Reportes\SqlServerReportes.cs" Link="SqlServer\SqlServerReportes.cs" />
  </ItemGroup>
```

- [ ] **Paso 2.** Prueba en rojo del residual. Todo va en **una transacción que se deshace** (revisión I7): `DrainAsync` trabaja dentro de la transacción de quien llama sin confirmar. Para que el manejador no calcule ni escriba el residual de nadie, la prueba desactiva, dentro de esa transacción, los tipos de comisión: el manejador vacía la cola (primera línea) y devuelve `NO_RESIDUAL_TYPES` sin escribir nada. Se usa además una fecha de periodo pasada (2000-01-01), que nunca choca con `ALREADY_CALCULATED`.

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateDailyResidual;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

/// <summary>Plan Q (DQ1): lo que se paga se calcula con la cola de piernas vacia.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerVaciarAntesDeLeerTests
{
    /// <summary>El reloj del motor de comisiones (CommissionEngine.Services.IDateTimeProvider).</summary>
    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>El usuario del motor de comisiones: el manejador lo pide en el constructor y no lo usa antes de vaciar.</summary>
    private sealed class SinUsuario : ICurrentUserService
    {
        public string UserId => "plan-q-prueba";
        public string MemberId => string.Empty;
        public bool IsAdmin => true;
    }

    [SqlServerFact]
    public async Task El_residual_diario_vacia_la_cola_antes_de_calcular()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var arranque = SqlServerReportes.NuevoId("PQV");   // sin nodo: el vaciado lo reclama y lo descarta
        db.DualLegDeltas.Add(new DualLegDelta
        {
            StartMemberId = arranque, Side = TreeSide.Left, Points = 9, NodeCount = 1,
            CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba"
        });
        await db.SaveChangesAsync();
        // Dentro de la transaccion, que se deshace: sin tipos activos el manejador termina justo despues de vaciar.
        await db.Database.ExecuteSqlRawAsync("UPDATE CommissionTypes SET IsActive = 0 WHERE IsActive = 1");

        var r = await new CalculateDailyResidualHandler(db, new Reloj(), new SinUsuario())
            .Handle(new CalculateDailyResidualCommand(new DateTime(2000, 1, 1)), CancellationToken.None);

        r.ErrorCode.Should().Be("NO_RESIDUAL_TYPES");
        (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == arranque))
            .Should().Be(0, "el manejador vacia la cola de piernas antes de leer DualTeamPoints");
        await tx.RollbackAsync();
    }
}
```

- [ ] **Paso 3.** Prueba en rojo de la cola de rango (Q-R1). `RankQualificationService.cs:81-82` lee `LeftLegPoints` y `RightLegPoints`: `ProcessRankQueueJob` vacía antes de evaluar. La evaluación se sustituye por un `IMediator` que falla (el trabajo lo captura y suma un reintento, dentro de la transacción que se deshace):

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Jobs;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Jobs;

/// <summary>Plan Q (Q-R1): la evaluacion de rango lee las piernas del binario; la cola de rango las vacia antes.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerColaDeRangoVaciaPiernasTests
{
    [SqlServerFact]
    public async Task La_cola_de_rango_vacia_las_piernas_antes_de_evaluar()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var x = SqlServerReportes.NuevoId("PQK");
        db.DualLegDeltas.Add(new DualLegDelta
        {
            StartMemberId = x, Side = TreeSide.Left, Points = 9, NodeCount = 1,
            CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba"
        });
        db.RankEvaluationQueue.Add(new RankEvaluationQueue
        {
            TriggerMemberId = x, EvaluateMemberId = x, TriggerEvent = RankEvaluationTrigger.Enrollment,
            TriggerDate = new DateTime(2000, 1, 1), CreatedBy = "plan-q-prueba", CreationDate = DateTime.Now
        });
        await db.SaveChangesAsync();
        var mediador = new Mock<IMediator>();
        mediador.Setup(m => m.Send(It.IsAny<EvaluateRankCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("plan-q-prueba: sin evaluar"));

        await new ProcessRankQueueJob(mediador.Object, db, NullLogger<ProcessRankQueueJob>.Instance).ExecuteAsync();

        (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == x)).Should().Be(0);
        await tx.RollbackAsync();
    }
}
```

- [ ] **Paso 4.** `dotnet test MLMConquerorGlobalEdition.CommissionEngine.Tests --filter FullyQualifiedName~SqlServerVaciarAntesDeLeer` y `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests --filter FullyQualifiedName~SqlServerColaDeRangoVaciaPiernas`. **Esperado:** las dos fallan en la última aserción (el delta sigue en la cola).

- [ ] **Paso 5.** En el residual, el bono presidencial y la instantánea, como primera línea del método (los tres tienen `_db` y `ct`):

```csharp
        // Plan Q (DQ1): las piernas son diferidas; lo que se paga se calcula con la cola vacia. Espera hasta 2 min a un movimiento.
        await LegPointMaintenance.DrainAsync(_db, ct: ct, lockTimeoutMs: 120_000);
```

  En `ProcessRankQueueJob.ExecuteAsync`, después de `if (entries.Count == 0) return;` (`:49-50`). Aquí no se espera a un movimiento: si el árbol está ocupado se evalúa con lo aplicado, y el vaciado de cada minuto y la siguiente pasada lo ponen al día.

```csharp
        // Plan Q (Q-R1): la evaluacion lee las piernas del binario (RankQualificationService). Se vacia antes, acotado a
        // 5 tandas para no retrasar la cola de rango.
        try
        {
            await LegPointMaintenance.DrainAsync(_db, maxBatches: 5, ct: ct);
        }
        catch (DualTeamBusyException ex)
        {
            _logger.LogWarning(ex, "{Job}: piernas sin vaciar, el arbol binario esta ocupado; se evalua con lo aplicado.", JobSource);
        }
```

  (`using MLMConquerorGlobalEdition.Repository.Queries;` y `using MLMConquerorGlobalEdition.Repository.Services.Teams;` en los ficheros que no los tengan.)
- [ ] **Paso 6.** En verde, más `dotnet test MLMConquerorGlobalEdition.CommissionEngine.Tests` y `dotnet test MLMConquerorGlobalEdition.RankEngine.Tests` enteros, en serie. Commit: `Plan Q: el residual diario, el bono presidencial, la instantanea y la cola de rango vacian la cola de piernas antes de leer`.

### Tarea 10: medición rápida local del bloque

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerMedicionPlanPTests.cs` (las mediciones de colocación a más de 8.000 niveles de la base local)
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerMedicionPlanQTests.cs`

- [ ] **Paso 1.** Las mediciones locales del Plan P («colocar una hoja a 8.223 niveles», «10 a la vez») pasan a medir la colocación sin la subida y, aparte, el vaciado. Nuevos objetivos:
  - colocar una hoja a más de 8.000 niveles, **< 300 ms** (antes 1,6 s);
  - 10 a la vez, **p95 < 2 s**;
  - vaciar los 10 deltas, **< 5 s**.

  En el `finally` de `Diez_colocaciones_simultaneas_a_gran_profundidad`, después del `CommitAsync` de las retiradas y antes de `BinarioDb.LimpiarAsync(x)`, se vacía la cola, para que la base local quede con las piernas como estaban: `await using (var vaciar = SqlServerReportes.Db()) await LegPointMaintenance.DrainAsync(vaciar);`.
- [ ] **Paso 2.** Concurrencia del vaciado con las colocaciones (revisión I9). El vaciado corre en bucle mientras se hacen 10 colocaciones simultáneas a más de 8.000 niveles: ninguna colocación falla (ni 1205 ni candado agotado), el vaciado no falla, se mide el p95 y, al vaciar del todo, la raíz común sube exactamente 10 nodos.

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan Q: mediciones sobre la base local (8.223 niveles) de lo que cambia el plan: la cola de piernas y su vaciado,
/// y (Tarea 20) el alta acotada. Solo con Medicion.runsettings.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
[Trait(SqlServerMedicionPlanMTests.Categoria, SqlServerMedicionPlanMTests.Medicion)]
public class SqlServerMedicionPlanQTests
{
    private readonly ITestOutputHelper _salida;

    public SqlServerMedicionPlanQTests(ITestOutputHelper salida) => _salida = salida;

    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [SqlServerFact]
    public async Task Vaciar_en_bucle_durante_diez_colocaciones_a_gran_profundidad()
    {
        var x = BinarioDb.Prefijo("MDQ");
        List<string> patrocinadores;
        string cima;
        int antes;
        await using (var db = SqlServerReportes.Db())
        {
            await LegPointMaintenance.DrainAsync(db);
            patrocinadores = await db.DualTeamTree.AsNoTracking()
                .Where(d => d.Depth > 8000 && db.DualTeamTree.Count(k => k.ParentMemberId == d.MemberId) < 2)
                .OrderByDescending(d => d.Depth).ThenBy(d => d.Id).Select(d => d.MemberId).Take(10).ToListAsync();
            patrocinadores.Should().HaveCount(10);
            // La raiz de cada patrocinador (profundidad 0 de su clave): las 10 colocaciones suben por ella.
            var raices = new HashSet<string>();
            foreach (var sp in patrocinadores)
            {
                var clave = (await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == sp)).BinaryPath!;
                var claveRaiz = DualBinaryPath.Ancestor(clave, 0);
                raices.Add(await db.DualTeamTree.AsNoTracking().Where(d => d.BinaryPath == claveRaiz).Select(d => d.MemberId).SingleAsync());
            }
            raices.Should().ContainSingle("la cadena honda cuelga de una sola raiz");
            cima = raices.Single();
            var r0 = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == cima);
            antes = r0.LeftLegCount + r0.RightLegCount;
        }

        var tiempos = new ConcurrentBag<double>();
        var fallosDelVaciado = new ConcurrentBag<Exception>();
        using var parar = new CancellationTokenSource();
        try
        {
            var vaciador = Task.Run(async () =>
            {
                while (!parar.IsCancellationRequested)
                {
                    try
                    {
                        await using var db = SqlServerReportes.Db();
                        await LegPointMaintenance.DrainAsync(db);
                    }
                    catch (Exception ex) { fallosDelVaciado.Add(ex); }
                    await Task.Delay(50);
                }
            });

            var resultados = await Task.WhenAll(patrocinadores.Select((sp, i) => Task.Run(async () =>
            {
                await using var db = SqlServerReportes.Db();
                var c = Stopwatch.StartNew();
                var r = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}{i:00}", sp);
                c.Stop();
                tiempos.Add(c.Elapsed.TotalMilliseconds);
                return r;
            })));
            parar.Cancel();
            await vaciador;

            var orden = tiempos.OrderBy(t => t).ToList();
            double P(double q) => orden[(int)Math.Ceiling(q * orden.Count) - 1];
            _salida.WriteLine($"10 colocaciones con el vaciado en bucle: p50 {P(0.5):0} ms, p95 {P(0.95):0} ms, max {orden[^1]:0} ms");
            resultados.Should().OnlyContain(r => r.IsSuccess, string.Join("; ", resultados.Where(r => !r.IsSuccess).Select(r => r.Error)));
            fallosDelVaciado.Should().BeEmpty("el vaciado no puede ser victima de interbloqueo ni agotar su tiempo");
            P(0.95).Should().BeLessThan(2000);

            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                var r1 = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == cima);
                (r1.LeftLegCount + r1.RightLegCount).Should().Be(antes + 10, "cada colocacion sube una vez por la linea comun");
            }
        }
        finally
        {
            parar.Cancel();
            await using (var db = SqlServerReportes.Db())
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                await DualTeamGate.EnterTreeExclusiveAsync(db);
                foreach (var id in await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(x)).Select(d => d.MemberId).ToListAsync())
                    await DualTeamNodes.RemoveAsync(db, id, DualTeamChildrenOnRemoval.Reject, DateTime.Now, "medicion");
                await tx.CommitAsync();
            }
            await using (var db = SqlServerReportes.Db()) await LegPointMaintenance.DrainAsync(db);
            await BinarioDb.LimpiarAsync(x);
        }
    }
}
```

- [ ] **Paso 3.** Ejecutar con la categoría de medición y anotar las cifras en «Resultados medidos». Commit: `Plan Q: medicion local de la colocacion sin subida, del vaciado de la cola y del vaciado durante colocaciones simultaneas`.

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
- **Scripts que nombran el binario junto a la ruta de inscripción** (no la del binario; la regla de la Tarea 14 no debe marcarlos): `Scripts/backfill-placements.ps1:104` y `Scripts/backfill-left.ps1:89` leen `g.HierarchyPath` de `GenealogyTree g` en una sentencia con `NOT EXISTS (SELECT 1 FROM dbo.DualTeamTree d …)`.
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

- [ ] **Paso 1.** Prueba en rojo. En un árbol P → L → LR → LRL, `GetDeepestNodeAsync(P, Left)` devuelve LRL con `Depth = 3` y el camino `[L, LR, LRL]`. `SearchBinarySubtreeAsync(P, "LRL")` devuelve el mismo camino y la pierna `Left`. Antes de consultar se vacía la ruta de esos nodos (`UPDATE DualTeamTree SET HierarchyPath = N'' WHERE MemberId LIKE @prefijo`) dentro de la transacción. Hoy falla: con la ruta vacía, el camino sale vacío. `DualTeamService` se construye como en las pruebas vecinas (`SqlServerDualSubtreeTests.cs:56`).

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>Plan Q, Bloque 3: el camino bajo la raiz (buscador y mas profundo del visualizador) sale de la clave.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerCaminoPorClaveTests
{
    [SqlServerFact]
    public async Task El_camino_bajo_la_raiz_no_depende_de_la_ruta_de_texto()
    {
        var x = BinarioDb.Prefijo("PQK");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left),
            ($"{x}LR", $"{x}L", TreeSide.Right), ($"{x}LRL", $"{x}LR", TreeSide.Left));
        await BinarioDb.SembrarPerfilesAsync(db, $"{x}P", $"{x}L", $"{x}LR", $"{x}LRL");
        // Se quita en la Tarea 13, con la columna.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DualTeamTree SET HierarchyPath = N'' WHERE MemberId LIKE {x + "%"}");
        var svc = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

        var hondo = await svc.GetDeepestNodeAsync($"{x}P", TreeSide.Left);
        hondo!.MemberId.Should().Be($"{x}LRL");
        hondo.Depth.Should().Be(3);
        hondo.Path.Select(p => p.MemberId).Should().Equal($"{x}L", $"{x}LR", $"{x}LRL");

        var hallado = (await svc.SearchBinarySubtreeAsync($"{x}P", $"{x}LRL")).Single();
        hallado.Leg.Should().Be("Left");
        hallado.Depth.Should().Be(3);
        hallado.Path.Select(p => p.MemberId).Should().Equal($"{x}L", $"{x}LR", $"{x}LRL");
        await tx.RollbackAsync();
    }
}
```

- [ ] **Paso 2.** `DualAncestors`. Una sola consulta para todas las coincidencias (no una por coincidencia, que con 100 coincidencias en la línea de 8.500 niveles eran 100 idas y vueltas): se calculan las claves de los ascendientes de cada nodo, se buscan **una vez** las claves distintas (comparten casi toda la línea) y se reparte. La pierna de cada coincidencia es el lado del primer paso del camino (el hijo de la raíz por el que se baja): sirve igual en SQL Server y en InMemory, donde los nodos pueden no tener clave. Así no hace falta un `LegOf` propio: `DualSubtree.LegOf` (`DualSubtree.cs:48`) sigue siendo el único que compara claves.

```csharp
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>Un paso del camino bajo una raiz: el miembro y el lado por el que cuelga de su padre.</summary>
public sealed record DualPathStep(string MemberId, TreeSide Side);

/// <summary>
/// Plan Q: el camino de cada nodo por debajo de <c>root</c> (sin root, con el nodo), de arriba abajo, por la clave
/// compacta (dbo.DualAncestorKeys y UX_DualTeamTree_BinaryPath). Sustituye a trocear HierarchyPath. En InMemory se sube
/// por ParentMemberId. La pierna de root en la que esta un nodo es el Side del primer paso.
/// </summary>
public static class DualAncestors
{
    private sealed record Fila(string NodeId, int Depth, string MemberId, int Side);

    // Las claves de los ascendientes de cada nodo pedido por debajo de @desde, las claves distintas buscadas una vez en
    // el indice unico de la clave y el reparto. @ids es un JSON con los MemberId (un solo parametro).
    private const string PathsSql = @"
SET NOCOUNT ON;
DROP TABLE IF EXISTS #k; DROP TABLE IF EXISTS #u;
SELECT n.MemberId AS NodeId, a.Depth, a.AncestorKey
INTO #k
FROM DualTeamTree n
CROSS APPLY dbo.DualAncestorKeys(n.BinaryPath, 0) a
WHERE n.IsDeleted = 0 AND n.BinaryPath IS NOT NULL AND a.Depth >= @desde
  AND n.MemberId IN (SELECT CAST(j.[value] AS nvarchar(450)) COLLATE DATABASE_DEFAULT FROM OPENJSON(@ids) j);
SELECT u.AncestorKey, d.MemberId, d.Side
INTO #u
FROM (SELECT DISTINCT AncestorKey FROM #k) u
JOIN DualTeamTree d ON d.BinaryPath = u.AncestorKey AND d.IsDeleted = 0;
SELECT k.NodeId, k.Depth, u.MemberId, u.Side FROM #k k JOIN #u u ON u.AncestorKey = k.AncestorKey;
DROP TABLE #k; DROP TABLE #u;";

    public static async Task<Dictionary<string, List<DualPathStep>>> PathsBelowAsync(
        AppDbContext db, DualTeamEntity root, IReadOnlyCollection<DualTeamEntity> nodes, CancellationToken ct = default)
    {
        var caminos = new Dictionary<string, List<DualPathStep>>(StringComparer.Ordinal);
        if (nodes.Count == 0) return caminos;

        if (db.Database.IsSqlServer())
        {
            var ids = JsonSerializer.Serialize(nodes.Select(n => n.MemberId).Distinct().ToList());
            var filas = await db.Database.SqlQueryRaw<Fila>(PathsSql,
                    new SqlParameter("@ids", SqlDbType.NVarChar, -1) { Value = ids },
                    new SqlParameter("@desde", (root.Depth ?? 0) + 1))
                .ToListAsync(ct);
            foreach (var g in filas.GroupBy(f => f.NodeId))
                caminos[g.Key] = g.OrderBy(f => f.Depth).Select(f => new DualPathStep(f.MemberId, (TreeSide)f.Side)).ToList();
            return caminos;
        }

        foreach (var node in nodes)
        {
            var camino = new List<DualPathStep>();
            var actual = node;
            while (actual is not null && actual.MemberId != root.MemberId)
            {
                camino.Add(new DualPathStep(actual.MemberId, actual.Side));
                var padre = actual.ParentMemberId;
                actual = padre is null ? null
                    : await db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == padre, ct);
            }
            camino.Reverse();
            caminos[node.MemberId] = camino;
        }
        return caminos;
    }
}
```

- [ ] **Paso 3.** En `DualTeamService`, los dos métodos y un ayudante de nombres. Los nombres van con un solo parámetro (`EF.Parameter`, `OPENJSON`): un camino de 8.500 niveles con un parámetro por id pasaría del límite de 2.100. Se borran `TreePrefix.Of(rootNode.HierarchyPath)`, `SegmentCount` y la consulta `legByGateway`.

```csharp
    /// <inheritdoc />
    public async Task<List<DualTreeSearchMatchView>> SearchBinarySubtreeAsync(
        string rootMemberId, string? term, int take = 25, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return new();
        take = Math.Clamp(take, 1, 100);

        var rootNode = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == rootMemberId, ct);
        if (rootNode is null) return new();

        // Plan P: el subarbol por el rango de la clave; los mas cercanos primero. (La busqueda pasa a prefijo en la Tarea 23.)
        var s = term.Trim().ToLower();
        var matches = await (
            from d in DualSubtree.Under(_db, rootNode).AsNoTracking()
            join m in _db.MemberProfiles.AsNoTracking() on d.MemberId equals m.MemberId
            where m.MemberId.ToLower().Contains(s) || (m.FirstName + " " + m.LastName).ToLower().Contains(s)
            orderby d.Depth, d.MemberId
            select new { Node = d, m.FirstName, m.LastName }
        ).Take(take).ToListAsync(ct);
        if (matches.Count == 0) return new();

        // Plan Q: los caminos bajo la raiz salen de la clave, todos en una consulta (DualAncestors).
        var caminos = await DualAncestors.PathsBelowAsync(_db, rootNode, matches.Select(mt => mt.Node).ToList(), ct);
        var names = await PathNamesAsync(caminos.Values.SelectMany(c => c).Select(p => p.MemberId).Distinct().ToList(), ct);

        return matches.Select(mt =>
        {
            var camino = caminos.GetValueOrDefault(mt.Node.MemberId) ?? new List<DualPathStep>();
            return new DualTreeSearchMatchView
            {
                MemberId = mt.Node.MemberId,
                FullName = $"{mt.FirstName} {mt.LastName}".Trim(),
                Leg      = camino.Count > 0 ? camino[0].Side.ToString() : string.Empty,
                Depth    = camino.Count,
                Path     = camino.Select(p => new DualTreePathNodeView
                {
                    MemberId = p.MemberId,
                    FullName = names.GetValueOrDefault(p.MemberId, p.MemberId)
                }).ToList()
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<DualTreeNavTargetView?> GetDeepestNodeAsync(
        string rootMemberId, TreeSide side, CancellationToken ct = default)
    {
        var rootNode = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == rootMemberId, ct);
        if (rootNode is null) return null;

        // Plan P: el mas profundo de la pierna, por el rango de la clave y Depth.
        var deepest = await DualSubtree.Leg(_db, rootNode, side).AsNoTracking()
            .OrderByDescending(d => d.Depth).ThenBy(d => d.CreationDate).ThenBy(d => d.Id)
            .FirstOrDefaultAsync(ct);
        if (deepest is null) return null;

        // Plan Q: el camino sale de la clave (DualAncestors), no de trocear la ruta.
        var camino = (await DualAncestors.PathsBelowAsync(_db, rootNode, new[] { deepest }, ct))
            .GetValueOrDefault(deepest.MemberId) ?? new List<DualPathStep>();
        var names = await PathNamesAsync(camino.Select(p => p.MemberId).ToList(), ct);

        return new DualTreeNavTargetView
        {
            MemberId = deepest.MemberId,
            FullName = names.GetValueOrDefault(deepest.MemberId, deepest.MemberId),
            Depth    = camino.Count,
            Path     = camino.Select(p => new DualTreePathNodeView
            {
                MemberId = p.MemberId,
                FullName = names.GetValueOrDefault(p.MemberId, p.MemberId)
            }).ToList()
        };
    }

    /// <summary>Plan Q: los nombres de los ids de los caminos, en una consulta con un solo parametro (OPENJSON).</summary>
    private Task<Dictionary<string, string>> PathNamesAsync(List<string> ids, CancellationToken ct) =>
        _db.MemberProfiles.AsNoTracking()
            .Where(p => EF.Parameter(ids).Contains(p.MemberId))
            .Select(p => new { p.MemberId, Name = p.FirstName + " " + p.LastName })
            .ToDictionaryAsync(x => x.MemberId, x => x.Name.Trim(), ct);
```

- [ ] **Paso 4.** En verde (y `Binario` entero, que incluye las pruebas InMemory del visualizador). Commit: `Plan Q: la busqueda y el mas profundo del binario sacan el camino de la clave compacta`.

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
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Tree/DualTeamEntity.cs:7,12` (fuera `HierarchyPath` y fuera `ITreeNode`; `ITreeNode.cs` no cambia, porque `GenealogyEntity` la sigue implementando)
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/DualTeamConfiguration.cs:13-19`
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/DualTeamNodes.cs` (las asignaciones `string.Empty` de la Tarea 12)
- Create: migración `RetiraRutaDelBinario`
- Create: `Scripts/sql/plan-q/03-recuperar-espacio-de-la-ruta.sql`
- Modify: las pruebas que siembran o afirman la ruta del binario (lista exacta: `grep -rln "DualTeamEntity" --include=*.cs *Tests* | xargs grep -ln "HierarchyPath"`, revisando que cada uso sea del binario y no de `GenealogyEntity`)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerCaminoPorClaveTests.cs` (Tarea 11: fuera el `UPDATE … HierarchyPath`)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerClaveCompactaTests.cs` (fuera `Ni_colocar_ni_mover_escriben_la_ruta_de_texto`, de la Tarea 12)
- Delete: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerDualSubtreeTests.cs:85-95` (la prueba que demostraba el error 8152 con `StartsWith` sobre la ruta)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/EscalaBinarioTests.cs:69, 80` (el informe deja de medir los MB de ruta)

- [ ] **Paso 1.** Quitar la propiedad y su configuración. Compilar la solución: los errores de compilación son la lista de lo que queda por cambiar. En las pruebas, quitar `HierarchyPath = …` de cada `new DualTeamEntity` y cada aserción sobre la ruta del binario. Donde la aserción comprobaba la posición (`PlaceMemberHandlerTests:129, 165, 232-233`, `AdminPlaceMemberHandlerTests:91, 96, 166, 220`, `AdminRemovePlacementHandlerTests:114, 117`, `SignupAmbassadorHandlerTests:811`, `SqlServerColocacionConcurrenteTests:111-113`, `SqlServerRaizProvisionalTests:84, 161`), pasa a comprobar `ParentMemberId`, `Side` y `Depth`, y en SQL Server `BinaryPath == DualBinaryPath.Child(padre.BinaryPath, lado)`. `ReportesDb.NodoBinario` conserva su firma (el parámetro `ruta` deja de usarse) para no tocar sus llamadas.
  - Dos pruebas del propio plan dejan de tener sentido sin la columna: en `SqlServerCaminoPorClaveTests` se borra la línea del `UPDATE DualTeamTree SET HierarchyPath = N''` (el resto de la prueba sigue igual: ya no hay ruta que vaciar); y `Ni_colocar_ni_mover_escriben_la_ruta_de_texto` se borra entera (su `SELECT … HierarchyPath <> N''` fallaría por columna inexistente; lo que vigilaba lo vigilan ahora el compilador y la regla de la Tarea 14).
- [ ] **Paso 2.** `dotnet ef migrations add RetiraRutaDelBinario …`. **Esperado:** `Up` = `DropColumn("HierarchyPath", "DualTeamTree")`. Al principio del `Up`, una guarda que falla cerrada si algún objeto de la base (vista, función, procedimiento o desencadenador) aún lee esa columna. No basta con buscar los dos nombres en el texto: un módulo que use `GenealogyTree.HierarchyPath` y nombre también `DualTeamTree` daría un falso positivo (revisión, menor de la Tarea 13). Los candidatos se filtran por texto y cada uno se confirma con `sys.dm_sql_referenced_entities`, que resuelve la columna referida. Si un módulo candidato no se puede analizar (referencia algo que no existe), la función falla y la migración se detiene: falla cerrada, y se revisa ese módulo a mano.

```csharp
            migrationBuilder.Sql(@"
DECLARE @modulo nvarchar(400);
DECLARE candidatos CURSOR LOCAL FAST_FORWARD FOR
    SELECT QUOTENAME(OBJECT_SCHEMA_NAME(m.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(m.object_id))
    FROM sys.sql_modules m
    JOIN sys.objects o ON o.object_id = m.object_id
    WHERE o.is_ms_shipped = 0
      AND m.definition LIKE N'%DualTeamTree%' AND m.definition LIKE N'%HierarchyPath%';
OPEN candidatos;
FETCH NEXT FROM candidatos INTO @modulo;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM sys.dm_sql_referenced_entities(@modulo, N'OBJECT') r
               WHERE r.referenced_entity_name = N'DualTeamTree' AND r.referenced_minor_name = N'HierarchyPath')
    BEGIN
        DECLARE @msg nvarchar(600) = CONCAT(N'El objeto ', @modulo, N' aun lee DualTeamTree.HierarchyPath (Plan Q, Tarea 13).');
        THROW 51100, @msg, 1;
    END
    FETCH NEXT FROM candidatos INTO @modulo;
END
CLOSE candidatos;
DEALLOCATE candidatos;");
```

  El `Down` vuelve a crear la columna `nvarchar(max) NOT NULL DEFAULT N''`, vacía. Volver atrás de verdad es restaurar la copia del despliegue (Q-R10).
- [ ] **Paso 3.** `03-recuperar-espacio-de-la-ruta.sql`. `DROP COLUMN` es solo de metadatos, así que el espacio (23,5 a 65,8 GB a escala) se libera por lotes. Es idempotente: una segunda pasada no encuentra nada que liberar.
  - `DBCC CLEANTABLE` recibe la base como nombre literal, id o `0` (la actual); se usa `0`, no una variable.
  - **El registro crece**: cada lote de 20.000 filas es su transacción, pero en recuperación completa el registro no se reutiliza hasta la siguiente copia del registro. En la base de escala (registro de 36,9 GB con 500.000) y en producción hay que vigilar `log_reuse_wait_desc` y hacer copias del registro durante el script si hace falta; en local, con recuperación simple, se reutiliza solo. El script informa del tamaño del registro antes y después.

```sql
/*
Plan Q, Tarea 13. Libera el espacio de la columna DualTeamTree.HierarchyPath, ya eliminada por RetiraRutaDelBinario.
DBCC CLEANTABLE por lotes de 20.000 filas (cada lote, su transaccion). Idempotente. Ejecutar con los servicios en marcha
o parados. En recuperacion completa el registro crece hasta la siguiente copia del registro: vigilar
log_reuse_wait_desc (sys.databases) y hacer copias del registro durante la ejecucion si hace falta.
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

DECLARE @recuperacion nvarchar(60) = (SELECT recovery_model_desc FROM sys.databases WHERE database_id = DB_ID());
DECLARE @registroAntes bigint = (SELECT SUM(size) * 8 / 1024 FROM sys.database_files WHERE type_desc = N'LOG');
DECLARE @antes bigint = (SELECT SUM(used_page_count) * 8 / 1024 FROM sys.dm_db_partition_stats WHERE object_id = OBJECT_ID(N'dbo.DualTeamTree'));
DECLARE @msg nvarchar(300) = CONCAT(N'Recuperacion ', @recuperacion, N'; registro ', @registroAntes, N' MB; DualTeamTree ', @antes, N' MB');
RAISERROR(@msg, 0, 1) WITH NOWAIT;

DBCC CLEANTABLE (0, N'dbo.DualTeamTree', 20000) WITH NO_INFOMSGS;
ALTER INDEX ALL ON dbo.DualTeamTree REORGANIZE WITH (LOB_COMPACTION = ON);

DECLARE @despues bigint = (SELECT SUM(used_page_count) * 8 / 1024 FROM sys.dm_db_partition_stats WHERE object_id = OBJECT_ID(N'dbo.DualTeamTree'));
DECLARE @registroDespues bigint = (SELECT SUM(size) * 8 / 1024 FROM sys.database_files WHERE type_desc = N'LOG');
SET @msg = CONCAT(N'DualTeamTree: ', @antes, N' MB -> ', @despues, N' MB; registro: ', @registroAntes, N' MB -> ', @registroDespues, N' MB');
RAISERROR(@msg, 0, 1) WITH NOWAIT;
```

- [ ] **Paso 4.** Aplicar en local, en este orden: la migración (`dotnet ef database update … --connection …`) y después el script. Anotar los MB de antes y después. **Esperado en local:** la tabla baja en unos 2.290 MB, que es el tamaño de las rutas medido en el Plan P.
- [ ] **Paso 5.** Todas las suites en verde, en serie (también `CommissionEngine.Tests` y `Billing.Tests`, que compilan contra `DualTeamEntity`). Commit: `Plan Q: se elimina la ruta de texto del binario con su script de recuperacion de espacio`.

### Tarea 14: los scripts y la regla

**Files:**
- Modify: `Scripts/sql/plan-p/01-reparar-huecos-binario.sql` (guarda de retirada al principio)
- Modify: `Scripts/sql/plan-p/02-recalcular-piernas.sql:38-47, 91, 98-103` (cola de piernas y nivel por `Depth`)
- Modify: `Scripts/sql/plan-p/04-contadores-y-colas.sql:38-43, 109, 117-121` (cola de piernas)
- Modify: `Scripts/sql/plan-m/04-control-binario.sql:24` (`BinaryPath` en lugar de la ruta)
- Modify: `Scripts/sql/perf-diag.ps1:28`, `Scripts/sql/leg-sponsors.ps1:9-26`, `Scripts/sql/right-sponsors.ps1:15`, `Scripts/backfill-left.ps1:41-45`, `Scripts/backfill-both-fast.ps1:45-49`, `Scripts/backfill-right-fast.ps1:45-49`, `Scripts/SignupLoadTest/Program.cs:546`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SinRutaEnElBinarioTests.cs:17-87`

- [ ] **Paso 1.** La regla, en rojo hasta terminar el paso 2. Desde ahora **cualquier** uso de la ruta del binario es un fallo, no solo un filtro. Se amplía el recorrido a `.ps1` y `.cs` de `Scripts/` y a `DeploymentScripts/` (`.sql` y `.ps1`), igual que `EscritoresDelBinarioTests`.
  - **Solo la ruta del binario** (revisión I10): una sentencia que nombra los dos árboles, como `Scripts/backfill-placements.ps1:104` y `Scripts/backfill-left.ps1:89` (`g.HierarchyPath` de `GenealogyTree g` con `NOT EXISTS (SELECT 1 FROM dbo.DualTeamTree d …)`), no es un fallo. Cuenta `HierarchyPath` con un alias ligado a `DualTeamTree` o `DualTeamEntity` (`DualTeamTree d`, `DualTeamTree AS d`, `from d in … DualTeamTree`, `DualTeamTree.Where(d => …)`), o sin alias en una sentencia que no nombra `GenealogyTree` ni `GenealogyEntity`.
  - **Excepciones**, cada una con su motivo:
    - `Migrations/`, que es historia;
    - `DeploymentScripts/migrate-rds.sql`: desactualizado y marcado así desde el Plan P (la Tarea 31 lo regenera y cambia este motivo);
    - `Scripts/sql/plan-p/01-reparar-huecos-binario.sql`: retirado por su guarda;
    - `Scripts/sql/plan-p/escala/altas/g-binario.sql` y `Scripts/sql/plan-p/escala/02-verificar-escala.sql`: los cambia la Tarea 32, que quita estas dos excepciones.
  - La excepción de `DualTeamNodes.cs` («escribe la ruta») se borra: desde la Tarea 12 no la escribe.

```csharp
    private static readonly Regex Comentario = new(@"//[^\r\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled);   // revision I2: sin Singleline (se comia el resto del fichero)
    private static readonly Regex ComentarioSql = new(@"--[^\r\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled);   // revision I2: sin Singleline
    private static readonly Regex NombraElBinario = new(@"\bDualTeam(Tree|Entity)\b", RegexOptions.Compiled);
    private static readonly Regex NombraLaGenealogia = new(@"\bGenealogy(Tree|Entity)\b", RegexOptions.Compiled);

    // Plan Q (revision I10): los alias ligados al binario en la sentencia. SQL: "DualTeamTree d", "DualTeamTree AS d",
    // "[DualTeamTree] d". LINQ: "from d in _db.DualTeamTree" y "DualTeamTree.Where(d => ...)".
    private static readonly Regex AliasDelBinario = new(
        @"\bDualTeam(?:Tree|Entity)\b\]?\s+(?:AS\s+)?(?!(?:WHERE|JOIN|ON|SET|WITH|INNER|LEFT|RIGHT|CROSS|OUTER|ORDER|GROUP|UNION|SELECT|AND|OR|VALUES)\b)(?<a>[A-Za-z_]\w*)" +
        @"|\bfrom\s+(?<a>[A-Za-z_]\w*)\s+in\s+[\w.]*\bDualTeam(?:Tree|Entity)\b" +
        @"|\bDualTeam(?:Tree|Entity)\b[^;]*?\(\s*(?<a>[A-Za-z_]\w*)\s*=>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RutaCualificada = new(@"\b(?<q>[A-Za-z_]\w*)\]?\s*\.\s*\[?HierarchyPath\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RutaSinAlias = new(@"(?<!\.\s*\[?)\bHierarchyPath\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Excepciones con nombre y motivo.</summary>
    private static readonly Dictionary<string, string> Excepciones = new()
    {
        ["DeploymentScripts/migrate-rds.sql"] = "desactualizado y marcado asi desde el Plan P; lo regenera la Tarea 31",
        ["Scripts/sql/plan-p/01-reparar-huecos-binario.sql"] = "retirado por su guarda (51104): no se ejecuta sin la columna",
        ["Scripts/sql/plan-p/escala/altas/g-binario.sql"] = "lo cambia la Tarea 32 (generador de escala)",
        ["Scripts/sql/plan-p/escala/02-verificar-escala.sql"] = "lo cambia la Tarea 32 (verificacion de escala)",
    };

    /// <summary>True si la sentencia (ya sin comentarios) usa la ruta de texto DEL BINARIO.</summary>
    internal static bool UsaLaRutaDelBinario(string sentencia)
    {
        if (!NombraElBinario.IsMatch(sentencia)) return false;
        var alias = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DualTeamTree", "DualTeamEntity" };
        foreach (Match m in AliasDelBinario.Matches(sentencia))
            if (m.Groups["a"].Success) alias.Add(m.Groups["a"].Value);
        if (RutaCualificada.Matches(sentencia).Any(m => alias.Contains(m.Groups["q"].Value))) return true;
        return RutaSinAlias.IsMatch(sentencia) && !NombraLaGenealogia.IsMatch(sentencia);
    }

    [Fact]
    public void Ninguna_sentencia_usa_la_ruta_del_binario()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var sep  = Path.DirectorySeparatorChar;
        var hallazgos = new List<string>();

        void Revisar(string archivo, Regex comentarios)
        {
            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            if (Excepciones.ContainsKey(relativo)) return;
            foreach (var sentencia in comentarios.Replace(File.ReadAllText(archivo), string.Empty).Split(';'))
                if (UsaLaRutaDelBinario(sentencia))
                    hallazgos.Add($"{relativo}: {sentencia.Trim()[..Math.Min(120, sentencia.Trim().Length)]}");
        }

        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}") || archivo.Contains($"{sep}Migrations{sep}"))
                continue;
            Revisar(archivo, Comentario);
        }

        IEnumerable<string> Ficheros(string carpeta, params string[] patrones) =>
            Directory.Exists(Path.Combine(raiz, carpeta))
                ? patrones.SelectMany(p => Directory.EnumerateFiles(Path.Combine(raiz, carpeta), p, SearchOption.AllDirectories))
                          .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
                : Enumerable.Empty<string>();

        foreach (var archivo in Directory.EnumerateFiles(raiz, "*.sql", SearchOption.TopDirectoryOnly)
                     .Concat(Ficheros("Scripts", "*.sql", "*.ps1"))
                     .Concat(Ficheros("DeploymentScripts", "*.sql", "*.ps1")))
            Revisar(archivo, ComentarioSql);
        foreach (var archivo in Ficheros("Scripts", "*.cs"))
            Revisar(archivo, Comentario);

        hallazgos.Should().BeEmpty("el binario no tiene ruta de texto: la posicion es la clave compacta (BinaryPath, Depth)");
    }

    [Theory]
    [InlineData("db.DualTeamTree.Where(d => d.HierarchyPath.StartsWith(prefijo))")]
    [InlineData("db.DualTeamTree.Where(d => EF.Functions.Like(d.HierarchyPath, x))")]
    [InlineData("SELECT * FROM DualTeamTree WHERE HierarchyPath LIKE @p")]
    [InlineData("SELECT d.MemberId, d.HierarchyPath FROM dbo.DualTeamTree d")]
    [InlineData("SELECT x.HierarchyPath FROM [DualTeamTree] AS x JOIN GenealogyTree g ON g.MemberId = x.MemberId")]
    [InlineData("from d in _db.DualTeamTree where pathPrefix.StartsWith(d.HierarchyPath) select d")]
    public void Detecta_la_ruta_del_binario(string codigo) => UsaLaRutaDelBinario(codigo).Should().BeTrue();

    /// <summary>Revision I10: la ruta de INSCRIPCION en una sentencia que tambien nombra el binario no es un fallo.</summary>
    [Theory]
    [InlineData(@"SELECT m.MemberId FROM dbo.MemberProfiles m
JOIN dbo.GenealogyTree g ON g.MemberId = m.MemberId AND g.IsDeleted = 0
WHERE (g.HierarchyPath LIKE '%/AMB-700829/%' OR m.SponsorMemberId='AMB-700829')
  AND m.MemberType = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.DualTeamTree d WHERE d.MemberId = m.MemberId)")]   // backfill-placements.ps1:104, backfill-left.ps1:89
    [InlineData("SELECT g.HierarchyPath FROM GenealogyTree g WHERE EXISTS (SELECT 1 FROM DualTeamTree WHERE MemberId = g.MemberId)")]
    public void No_confunde_la_ruta_de_inscripcion(string codigo) => UsaLaRutaDelBinario(codigo).Should().BeFalse();

    /// <summary>Revision I2: un uso de la ruta puesto despues de un comentario tambien se ve.</summary>
    [Fact]
    public void Detecta_un_uso_puesto_despues_de_un_comentario()
    {
        var sql = @"-- un comentario
SELECT 1;
SELECT * FROM DualTeamTree WHERE HierarchyPath LIKE @p";
        ComentarioSql.Replace(sql, string.Empty).Split(';').Any(UsaLaRutaDelBinario).Should().BeTrue();
    }
```

  Sustituye a `RutaEnFiltro`, `Ninguna_consulta_del_binario_filtra_por_la_ruta`, `Detecta_un_filtro_puesto_despues_de_un_comentario` y `Detecta_cada_forma_de_filtrar_por_la_ruta` (líneas 17-87); el resumen de la clase pasa a decir «ninguna sentencia usa la ruta de texto del binario».
- [ ] **Paso 2.** Los scripts:
  - `01-reparar-huecos-binario.sql`, primera instrucción tras las guardas de base:

    ```sql
    IF COL_LENGTH(N'dbo.DualTeamTree', N'HierarchyPath') IS NULL
        THROW 51104, N'Retirado por el Plan Q: el binario ya no tiene ruta de texto. La reparacion se hizo en el Plan P.', 1;
    ```

  - `02-recalcular-piernas.sql:47`: `Nivel = d.Depth` en lugar de contar barras.
  - **`02-recalcular-piernas.sql` y `04-contadores-y-colas.sql` neutralizan la cola de piernas** (revisión I3). Los dos recalculan desde cero, con `dual-tree` en exclusivo, una parte de las piernas: `02` los puntos y `04` los contadores. Un delta pendiente de antes del script ya está contenido en ese recálculo, y aplicarlo después lo contaría dos veces. Un delta lleva puntos **y** contadores, así que cada script pone a cero **solo su parte** (borrarlos enteros perdería la otra, que el script no recalcula) y borra los que quedan a cero. Va justo después de tomar `dual-tree` (`02.sql:41`, `04.sql:42`), antes de leer el árbol; con `dual-tree` en exclusivo ninguna colocación encola mientras dura, y con `dual-leg-deltas` en exclusivo ningún vaciado reclama a la vez. En `02`:

    ```sql
    -- Plan Q (revision I3): este script recalcula los PUNTOS de pierna desde cero. Lo pendiente de puntos en la cola ya
    -- va dentro del recalculo: se pone a cero (los contadores pendientes se conservan: este script no los recalcula).
    DECLARE @cola int;
    EXEC @cola = sp_getapplock @Resource = N'dual-leg-deltas', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @cola < 0
        THROW 51112, N'La cola de piernas se esta vaciando (dual-leg-deltas ocupado): parar los servicios.', 1;
    IF OBJECT_ID(N'dbo.DualLegDeltas') IS NOT NULL
    BEGIN
        BEGIN TRAN;
        UPDATE DualLegDeltas SET Points = 0 WHERE Points <> 0;
        DELETE FROM DualLegDeltas WHERE Points = 0 AND NodeCount = 0;
        COMMIT;
    END
    ```

    En `04`, lo mismo con `NodeCount` (este script recalcula los contadores) y el error `51113`:

    ```sql
    -- Plan Q (revision I3): este script recalcula los CONTADORES de pierna desde cero. Lo pendiente de contadores ya va
    -- dentro del recalculo: se pone a cero (los puntos pendientes se conservan: este script no los recalcula).
    DECLARE @cola int;
    EXEC @cola = sp_getapplock @Resource = N'dual-leg-deltas', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @cola < 0
        THROW 51113, N'La cola de piernas se esta vaciando (dual-leg-deltas ocupado): parar los servicios.', 1;
    IF OBJECT_ID(N'dbo.DualLegDeltas') IS NOT NULL
    BEGIN
        BEGIN TRAN;
        UPDATE DualLegDeltas SET NodeCount = 0 WHERE NodeCount <> 0;
        DELETE FROM DualLegDeltas WHERE Points = 0 AND NodeCount = 0;
        COMMIT;
    END
    ```

    En los dos, el candado de la cola se suelta junto al de `dual-tree`: en la salida normal (`02.sql:91`, `04.sql:109`) y en el `CATCH` (`02.sql:100-102`, `04.sql:119-120`):

    ```sql
    IF APPLOCK_MODE(N'public', N'dual-leg-deltas', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-leg-deltas', @LockOwner = N'Session';
    ```

    Prueba manual en local, tras la Tarea 7: colocar una hoja con BizCenter parado (queda un delta), ejecutar `02` y `04`, arrancar BizCenter y, tras el vaciado, `02` y `04` con `Confirmo=LOCAL` otra vez no cambian ninguna pierna (`PiernasCambiadas = 0`, `ContadoresCambiados = 0`).
  - `04-control-binario.sql:24`: lista `c.BinaryPath` y `c.Depth`.
  - En los `.ps1` y en `SignupLoadTest/Program.cs`, la pertenencia `d.HierarchyPath LIKE N'%/X/%'` pasa a esta expresión, que usa la función de la Tarea 5. Es lenta, pero son herramientas de desarrollo:

    ```sql
    EXISTS (SELECT 1 FROM dbo.DualAncestorKeys(d.BinaryPath, 0) a
            WHERE a.AncestorKey = (SELECT x.BinaryPath FROM DualTeamTree x WHERE x.MemberId = N'X' AND x.IsDeleted = 0))
    ```

    La profundidad por barras pasa a `d.Depth`. `backfill-placements.ps1:104` y `backfill-left.ps1:89` no cambian: leen la ruta de inscripción.
- [ ] **Paso 3.** La regla en verde. Commit: `Plan Q: los scripts dejan la ruta del binario, los recalculos neutralizan la cola de piernas y la regla prohibe cualquier uso de la ruta del binario`.

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
- Modify: `MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupAmbassadorHandlerTests.cs` (prueba nueva)
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/DownstreamTriggersTests.cs` (hoy `DownstreamTriggers` no tiene pruebas)

- [ ] **Paso 1.** Pruebas en rojo (InMemory).
  - En `SignupAmbassadorHandlerTests`, un patrocinador a 15 niveles de la raíz: el alta deja exactamente 10 filas de `RankEvaluationQueue` con `TriggerEvent = Enrollment`, para el patrocinador y sus 9 ascendientes más cercanos, y el `Mock<IPushNotificationService>` recibe `DownlineEnrolled` una vez para cada uno de esos 10 y ninguna para los otros 5. Solo el nodo del patrocinador hace falta: la línea sale de su ruta, y el `Node` de 15 niveles es para `NextChildAsync`.

```csharp
    // ─── Plan Q (DQ5): la linea de patrocinio hasta GenealogyUpline.EnrollmentLevels ─────────────────────

    [Fact]
    public async Task Handle_ConPatrocinadorA15Niveles_EncolaYAvisaSoloALos10MasCercanos()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        var linea = Enumerable.Range(1, 15).Select(i => $"AMB-{i:D6}").ToArray();   // linea[^1] es el patrocinador
        await db.MemberProfiles.AddAsync(BuildSponsor(linea[^1], "maria-site"));
        await db.GenealogyTree.AddAsync(new GenealogyEntity
        {
            MemberId       = linea[^1],
            ParentMemberId = linea[^2],
            HierarchyPath  = "/" + string.Join("/", linea) + "/",
            Node           = HierarchyId.Parse("/" + string.Join("/", Enumerable.Repeat(1, 15)) + "/"),
            Level          = 15,
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();
        var push = BuildPush();

        var r = await BuildHandler(db, push: push).Handle(
            new SignupAmbassadorCommand(BuildRequest(sponsorSlug: "maria-site", slug: "quince")), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error);
        var esperados = linea[^10..];   // AMB-000006 .. AMB-000015
        (await db.RankEvaluationQueue.Where(q => q.TriggerEvent == RankEvaluationTrigger.Enrollment)
                .Select(q => q.EvaluateMemberId).ToListAsync())
            .Should().BeEquivalentTo(esperados);
        foreach (var id in linea)
            push.Verify(p => p.SendAsync(id, NotificationEvents.DownlineEnrolled, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), esperados.Contains(id) ? Times.Once() : Times.Never());
    }
```

  (`using MLMConquerorGlobalEdition.SharedKernel;` para `NotificationEvents`; `RankEvaluationTrigger` ya entra por `Domain.Enums`.)
  - En `Billing.Tests`, `DownstreamTriggers` con un alta activada a 16 niveles: 10 filas de `RankEvaluationQueue` (`MembershipChange`), para los 10 ascendientes más cercanos.

```csharp
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>Plan Q (DQ5): la etapa 4 del motor de alto volumen encola rango solo a la linea cercana.</summary>
public class DownstreamTriggersTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 9, 0, 0);

    [Fact]
    public async Task Encola_rango_solo_a_los_10_ascendientes_mas_cercanos()
    {
        using var db = TestDbContextFactory.Create();
        var linea = Enumerable.Range(1, 15).Select(i => $"AMB-{i:D6}").ToArray();
        db.GenealogyTree.Add(new GenealogyEntity
        {
            Id = Guid.NewGuid().ToString(), MemberId = "AMB-NUEVO", ParentMemberId = linea[^1],
            HierarchyPath = "/" + string.Join("/", linea) + "/AMB-NUEVO/", Level = 16,
            CreatedBy = "test", CreationDate = Now, LastUpdateDate = Now
        });
        db.PointDeltaEvents.Add(new PointDeltaEvent
        {
            BatchId = "LOTE-1", OrderId = "ORD-1", MemberId = "AMB-NUEVO", EventType = PointDeltaEventType.Activated,
            OccurredAt = Now, Status = PointDeltaEventStatus.Applied, CreatedBy = "test", CreationDate = Now
        });
        await db.SaveChangesAsync();
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(d => d.Now).Returns(Now);
        var disparos = new DownstreamTriggers(db, reloj.Object, Mock.Of<IPushNotificationService>(),
            Mock.Of<IBackgroundJobClient>(), Mock.Of<ILogger<DownstreamTriggers>>());

        (await disparos.DispatchAsync("LOTE-1")).IsSuccess.Should().BeTrue();

        (await db.RankEvaluationQueue.Where(q => q.TriggerEvent == RankEvaluationTrigger.MembershipChange)
                .Select(q => q.EvaluateMemberId).ToListAsync())
            .Should().BeEquivalentTo(linea[^10..]);
    }
}
```

  `IDateTimeProvider` aquí es el de `Billing.Services` (el que pide el constructor); `IPushNotificationService` va con alias porque `SharedKernel.Interfaces` también define un `IDateTimeProvider` y los dos `using` chocarían. **Esperado:** hoy salen 15 en las dos.
- [ ] **Paso 2.** En los dos manejadores, en los dos bucles de cada uno (cola de rango y avisos), la lista de ascendientes pasa a ser la línea acotada; el cuerpo de cada bucle no cambia:

```csharp
            // Plan Q (DQ5): la linea de patrocinio hasta GenealogyUpline.EnrollmentLevels, del mas cercano al mas lejano;
            // mas alla evalua el barrido nocturno (RankEvaluationSweepJob, fase 2).
            var uplineIds = GenealogyUpline.Closest(sponsorNode.HierarchyPath, null);
```

  Sustituye a `var uplineIds = sponsorNode.HierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries);` (cola de rango, `SignupAmbassadorHandler.cs:315-316`, `SignupMemberHandler.cs:198-199`) y a la misma expresión con `.ToList()` (avisos, `SignupAmbassadorHandler.cs:355-357`, `SignupMemberHandler.cs:238-240`). `using MLMConquerorGlobalEdition.Domain.Services;` en los dos ficheros.
- [ ] **Paso 3.** `DownstreamTriggers.ParseUplineIds` (`:189-198`) se borra, y la llamada de `:75` pasa a `var uplineIds = GenealogyUpline.Closest(row.HierarchyPath, row.MemberId);`.
- [ ] **Paso 4.** Las pruebas existentes de las dos suites siembran un patrocinador de un solo nivel, así que siguen dando una fila y un aviso. En verde: `dotnet test MLMConquerorGlobalEdition.Signups.Tests` y `dotnet test MLMConquerorGlobalEdition.Billing.Tests`, en serie. Commit: `Plan Q: el alta y la facturacion encolan rango y avisan a la linea de patrocinio hasta 10 niveles`.

### Tarea 18: un delta por alta (`Scope = SponsorLine`)

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberStatisticDeltaScope.cs`
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberStatisticDelta.cs` (propiedad `Scope`)
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberStatisticDeltaConfiguration.cs:24-32` (`Scope` con valor por defecto 0)
- Create: migración `DeltaPorLineaDePatrocinio` (columna y función `dbo.GenealogyAncestors`)
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Services/SignupActivationService.cs:69-106, 153-154`
- Create: `MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/ActivacionUnDeltaPorAltaTests.cs`

**Las Tareas 18 y 19 no se despliegan por separado.** Con la 18 sola, el trabajo de deltas de hoy trataría la fila `SponsorLine` como una fila del propio patrocinador: los ascendientes dejarían de recibir sus puntos de inscripción. En local se hacen en commits seguidos y no se arranca SignupAPI entre los dos; en producción van en la misma ventana (lo son todas las migraciones del plan, ver «Despliegue») (Q-R23).

- [ ] **Paso 1.** Prueba en rojo (InMemory). Activar un alta cuyo patrocinador está a 12 niveles deja **una** fila de `MemberStatisticDeltas`: `MemberId` = el patrocinador, `Scope = SponsorLine`, `EnrollmentPointsDelta` = los puntos de calificación del pedido, `EnrollmentTeamSizeDelta = 1` y `QualifiedSponsoredMembersDelta = 1`. **Esperado:** hoy no compila (no existe `Scope`); con el enum y la propiedad, salen 12 filas.

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>Plan Q (Bloque 4): la activacion de un alta encola UNA fila de estadisticas para toda la linea.</summary>
public class ActivacionUnDeltaPorAltaTests
{
    [Fact]
    public async Task La_activacion_deja_un_solo_delta_de_linea_con_el_patrocinador_a_12_niveles()
    {
        await using var db = InMemoryDbHelper.Create();
        var linea = Enumerable.Range(1, 12).Select(i => $"AMB-{i:D6}").ToArray();   // linea[^1] es el patrocinador
        db.GenealogyTree.Add(new GenealogyEntity
        {
            MemberId = linea[^1], ParentMemberId = linea[^2], HierarchyPath = "/" + string.Join("/", linea) + "/",
            Node = HierarchyId.Parse("/" + string.Join("/", Enumerable.Repeat(1, 12)) + "/"), Level = 12, CreatedBy = "seed"
        });
        var nuevo = new MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = "AMB-NUEVO", SponsorMemberId = linea[^1], FirstName = "Nuevo", LastName = "Alta",
            Email = "nuevo@prueba.test", Country = "US", MemberType = MemberType.Ambassador,
            Status = MemberAccountStatus.Pending, CreatedBy = "seed"
        };
        var pedido = new Orders { MemberId = "AMB-NUEVO", Status = OrderStatus.Pending, CreatedBy = "seed" };
        var suscripcion = new MembershipSubscription
        {
            MemberId = "AMB-NUEVO", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Pending, CreatedBy = "seed"
        };
        db.AddRange(nuevo, pedido, suscripcion);
        await db.SaveChangesAsync();
        var servicio = new SignupActivationService(db, Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
            Mock.Of<IRecurringBillingEnrollmentService>());

        await servicio.ActivateAsync(pedido, nuevo, suscripcion, totalQualificationPoints: 6,
            new DateTime(2026, 10, 19, 12, 0, 0), "nuevo@prueba.test", CancellationToken.None);
        await db.SaveChangesAsync();

        var delta = (await db.MemberStatisticDeltas.ToListAsync()).Should().ContainSingle().Subject;
        (delta.MemberId, delta.Scope, delta.EnrollmentPointsDelta, delta.EnrollmentTeamSizeDelta,
         delta.QualifiedSponsoredMembersDelta, delta.SourceMemberId, delta.IsApplied)
            .Should().Be((linea[^1], MemberStatisticDeltaScope.SponsorLine, 6, 1, 1, "AMB-NUEVO", false));
    }
}
```

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
- [ ] **Paso 5.** La prueba del Paso 1 en verde. Aplicar la migración en local. Commit (el trabajo se cambia en la Tarea 19, que va a continuación sin arrancar SignupAPI entre medias): `Plan Q: la activacion encola un solo delta de estadisticas por alta para toda la linea de patrocinio`.

### Tarea 19: el trabajo de deltas, por conjuntos

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Jobs/ApplyMemberStatisticDeltasJob.cs:62-201`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerDeltasPorLineaTests.cs`
- Modify: `MLMConquerorGlobalEdition.Signups.Tests/Jobs/ApplyMemberStatisticDeltasJobTests.cs` (InMemory: caso `SponsorLine`)
- Sin cambios, pero tiene que seguir en verde: `MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/SignupRankInputsTests.cs` (siembra nodos **sin** `Node` en `:206-218` y `:308-326` y exige que el abuelo reciba los puntos en `:371-375`; por eso la rama InMemory sube por `ParentMemberId`, no por `Node`, revisión I6)

- [ ] **Paso 1.** Prueba en rojo, `[SqlServerFact]` dentro de una transacción que se deshace. Una línea de 12 niveles (L0 a L11), cada uno con su `MemberStatistics`, y un delta `SponsorLine` sobre L11 con 6 puntos. Tras el trabajo:
  - los 12 tienen `EnrollmentPoints` +6 y `EnrollmentTeamSize` +1;
  - solo L11 tiene `QualifiedSponsoredMembers` +1;
  - hay una evaluación de rango pendiente para L11 a L2 (10) y ninguna para L1 ni L0;
  - el delta queda `IsApplied`.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Jobs;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan Q (Bloque 4): una fila SponsorLine por alta; el trabajo la expande por la linea en SQL.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerDeltasPorLineaTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [SqlServerFact]
    public async Task Un_delta_de_linea_llega_a_todos_los_ascendientes_y_encola_rango_a_10_niveles()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        // Los pendientes de la base local no son de esta prueba: se dan por aplicados dentro de la transaccion.
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        var ids = Enumerable.Range(0, 12).Select(_ => SqlServerReportes.NuevoId("DL")).ToArray();
        for (var i = 0; i < ids.Length; i++)
        {
            ReportesDb.Miembro(db, ids[i], i == 0 ? null : ids[i - 1]);
            ReportesDb.Nodo(db, ids[i], i == 0 ? null : ids[i - 1], ReportesDb.Ruta(ids[..(i + 1)]), i + 1);
            ReportesDb.Puntos(db, ids[i], 100);
        }
        var delta = new MemberStatisticDelta
        {
            MemberId = ids[11], Scope = MemberStatisticDeltaScope.SponsorLine, EnrollmentPointsDelta = 6,
            EnrollmentTeamSizeDelta = 1, QualifiedSponsoredMembersDelta = 1, SourceMemberId = "NUEVO",
            CreatedBy = "plan-q-prueba", CreationDate = DateTime.Now
        };
        db.MemberStatisticDeltas.Add(delta);
        await db.SaveChangesAsync();

        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();

        var stats = await db.MemberStatistics.AsNoTracking().Where(s => ids.Contains(s.MemberId)).ToDictionaryAsync(s => s.MemberId);
        ids.Should().OnlyContain(i => stats[i].EnrollmentPoints == 106 && stats[i].EnrollmentTeamSize == 1);
        ids.Select(i => stats[i].QualifiedSponsoredMembers).Should().Equal(Enumerable.Repeat(0, 11).Append(1));
        var encolados = await db.RankEvaluationQueue.AsNoTracking()
            .Where(q => ids.Contains(q.EvaluateMemberId) && !q.IsProcessed).Select(q => q.EvaluateMemberId).ToListAsync();
        encolados.Should().BeEquivalentTo(ids[2..]);
        (await db.MemberStatisticDeltas.AsNoTracking().SingleAsync(d => d.Id == delta.Id)).IsApplied.Should().BeTrue();
        await tx.RollbackAsync();
    }
}
```

  (`ReportesDb.Puntos` crea `MemberStatistics` con `EnrollmentPoints = 100`.)

  Y el caso InMemory, en `ApplyMemberStatisticDeltasJobTests` (los nodos sin `Node`, como siembra `SignupRankInputsTests`):

```csharp
    [Fact]
    public async Task ExecuteAsync_SponsorLine_SubePorLaLineaYEncolaRangoHasta10Niveles()
    {
        await using var db = InMemoryDbHelper.Create();
        var linea = Enumerable.Range(0, 12).Select(i => $"AMB-L{i:D2}").ToArray();
        for (var i = 0; i < linea.Length; i++)
            db.GenealogyTree.Add(new GenealogyEntity
            {
                MemberId = linea[i], ParentMemberId = i == 0 ? null : linea[i - 1],
                HierarchyPath = "/" + string.Join("/", linea[..(i + 1)]) + "/", Level = i + 1,
                CreatedBy = "test", CreationDate = FixedNow
            });
        var delta = BuildDelta(linea[11], enrollmentPoints: 6, teamSize: 1, qsm: 1);
        delta.Scope = MemberStatisticDeltaScope.SponsorLine;
        db.MemberStatisticDeltas.Add(delta);
        await db.SaveChangesAsync();

        await CreateJob(db).ExecuteAsync(CancellationToken.None);

        var stats = await db.MemberStatistics.ToDictionaryAsync(s => s.MemberId);
        linea.Should().OnlyContain(id => stats[id].EnrollmentPoints == 6 && stats[id].EnrollmentTeamSize == 1);
        linea.Select(id => stats[id].QualifiedSponsoredMembers).Should().Equal(Enumerable.Repeat(0, 11).Append(1));
        (await db.RankEvaluationQueue.Select(q => q.EvaluateMemberId).ToListAsync()).Should().BeEquivalentTo(linea[2..]);
        (await db.MemberStatisticDeltas.SingleAsync()).IsApplied.Should().BeTrue();
    }
```

  (`using MLMConquerorGlobalEdition.Domain.Entities.Tree;` en ese fichero.) **Esperado:** las dos fallan (hoy solo L11 recibe el delta).

- [ ] **Paso 2.** El trabajo. En SQL Server, cada tanda es **un** comando por conjuntos y **una** transacción (revisión C2): sin ella, el `MERGE`, la cola de rango y la marca `IsApplied` se confirmaban por separado, y una caída (o una víctima de interbloqueo) entre el `MERGE` y la marca volvía a aplicar el `MERGE` en la siguiente pasada. El interbloqueo es probable: el vaciado de piernas actualiza `MemberStatistics.DualTeamPoints` de las mismas filas en otro orden. La tanda se reintenta entera si es víctima (1205). La rama InMemory expande `SponsorLine` subiendo por `ParentMemberId` del nodo vigente de cada miembro (revisión I6).

```csharp
using Hangfire;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Jobs;

// (el resumen de la clase se conserva, con un parrafo nuevo:)
/// Plan Q (Bloque 4): una fila puede ser de un miembro (Scope = Member, las anteriores al Plan Q) o de una linea entera
/// (Scope = SponsorLine: MemberId es el patrocinador y el delta se aplica a el y a cada ascendiente de su Node, con
/// QualifiedSponsoredMembersDelta solo a el). En SQL Server cada tanda es un comando por conjuntos en una transaccion;
/// la cola de rango se llena solo hasta GenealogyUpline.EnrollmentLevels de distancia del patrocinador.
[Queue("signups")]
public class ApplyMemberStatisticDeltasJob
{
    public const int BatchSize = 5000;

    /// <summary>Revision C2: reintentos de una tanda si SQL Server la elige victima de un interbloqueo (1205).</summary>
    public const int MaxDeadlockRetries = 3;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<ApplyMemberStatisticDeltasJob> _logger;

    public ApplyMemberStatisticDeltasJob(AppDbContext db, IDateTimeProvider dateTime, ILogger<ApplyMemberStatisticDeltasJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 7200)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var startedAt    = _dateTime.Now;
        var totalApplied = 0;
        while (true)
        {
            var n = _db.Database.IsSqlServer()
                ? await ApplyBatchSqlServerAsync(ct)
                : await ApplyBatchInMemoryAsync(ct);
            if (n == 0) break;
            totalApplied += n;
            _db.ChangeTracker.Clear(); // no acumular entidades seguidas entre tandas
        }

        var elapsedMs = (int)(_dateTime.Now - startedAt).TotalMilliseconds;
        if (totalApplied > 0)
            _logger.LogInformation("ApplyMemberStatisticDeltas: applied {DeltaCount} deltas in {ElapsedMs}ms.", totalApplied, elapsedMs);
    }

    /// <summary>Una tanda en SQL Server: un comando, una transaccion (la de quien llama si ya hay una, en pruebas).</summary>
    private async Task<int> ApplyBatchSqlServerAsync(CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            var propia = _db.Database.CurrentTransaction is null;
            await using var tx = propia ? await _db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                var n = (await _db.Database.SqlQueryRaw<int>(ApplyBatchSql,
                        new SqlParameter("@lote", BatchSize),
                        new SqlParameter("@now", _dateTime.Now),
                        new SqlParameter("@enrollment", (int)RankEvaluationTrigger.Enrollment),
                        new SqlParameter("@niveles", GenealogyUpline.EnrollmentLevels))
                    .ToListAsync(ct)).Single();
                if (tx is not null) await tx.CommitAsync(ct);
                return n;
            }
            catch (SqlException ex) when (ex.Number == 1205 && propia && intento < MaxDeadlockRetries)
            {
                _logger.LogWarning(ex, "ApplyMemberStatisticDeltas: tanda victima de un interbloqueo, intento {Intento}.", intento);
            }
        }
    }

    /// <summary>
    /// Una tanda en InMemory (pruebas). SponsorLine sube por ParentMemberId del nodo vigente de cada miembro (las pruebas
    /// siembran nodos sin Node, revision I6); el patrocinador sin nodo no recibe nada, como en SQL.
    /// </summary>
    private async Task<int> ApplyBatchInMemoryAsync(CancellationToken ct)
    {
        var batch = await _db.MemberStatisticDeltas.Where(d => !d.IsApplied).OrderBy(d => d.Id).Take(BatchSize).ToListAsync(ct);
        if (batch.Count == 0) return 0;
        var now = _dateTime.Now;

        var filas = new List<(string MemberId, int Distancia, MemberStatisticDelta Delta)>();
        foreach (var d in batch)
        {
            if (d.Scope == MemberStatisticDeltaScope.Member)
            {
                filas.Add((d.MemberId, 0, d));
                continue;
            }
            var vistos = new HashSet<string>(StringComparer.Ordinal);
            var distancia = 0;
            for (var actual = d.MemberId; actual is not null && vistos.Add(actual); distancia++)
            {
                var id = actual;
                var nodo = await _db.GenealogyTree.AsNoTracking().Where(g => g.MemberId == id)
                    .OrderByDescending(g => g.CreationDate).ThenByDescending(g => g.Id)
                    .Select(g => new { g.ParentMemberId }).FirstOrDefaultAsync(ct);
                if (nodo is null) break;
                filas.Add((id, distancia, d));
                actual = nodo.ParentMemberId;
            }
        }

        var grupos = filas.GroupBy(f => f.MemberId).Select(g => new
        {
            MemberId  = g.Key,
            P         = g.Sum(f => f.Delta.EnrollmentPointsDelta),
            T         = g.Sum(f => f.Delta.EnrollmentTeamSizeDelta),
            Q         = g.Sum(f => f.Delta.Scope == MemberStatisticDeltaScope.Member || f.Distancia == 0
                                   ? f.Delta.QualifiedSponsoredMembersDelta : 0),
            CreatedBy = g.OrderBy(f => f.Delta.Id).First().Delta.CreatedBy,
            Encolar   = g.Any(f => f.Distancia < GenealogyUpline.EnrollmentLevels)
        }).ToList();

        foreach (var grp in grupos)
        {
            var existing = await _db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == grp.MemberId, ct);
            if (existing is not null)
            {
                existing.EnrollmentPoints          += grp.P;
                existing.EnrollmentTeamSize        += grp.T;
                existing.QualifiedSponsoredMembers += grp.Q;
            }
            else
            {
                await _db.MemberStatistics.AddAsync(new MemberStatisticEntity
                {
                    MemberId = grp.MemberId, EnrollmentPoints = grp.P, EnrollmentTeamSize = grp.T,
                    QualifiedSponsoredMembers = grp.Q, CreatedBy = grp.CreatedBy, CreationDate = now
                }, ct);
            }
        }

        foreach (var d in batch)
        {
            d.IsApplied = true;
            d.AppliedAt = now;
        }

        var tocados = grupos.Where(g => g.Encolar).Select(g => g.MemberId).ToList();
        var yaEncolados = (await _db.RankEvaluationQueue
                .Where(q => !q.IsProcessed && q.TriggerEvent == RankEvaluationTrigger.Enrollment && tocados.Contains(q.EvaluateMemberId))
                .Select(q => q.EvaluateMemberId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in tocados.Where(i => !yaEncolados.Contains(i)))
            await _db.RankEvaluationQueue.AddAsync(new RankEvaluationQueue
            {
                TriggerMemberId = id, EvaluateMemberId = id, TriggerEvent = RankEvaluationTrigger.Enrollment,
                TriggerDate = now, CreatedBy = "delta-apply", CreationDate = now
            }, ct);

        await _db.SaveChangesAsync(ct);
        return batch.Count;
    }

    private const string ApplyBatchSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
DROP TABLE IF EXISTS #lote; DROP TABLE IF EXISTS #exp; DROP TABLE IF EXISTS #agg;
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
}
```

  - El `MERGE`, la cola y la marca van en la misma transacción: o se aplica la tanda entera o no se aplica nada y los deltas siguen pendientes.
  - Con la base actual hay un solo `MemberStatistics` por miembro; si hubiera varios, el `MERGE` los actualizaría todos, igual que el `MERGE` de hoy.
  - La cota de la cola de rango es `GenealogyUpline.EnrollmentLevels`, la misma constante que usan las Tareas 16 y 17: cambiarla (Q1) es cambiar un número.
- [ ] **Paso 3.** En verde: la prueba nueva, `ApplyMemberStatisticDeltasJobTests` y `SignupRankInputsTests` (`dotnet test MLMConquerorGlobalEdition.Signups.Tests` entero y el filtro `SqlServerDeltasPorLinea` de BizCenter.Tests). Commit: `Plan Q: el trabajo de deltas expande la linea de patrocinio en SQL y aplica las estadisticas con un MERGE por tanda en una transaccion`.

### Tarea 20: medición rápida local del bloque

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerMedicionPlanQTests.cs` (de la Tarea 10)

- [ ] **Paso 1.** Prueba de medición contra la base local, en una transacción que se deshace. Simula 1.000 altas bajo el miembro más profundo de la inscripción local (la rama más larga): una fila `SponsorLine` por alta, como deja la activación (Tarea 18). Comprueba que:
  - quedan 1.000 filas de deltas (antes, 1.000 × profundidad);
  - el trabajo las aplica en menos de 10 s;
  - `EnrollmentTeamSize` de la raíz sube 1.000.

```csharp
    [SqlServerFact]
    public async Task Mil_altas_bajo_el_miembro_mas_profundo_se_aplican_en_menos_de_10_s()
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(300);
        var hondo = await db.GenealogyTree.AsNoTracking().Where(g => g.Node != null)
            .OrderByDescending(g => g.Level).ThenBy(g => g.Id).FirstAsync();
        var nodoRaiz = hondo.Node!.GetAncestor(hondo.Node.GetLevel() - 1);
        var raiz = await db.GenealogyTree.AsNoTracking().Where(g => g.Node == nodoRaiz).Select(g => g.MemberId).SingleAsync();
        int Equipo(List<Domain.Entities.Member.MemberStatisticEntity> s) => s.OrderBy(x => x.Id).First().EnrollmentTeamSize;
        var antes = Equipo(await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == raiz).ToListAsync());
        _salida.WriteLine($"Miembro mas profundo de inscripcion: {hondo.MemberId}, nivel {hondo.Level}; raiz {raiz}");

        await using var tx = await db.Database.BeginTransactionAsync();
        // Los pendientes de la base local no son de esta medicion: se dan por aplicados dentro de la transaccion.
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        for (var i = 0; i < 1000; i++)
            db.MemberStatisticDeltas.Add(new Domain.Entities.Member.MemberStatisticDelta
            {
                MemberId = hondo.MemberId, Scope = Domain.Entities.Member.MemberStatisticDeltaScope.SponsorLine,
                EnrollmentPointsDelta = 6, EnrollmentTeamSizeDelta = 1, QualifiedSponsoredMembersDelta = 1,
                SourceMemberId = $"MEDQ{i:0000}", CreatedBy = "medicion", CreationDate = DateTime.Now
            });
        await db.SaveChangesAsync();
        (await db.MemberStatisticDeltas.CountAsync(d => !d.IsApplied)).Should().Be(1000, "una fila por alta, no una por ascendiente");

        var t = Stopwatch.StartNew();
        await new Repository.Jobs.ApplyMemberStatisticDeltasJob(db, new Reloj(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Repository.Jobs.ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();
        t.Stop();
        var despues = Equipo(await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == raiz).ToListAsync());
        await tx.RollbackAsync();

        _salida.WriteLine($"Aplicar 1.000 altas a {hondo.Level} niveles: {t.ElapsedMilliseconds} ms");
        (despues - antes).Should().Be(1000);
        t.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }
```

- [ ] **Paso 2.** Anotar en «Resultados medidos». Commit: `Plan Q: medicion local del alta acotada y del trabajo de deltas por conjuntos`.

---

## Bloque 5: reportes con 1.000.000

### Tarea 21: un solo nodo vivo por miembro en `GenealogyTree`

**Files:**
- Create: `Scripts/sql/plan-q/01-nodos-de-inscripcion-repetidos.sql`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/GenealogyConfiguration.cs:24`
- Create: migración `UnNodoPorMiembro`
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/GenealogySubtree.cs:71-82`
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/GenealogyNodes.cs:69-71` (`IsDuplicateLiveMember`)
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs:339` y `SignupMember/SignupMemberHandler.cs:222` (el 2601 del índice nuevo se reintenta con otro id)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportesDb.cs:150-163` (`NodoObsoletoAsync`)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/NodoDeGenealogiaModeloTests.cs` (prueba de modelo)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerNodoDeGenealogiaTests.cs` (prueba del 2601)

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

- [ ] **Paso 2.** Pruebas en rojo.
  - De modelo, en `NodoDeGenealogiaModeloTests` (solo construye el modelo, como sus vecinas):

```csharp
    /// <summary>Plan Q (DQ9): un solo nodo vivo por miembro.</summary>
    [Fact]
    public void Indice_unico_de_un_nodo_vivo_por_miembro()
    {
        using var db = SqlServerSinUseHierarchyId();
        var tipo = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(GenealogyEntity))!;

        var vivo = tipo.GetIndexes().Single(i => i.GetDatabaseName() == GenealogyConfiguration.LiveMemberIndexName);
        vivo.Properties.Select(p => p.Name).Should().Equal("MemberId");
        vivo.IsUnique.Should().BeTrue();
        vivo.GetFilter().Should().Be("[IsDeleted] = 0");
    }
```

  (`using MLMConquerorGlobalEdition.Repository.Configurations;`.)
  - Contra SQL Server, en `SqlServerNodoDeGenealogiaTests`. InMemory no aplica índices únicos, así que la prueba del 2601 va aquí; la InMemory `El_nodo_vigente_desempata_por_Id` (`NodosVigentesEnLosEquiposTests.cs:48-63`) no cambia, porque `NodeOfAsync` sigue desempatando igual.

```csharp
    /// <summary>Plan Q (DQ9): un segundo nodo vivo del mismo miembro choca con UX_GenealogyTree_MemberId_Vivo (2601).</summary>
    [SqlServerFact]
    public async Task Un_segundo_nodo_vivo_del_mismo_miembro_falla_con_el_indice_unico()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var x = SqlServerReportes.NuevoId("PQN");
        ReportesDb.Nodo(db, x, null, ReportesDb.Ruta(x), 1);
        await db.SaveChangesAsync();
        ReportesDb.Nodo(db, x, null, ReportesDb.Ruta(x), 1);   // otra raiz (otro Node), el mismo miembro

        var error = await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        error.Which.InnerException.Should().BeOfType<Microsoft.Data.SqlClient.SqlException>().Which.Number.Should().Be(2601);
        GenealogyNodes.IsDuplicateLiveMember(error.Which).Should().BeTrue();
        await tx.RollbackAsync();
    }
```

- [ ] **Paso 3.** Configuración, junto a la línea 24, con el nombre en una constante (la usan la prueba de modelo y `GenealogyNodes`):

```csharp
    /// <summary>Plan Q (DQ9): un solo nodo vivo por miembro.</summary>
    public const string LiveMemberIndexName = "UX_GenealogyTree_MemberId_Vivo";
```

```csharp
        // Plan Q (DQ9): un solo nodo vivo por miembro. Con el, el nodo vigente es el unico y CurrentUnder no necesita la
        // antijoin (medida en el Plan P: el conteo del equipo de inscripcion tardaba 2,8 s con 1.000.000).
        builder.HasIndex(new[] { nameof(GenealogyEntity.MemberId) }, LiveMemberIndexName)
            .IsUnique().HasFilter("[IsDeleted] = 0");
```

  En `GenealogyNodes`, junto a `IsDuplicateNode` (`:69-71`):

```csharp
    /// <summary>
    /// Plan Q (DQ9): el guardado choco con UX_GenealogyTree_MemberId_Vivo, otro nodo vivo con el mismo MemberId. En el alta
    /// solo pasa si dos altas sacaron el mismo id libre a la vez (el nodo puede insertarse antes que el perfil y chocar
    /// aqui en vez de en AK_MemberProfiles_MemberId): se reintenta con otro id.
    /// </summary>
    public static bool IsDuplicateLiveMember(DbUpdateException ex)
        => ex.InnerException?.Message is { } m
           && m.Contains(GenealogyConfiguration.LiveMemberIndexName, StringComparison.OrdinalIgnoreCase);
```

  En los dos manejadores del alta, el filtro del reintento (`SignupAmbassadorHandler.cs:339`, `SignupMemberHandler.cs:222`) incluye el índice nuevo; sin esto, ese choque saldría como `INTERNAL_ERROR`:

```csharp
        catch (DbUpdateException ex) when ((IsDuplicateMemberIdViolation(ex) || GenealogyNodes.IsDuplicateNode(ex)
                                            || GenealogyNodes.IsDuplicateLiveMember(ex)) && idAttempt < 5)
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

- [ ] **Paso 5.** `ReportesDb.NodoObsoletoAsync` sigue creando el nodo con fecha antigua, pero lo deja borrado, que es lo que hace el script. Se marca **antes** del primer guardado: varias pruebas siembran el vigente y el obsoleto del mismo miembro en el mismo `SaveChanges`, y con los dos vivos chocarían con el índice único.

```csharp
    public static async Task<GenealogyEntity> NodoObsoletoAsync(
        AppDbContext db, string memberId, string? padre, string ruta, int nivel)
    {
        var nodo = Nodo(db, memberId, padre, ruta, nivel);
        // Plan Q (DQ9): el obsoleto queda borrado, como lo deja 01-nodos-de-inscripcion-repetidos.sql. Asi no choca con
        // UX_GenealogyTree_MemberId_Vivo aunque el vigente del mismo miembro se guarde a la vez.
        nodo.IsDeleted = true;
        nodo.DeletedBy = "plan-q-01";
        await db.SaveChangesAsync();
        nodo.CreationDate = new DateTime(2020, 1, 1);
        await db.SaveChangesAsync();
        return nodo;
    }
```

  Las pruebas que exigían «el obsoleto no cuenta» (`NodosVigentesEnLosEquiposTests`, `GenealogySubtreeTests`, `ClientesDelEquipoTests`, `EnrollmentActivityServiceTests`, `PrefijoDeSubarbolTests`, `SqlServerActividadTests`, `SqlServerClientesTests`, `TeamEventRegistrationsServiceTests`, `TeamStatusChangesServiceTests`) siguen valiendo con esa siembra y no cambian.
- [ ] **Paso 6.** En local: script con `SoloInforme=1` (**esperado:** 0) y con `SoloInforme=0`; después la migración. Suites `Reportes` y `Rendimiento` de BizCenter y `Signups.Tests` (por el reintento del alta, incluida `SqlServerAltasConcurrentesTests`) en verde. Commit: `Plan Q: un solo nodo vivo por miembro en la genealogia y CurrentUnder por el rango`.

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

- [ ] **Paso 1.** Pruebas en rojo (InMemory). Tres miembros (YO, A y B bajo YO, A antes que B por estructura) y una petición ordenada por `FullName` descendente, que daría B antes que A. Con el equipo por encima del límite el orden pedido se sustituye por el de estructura (A, B) y el resultado lo avisa; por debajo se respeta; sin orden pedido, estructura (DQ7). El tamaño sale de los contadores de pierna en el binario (como en `MiEquipoDualTotalPorContadoresTests`) y de `MemberStatistics.EnrollmentTeamSize` en inscripción.

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q (DQ7, P7 del Plan P): por encima de TeamGridLimits.FreeMaxRows, Mi equipo ordena por estructura y lo avisa.</summary>
public class LimitesDeRejillaTests
{
    private const string Yo = "AMB-000001";
    private const string A  = "AMB-000002";   // «Nombre AMB-000002»: el primero por estructura, el segundo por nombre desc
    private const string B  = "AMB-000003";

    private static GridDataRequest PorNombreDesc() =>
        new() { PageSize = 20, Sorts = { new GridSort { Field = "FullName", Direction = "desc" } } };

    private static async Task<AppDbContext> DualAsync(int tamano)
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        foreach (var id in new[] { Yo, A, B }) ReportesDb.Miembro(db, id, id == Yo ? null : Yo);
        ReportesDb.NodoBinario(db, Yo, null, ReportesDb.Ruta(Yo), TreeSide.Left);
        ReportesDb.NodoBinario(db, A, Yo, ReportesDb.Ruta(Yo, A), TreeSide.Left);
        ReportesDb.NodoBinario(db, B, Yo, ReportesDb.Ruta(Yo, B), TreeSide.Right);
        await db.SaveChangesAsync();
        var yo = await db.DualTeamTree.SingleAsync(d => d.MemberId == Yo);
        yo.LeftLegCount = tamano - 1;
        yo.RightLegCount = 1;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task<AppDbContext> InscripcionAsync(int tamano)
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        foreach (var id in new[] { Yo, A, B }) ReportesDb.Miembro(db, id, id == Yo ? null : Yo);
        ReportesDb.Nodo(db, Yo, null, ReportesDb.Ruta(Yo), 1);
        ReportesDb.Nodo(db, A, Yo, ReportesDb.Ruta(Yo, A), 2);   // Node /r/1/
        ReportesDb.Nodo(db, B, Yo, ReportesDb.Ruta(Yo, B), 2);   // Node /r/2/
        ReportesDb.Puntos(db, Yo, 0).EnrollmentTeamSize = tamano;
        await db.SaveChangesAsync();
        return db;
    }

    private static DualTeamService Dual(AppDbContext db) =>
        new(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

    private static EnrollmentTeamService Inscripcion(AppDbContext db) =>
        new(db, CalificadoEnLosEquiposTests.Rangos(db), new EnrollmentTeamPointsService(db), CalificadoEnLosEquiposTests.Calificacion(db));

    [Fact]
    public async Task Dual_con_mas_de_20000_el_orden_pedido_pasa_a_estructura_y_se_avisa()
    {
        await using var db = await DualAsync(30_000);
        var r = await Dual(db).GetMyTeamGridAsync(Yo, PorNombreDesc());
        r.Limited.Should().BeTrue();
        r.Items.Select(i => i.MemberId).Should().Equal(A, B);
    }

    [Fact]
    public async Task Dual_con_20000_o_menos_se_respeta_el_orden()
    {
        await using var db = await DualAsync(2);
        var r = await Dual(db).GetMyTeamGridAsync(Yo, PorNombreDesc());
        r.Limited.Should().BeFalse();
        r.Items.Select(i => i.MemberId).Should().Equal(B, A);
    }

    [Fact]
    public async Task Dual_sin_orden_pedido_ordena_por_estructura()
    {
        await using var db = await DualAsync(2);
        var r = await Dual(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20 });
        r.Items.Select(i => i.MemberId).Should().Equal(A, B);
    }

    [Fact]
    public async Task Inscripcion_con_mas_de_20000_el_orden_pedido_pasa_a_estructura_y_se_avisa()
    {
        await using var db = await InscripcionAsync(30_000);
        var r = await Inscripcion(db).GetMyTeamGridAsync(Yo, PorNombreDesc());
        r.Limited.Should().BeTrue();
        r.Items.Select(i => i.MemberId).Should().Equal(A, B);
    }

    [Fact]
    public async Task Inscripcion_con_20000_o_menos_se_respeta_el_orden()
    {
        await using var db = await InscripcionAsync(2);
        var r = await Inscripcion(db).GetMyTeamGridAsync(Yo, PorNombreDesc());
        r.Limited.Should().BeFalse();
        r.Items.Select(i => i.MemberId).Should().Equal(B, A);
    }

    [Fact]
    public async Task Inscripcion_sin_orden_pedido_ordena_por_estructura()
    {
        await using var db = await InscripcionAsync(2);
        var r = await Inscripcion(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20 });
        r.Items.Select(i => i.MemberId).Should().Equal(A, B);
    }
}
```

  **Esperado:** no compila (no existen `PagedResult.Limited` ni `TeamGridLimits`).
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

    /// <summary>True si el equipo supera el limite: quita los ordenes pedidos y quien llama ordena por estructura.</summary>
    public static bool Limit(GridDataRequest request, int teamSize)
    {
        if (teamSize <= FreeMaxRows) return false;
        request.Sorts = new List<GridSort>();
        return true;
    }
}
```

  `PagedResult<T>`:

```csharp
    /// <summary>
    /// Plan Q (DQ7): el equipo supero TeamGridLimits.FreeMaxRows, asi que el orden es por estructura y la busqueda por
    /// prefijo. La rejilla lo avisa.
    /// </summary>
    public bool Limited { get; set; }
```

  `DualTeamService.GetMyTeamGridAsync` (sustituye a `:97-119`) y un ayudante:

```csharp
    public async Task<PagedResult<DualTeamMyTeamMemberView>> GetMyTeamGridAsync(
        string memberId, GridDataRequest request, CancellationToken ct = default)
    {
        var myNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<DualTeamMyTeamMemberView>();

        // Plan Q (DQ7): el tamano sale de los contadores de pierna (diferidos hasta 1 minuto, Q-R1; basta para un limite).
        var tamano   = myNode.LeftLegCount + myNode.RightLegCount;
        var limitado = TeamGridLimits.Limit(request, tamano);

        // Sin orden sobre una columna de la consulta (o con el limite, que los quita), por estructura: el recorrido del
        // indice de la clave. Antes se anadia EnrollDate desc, que con un millon de filas ordenaba el rango entero.
        var ordenPedido = request.Sorts.Any(s => DualRowSearchableFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)
                                              || string.Equals(s.Field, nameof(DualMyTeamRow.EnrollDate), StringComparison.OrdinalIgnoreCase));
        var filas = MyTeamRows(myNode);
        if (!ordenPedido) filas = ByStructure(filas);

        // Plan P (Plan B): sin busqueda ni filtros el total es el equipo entero, el de los contadores de pierna.
        var paged = await filas.ToGridResultAsync(request, DualRowSearchableFields, ct: ct, unfilteredTotal: tamano);

        var items = await BuildDualViewsForPageAsync(paged.Items.ToList(), myNode, memberId, ct);
        return new PagedResult<DualTeamMyTeamMemberView>
        {
            Items = items, TotalCount = paged.TotalCount, Page = paged.Page, PageSize = paged.PageSize, Limited = limitado
        };
    }

    /// <summary>
    /// Plan Q (DQ7): el orden por estructura. En SQL Server, la clave (el orden del indice UX_DualTeamTree_BinaryPath);
    /// en InMemory, donde un byte[] no se puede ordenar, profundidad e id.
    /// </summary>
    private IQueryable<DualMyTeamRow> ByStructure(IQueryable<DualMyTeamRow> filas) =>
        _db.Database.IsSqlServer()
            ? filas.OrderBy(r => r.BinaryPath)
            : filas.OrderBy(r => r.Depth).ThenBy(r => r.MemberId);
```

  `EnrollmentTeamService`: `SanitizeGridRequest` (`:67-73`) deja de añadir `EnrollDate desc` (se borran sus dos últimas líneas), y `GetMyTeamGridAsync` y `BuildMyTeamQueryable` (`:117-164`) pasan a:

```csharp
    public async Task<PagedResult<EnrollmentMyTeamMemberView>> GetMyTeamGridAsync(
        string memberId, GridDataRequest request, CancellationToken ct = default)
    {
        var myNode = await _db.GenealogyTree.AsNoTracking()
            .CurrentOfAsync(memberId, ct); // nodo vigente (Plan M, M2)
        if (myNode?.Node is null) return new PagedResult<EnrollmentMyTeamMemberView>();

        SanitizeGridRequest(request);
        // Plan Q (DQ7): el tamano del equipo de inscripcion lo guarda MemberStatistics (diferido por la cola de deltas;
        // basta para decidir un limite).
        var tamano = await _db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == memberId)
            .OrderBy(s => s.Id).Select(s => s.EnrollmentTeamSize).FirstOrDefaultAsync(ct);
        var limitado = TeamGridLimits.Limit(request, tamano);

        var query  = BuildMyTeamQueryable(myNode.Node, myNode.Level, memberId, byStructure: request.Sorts.Count == 0);
        var result = await query.ToGridResultAsync(request, MyTeamSearchableFields, ct: ct);
        await EnrichPageAsync(result.Items, ct);
        result.Limited = limitado;
        return result;
    }

    private IQueryable<EnrollmentMyTeamMemberView> BuildMyTeamQueryable(
        HierarchyId root, int rootLevel, string memberId, bool byStructure = false)
    {
        var equipo = _db.GenealogyTree.AsNoTracking().CurrentUnder(root).Where(g => g.MemberId != memberId);
        // Plan Q (DQ7): el orden por estructura va sobre el nodo, antes del join y de la proyeccion (la vista no lleva
        // Node): la pagina sale del recorrido de IX_GenealogyTree_Node, sin ordenar el rango (la Tarea 26 lo comprueba).
        if (byStructure) equipo = equipo.OrderBy(g => g.Node);
        return
            from g in equipo
            join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            select new EnrollmentMyTeamMemberView
            {
                MemberId        = m.MemberId,
                FullName        = m.FirstName + " " + m.LastName,
                Email           = m.Email,
                Phone           = m.Phone,
                Country         = m.Country,
                Level           = g.Level - rootLevel,
                EnrollDate      = m.EnrollDate,
                SponsorMemberId = m.SponsorMemberId,
                AccountStatus   = m.Status.ToString()
            };
    }
```

  `GetMyTeamAsync` (la lectura sin rejilla, `:77-112`) sigue llamando a `BuildMyTeamQueryable(myNode.Node, myNode.Level, memberId)` con su orden por fecha: no cambia.
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

### Tarea 23: búsqueda por el comienzo del nombre (`MemberNameSearch`)

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberProfile.cs:20` (dos propiedades calculadas)
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs:36-37`
- Create: `MLMConquerorGlobalEdition.Repository/Queries/MemberNameSearch.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/DualSubtree.cs` (nuevo `SearchUnder`)
- Modify: `DualTeamService.cs:97-119, 319-337` y `EnrollmentTeamService.cs:117-164`
- Create (Tarea 25): migración `BusquedaYNivelDeAlta`, compartida con el nivel de alta
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerBusquedaPorNombreTests.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/MemberNameSearchTests.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/LimitesDeRejillaTests.cs` (Tarea 22)

- [ ] **Paso 1.** Pruebas en rojo. La búsqueda es por el **comienzo del nombre completo** («nombre apellido» o «apellido nombre») o del id, no por el comienzo de cada palabra (DQ8, revisión I11): «luci» no encuentra a «Ana Lucía Gómez», cuyo nombre completo empieza por «Ana».
  - **SQL Server** (`SqlServerBusquedaPorNombreTests`), en un binario pequeño con «Lucía Díaz», «Luciano Pérez» y «Ana Lucía Gómez», sin tildes ni mayúsculas en lo que se escribe:
    - «lucia diaz» encuentra a «Lucía Díaz»;
    - «diaz» la encuentra por el apellido (nombre invertido);
    - «luci» encuentra a «Lucía Díaz» y a «Luciano Pérez», no a «Ana Lucía Gómez»;
    - «ucia» no encuentra a nadie (es prefijo, no subcadena).
  - **Plan:** `DualSubtree.SearchUnder` sobre el miembro con el equipo binario mayor de la base local parte de `IX_MemberProfiles_SearchName` con un *Index Seek* y no recorre `MemberProfiles` ni `DualTeamTree` (`PlanDeEjecucion`, como las pruebas de plan del Plan M).
  - **InMemory** (`MemberNameSearchTests`): el mismo resultado con nombres **sin** tildes, porque la caída a `StartsWith` no las ignora.

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q (DQ8): busqueda por el comienzo del nombre completo o del id, sin tildes ni mayusculas, con indice.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerBusquedaPorNombreTests
{
    [SqlServerFact]
    public async Task Busca_por_el_comienzo_del_nombre_o_del_apellido_sin_tildes()
    {
        var x = BinarioDb.Prefijo("PQB");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}1", $"{x}P", TreeSide.Left),
            ($"{x}2", $"{x}1", TreeSide.Left), ($"{x}3", $"{x}1", TreeSide.Right));
        ReportesDb.Miembro(db, $"{x}P");
        foreach (var (id, nombre, apellido) in new[] { ("1", "Lucía", "Díaz"), ("2", "Luciano", "Pérez"), ("3", "Ana Lucía", "Gómez") })
        {
            var perfil = ReportesDb.Miembro(db, $"{x}{id}", $"{x}P");
            perfil.FirstName = nombre;
            perfil.LastName = apellido;
        }
        await db.SaveChangesAsync();
        var raiz = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");

        async Task<string[]> Buscar(string termino) =>
            (await DualSubtree.SearchUnder(db, raiz, termino).Select(d => d.MemberId).ToListAsync()).Order().ToArray();

        (await Buscar("lucia diaz")).Should().Equal($"{x}1");
        (await Buscar("diaz")).Should().Equal($"{x}1");
        (await Buscar("luci")).Should().Equal($"{x}1", $"{x}2");
        (await Buscar("ucia")).Should().BeEmpty();
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task La_busqueda_parte_del_indice_de_nombre()
    {
        await using var db = SqlServerReportes.Db();
        var mayor = await db.DualTeamTree.AsNoTracking()
            .OrderByDescending(d => d.LeftLegCount + d.RightLegCount).ThenBy(d => d.MemberId).FirstAsync();

        var plan = await PlanDeEjecucion.CompiladoAsync(db, DualSubtree.SearchUnder(db, mayor, "lucia").Select(d => d.MemberId));

        plan.Should().Contain("IX_MemberProfiles_SearchName").And.Contain(PlanDeEjecucion.Seek);
        PlanDeEjecucion.Recorridos(plan, "MemberProfiles").Should().BeEmpty();
        PlanDeEjecucion.Recorridos(plan, "DualTeamTree").Should().BeEmpty();
    }
}
```

```csharp
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q (DQ8): la caida InMemory de MemberNameSearch (StartsWith, sin ignorar tildes: nombres sin tildes).</summary>
public class MemberNameSearchTests
{
    [Fact]
    public async Task En_memoria_busca_por_el_comienzo_del_nombre_completo()
    {
        await using var db = ReportesDb.Nueva();
        foreach (var (id, nombre, apellido) in new[] { ("AMB-1", "Lucia", "Diaz"), ("AMB-2", "Luciano", "Perez"), ("AMB-3", "Ana Lucia", "Gomez") })
        {
            var perfil = ReportesDb.Miembro(db, id);
            perfil.FirstName = nombre;
            perfil.LastName = apellido;
        }
        await db.SaveChangesAsync();

        string[] Buscar(string termino) =>
            MemberNameSearch.Apply(db, db.MemberProfiles, termino).Select(m => m.MemberId).AsEnumerable().Order().ToArray();

        Buscar("lucia diaz").Should().Equal("AMB-1");
        Buscar("DIAZ").Should().Equal("AMB-1");
        Buscar("luci").Should().Equal("AMB-1", "AMB-2");
        Buscar("ucia").Should().BeEmpty();
        Buscar("amb-3").Should().Equal("AMB-3");
    }
}
```
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
/// Plan Q (DQ8): busqueda de miembros por el comienzo del nombre completo («nombre apellido» o «apellido nombre») o del
/// id, sin tildes ni mayusculas, con indice. No es prefijo de cada palabra: «luci» no encuentra «Ana Lucia Gomez». Full-Text
/// no esta instalado (FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 0). En InMemory, StartsWith sin ignorar tildes.
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

  `Key`, `Param` y `RecompileTag` ya existen en `DualSubtree` (`:21`, `:57-58`, `:60-62`), igual que los `using` de `System.Data` y `Microsoft.Data.SqlClient`.
- [ ] **Paso 4.** Uso.
  - **`DualTeamService.GetMyTeamGridAsync`** (la versión de la Tarea 22). Con `request.Search` y el equipo por encima del límite, la base es `SearchUnder`; el término sale de la petición (la rejilla no lo vuelve a aplicar por subcadena) y el total se cuenta, porque ya no es el equipo entero. Con el equipo dentro del límite, la búsqueda de siempre por subcadena en todos los campos. Las líneas entre `TeamGridLimits.Limit` y `ToGridResultAsync` pasan a:

```csharp
        var ordenPedido = request.Sorts.Any(s => DualRowSearchableFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)
                                              || string.Equals(s.Field, nameof(DualMyTeamRow.EnrollDate), StringComparison.OrdinalIgnoreCase));
        IQueryable<DualMyTeamRow> filas;
        int? totalSinFiltros = tamano;
        if (limitado && !string.IsNullOrWhiteSpace(request.Search))
        {
            // Plan Q (DQ8): por encima del limite, por el comienzo del nombre o del id, con indice (SearchUnder).
            filas = Project(DualSubtree.SearchUnder(_db, myNode, request.Search).AsNoTracking());
            request.Search  = null;
            totalSinFiltros = null;
        }
        else
        {
            filas = MyTeamRows(myNode);
        }
        if (!ordenPedido) filas = ByStructure(filas);

        var paged = await filas.ToGridResultAsync(request, DualRowSearchableFields, ct: ct, unfilteredTotal: totalSinFiltros);
```

  - **`SearchBinarySubtreeAsync`** (la versión de la Tarea 11), siempre por prefijo (es la búsqueda del visualizador): se borra `var s = term.Trim().ToLower();` y la consulta de coincidencias pasa a:

```csharp
        // Plan Q (DQ8): por el comienzo del nombre completo o del id, con indice; los mas cercanos primero.
        var matches = await (
            from d in DualSubtree.SearchUnder(_db, rootNode, term).AsNoTracking()
            join m in _db.MemberProfiles.AsNoTracking() on d.MemberId equals m.MemberId
            orderby d.Depth, d.MemberId
            select new { Node = d, m.FirstName, m.LastName }
        ).Take(take).ToListAsync(ct);
```

  - **`EnrollmentTeamService`** (la versión de la Tarea 22). `BuildMyTeamQueryable` recibe el término cuando el equipo supera el límite y une con los perfiles que casan en lugar de con todos:

```csharp
    private IQueryable<EnrollmentMyTeamMemberView> BuildMyTeamQueryable(
        HierarchyId root, int rootLevel, string memberId, bool byStructure = false, string? prefixTerm = null)
    {
        var equipo = _db.GenealogyTree.AsNoTracking().CurrentUnder(root).Where(g => g.MemberId != memberId);
        if (byStructure) equipo = equipo.OrderBy(g => g.Node);
        // Plan Q (DQ8): con el limite, solo los perfiles cuyo nombre completo o id empieza por el termino (con indice).
        var perfiles = prefixTerm is null
            ? _db.MemberProfiles.AsNoTracking()
            : MemberNameSearch.Apply(_db, _db.MemberProfiles.AsNoTracking(), prefixTerm);
        return
            from g in equipo
            join m in perfiles on g.MemberId equals m.MemberId
            select new EnrollmentMyTeamMemberView
            {
                MemberId        = m.MemberId,
                FullName        = m.FirstName + " " + m.LastName,
                Email           = m.Email,
                Phone           = m.Phone,
                Country         = m.Country,
                Level           = g.Level - rootLevel,
                EnrollDate      = m.EnrollDate,
                SponsorMemberId = m.SponsorMemberId,
                AccountStatus   = m.Status.ToString()
            };
    }
```

    Y en `GetMyTeamGridAsync`, entre `TeamGridLimits.Limit` y `BuildMyTeamQueryable`:

```csharp
        string? prefijo = null;
        if (limitado && !string.IsNullOrWhiteSpace(request.Search))
        {
            prefijo = request.Search;   // DQ8: por prefijo con indice; la rejilla no vuelve a buscar por subcadena
            request.Search = null;
        }
        var query  = BuildMyTeamQueryable(myNode.Node, myNode.Level, memberId,
                                          byStructure: request.Sorts.Count == 0, prefixTerm: prefijo);
```

  - Dos pruebas más en `LimitesDeRejillaTests` (InMemory): con el límite, «000003» (subcadena del id) ya no encuentra a nadie y «amb-000003» (comienzo del id) encuentra a B.

```csharp
    [Fact]
    public async Task Dual_con_mas_de_20000_busca_por_prefijo_y_no_por_subcadena()
    {
        await using var db = await DualAsync(30_000);
        (await Dual(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = "000003" })).Items.Should().BeEmpty();
        (await Dual(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = "amb-000003" }))
            .Items.Select(i => i.MemberId).Should().Equal(B);
    }

    [Fact]
    public async Task Inscripcion_con_mas_de_20000_busca_por_prefijo_y_no_por_subcadena()
    {
        await using var db = await InscripcionAsync(30_000);
        (await Inscripcion(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = "000003" })).Items.Should().BeEmpty();
        (await Inscripcion(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = "amb-000003" }))
            .Items.Select(i => i.MemberId).Should().Equal(B);
    }
```

- [ ] **Paso 5.** Commit tras la migración de la Tarea 25 (las columnas van en ella): `Plan Q: busqueda de miembros por el comienzo del nombre completo o del id con indice y sin tildes`.

### Tarea 24: Clientes y Eventos, por el rango

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentTeamService.cs:600-619` (`BuildCustomerQueryable`, ámbito de equipo)
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Events/TeamEventRegistrationsService.cs:56-142, 151-155, 178-184`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs` (índice filtrado de clientes)
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/CapturadorDeSql.cs` (lo usan también las Tareas 25 y 26)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SqlServerClientesTests.cs` y `SqlServerEventosTests.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/TeamEventRegistrationsServiceTests.cs` (límite de «Todo el equipo»)

- [ ] **Paso 1.** Un capturador de lo que EF manda (texto y valores de parámetros), compartido por las pruebas de SQL del bloque:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q: guarda el texto y los valores de parametros de cada comando que EF manda.</summary>
public sealed class CapturadorDeSql : DbCommandInterceptor
{
    public readonly List<string> Textos = new();
    public readonly List<string> Valores = new();

    /// <summary>Un contexto contra la base local con este capturador.</summary>
    public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(SqlServerReportes.ConnectionString).AddInterceptors(this).Options);

    public void Limpiar()
    {
        Textos.Clear();
        Valores.Clear();
    }

    private void Guardar(DbCommand c)
    {
        Textos.Add(c.CommandText);
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
```

- [ ] **Paso 2.** Pruebas en rojo.
  - **Clientes, de plan** (en `SqlServerClientesTests`). La forma de la consulta de equipo, sobre la raíz mayor local: parte de `IX_MemberProfiles_Clientes` y no recorre `GenealogyTree`. La forma se reproduce en la prueba, como en `SqlServerPlanesDeEjecucionTests`, y además se exige que el SQL que manda el servicio empiece por los perfiles (`FROM [MemberProfiles]` antes que `[GenealogyTree]`), para que la forma probada sea la del servicio.

```csharp
    [SqlServerFact]
    public async Task El_equipo_de_clientes_parte_del_indice_de_clientes()
    {
        const string raizMayor = "AMB-700829";
        await using (var db = SqlServerReportes.Db())
        {
            var raiz = await db.GenealogyTree.AsNoTracking().NodeOfAsync(raizMayor);
            var plan = await Rendimiento.PlanDeEjecucion.CompiladoAsync(db,
                from m in db.MemberProfiles.AsNoTracking()
                where m.MemberType == MemberType.ExternalMember && m.MemberId != raizMayor
                join g in db.GenealogyTree.AsNoTracking().Under(raiz) on m.MemberId equals g.MemberId
                select new { m.MemberId, m.FirstName, m.LastName, m.EnrollDate, m.SponsorMemberId, g.Level });

            plan.Should().Contain("IX_MemberProfiles_Clientes");
            Rendimiento.PlanDeEjecucion.Recorridos(plan, "GenealogyTree").Should().BeEmpty();
        }

        var cap = new CapturadorDeSql();
        await using (var db = cap.Db())
            await ClientesDelEquipoTests.Servicio(db).GetCustomersGridAsync(raizMayor, new GridDataRequest { PageSize = 20 }, CustomerScope.Team);
        var pagina = cap.Textos.Last(t => t.Contains("[GenealogyTree]") && t.Contains("[MemberProfiles]"));
        pagina.IndexOf("FROM [MemberProfiles]", StringComparison.Ordinal)
            .Should().BeLessThan(pagina.IndexOf("[GenealogyTree]", StringComparison.Ordinal), "la consulta parte de los clientes");
    }
```

  (`using MLMConquerorGlobalEdition.Repository.Queries;` para `NodeOfAsync` y `Under`.)
  - **Eventos, del SQL** (en `SqlServerEventosTests`), sobre el escenario que ya siembra el fichero: ni la rejilla de inscritos ni los totales mandan `IsDescendantOf` (que solo salía del `TOP 1` correlacionado), y dan lo mismo que hoy.

```csharp
    [SqlServerFact]
    public Task Inscritos_y_totales_unen_con_el_rango_sin_IsDescendantOf() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        var cap = new CapturadorDeSql();
        await using var db = cap.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = Nuevos();
        await TeamEventRegistrationsServiceTests.EscenarioAsync(db, id, sembrarRango: false);
        var servicio = TeamEventRegistrationsServiceTests.Servicio(db);
        cap.Limpiar();

        var inscritos = (await servicio.GetGridAsync(id.Yo, id.Evento, true, new GridDataRequest { PageSize = 50 })).Value!.Items.ToList();
        var totales   = (await servicio.GetTotalsAsync(id.Yo, id.Evento)).Value!;

        inscritos.Select(f => f.MemberId).Should().BeEquivalentTo(new[] { id.A, id.Yo });
        (totales.Registered, totales.Tickets).Should().Be((2, 6));
        cap.Textos.Should().NotContain(t => t.Contains("IsDescendantOf"));
        await tx.RollbackAsync();
    });
```

  - **Eventos, límite** (en `TeamEventRegistrationsServiceTests`, InMemory, con su `EscenarioAsync`): con `MemberStatistics.EnrollmentTeamSize` del que mira en 30.000, «Todo el equipo» sin orden pedido sale con `Limited = true`; con 2, `Limited = false` y el orden de siempre (`LastName asc`).

```csharp
    [Theory]
    [InlineData(30_000, true)]
    [InlineData(2, false)]
    public async Task Todo_el_equipo_por_encima_del_limite_ordena_por_estructura_y_lo_avisa(int tamano, bool limitado)
    {
        await using var db = ReportesDb.Nueva();
        var id = Ids.Fijos;
        await EscenarioAsync(db, id);
        ReportesDb.Puntos(db, id.Yo, 0).EnrollmentTeamSize = tamano;
        await db.SaveChangesAsync();

        var r = (await Servicio(db).GetGridAsync(id.Yo, id.Evento, false, new GridDataRequest { PageSize = 50 })).Value!;

        r.Limited.Should().Be(limitado);
        r.Items.Should().HaveCount(3);
    }
```

  **Esperado:** las tres primeras fallan (la consulta de clientes empieza por `GenealogyTree`, el índice no existe y el SQL de eventos lleva `IsDescendantOf`); la de límite no compila hasta la Tarea 22 y después falla con 30.000.
- [ ] **Paso 3.** Clientes. Índice filtrado:

```csharp
        // Plan Q: los clientes (MemberType = ExternalMember = 1) son pocos; el reporte de equipo parte de ellos. El filtro
        // global de MemberProfile anade IsDeleted = 0 a todas sus consultas, asi que el indice filtrado sirve.
        builder.HasIndex(new[] { nameof(MemberProfile.MemberId) }, "IX_MemberProfiles_Clientes")
            .HasFilter("[MemberType] = 1 AND [IsDeleted] = 0")
            .IncludeProperties(m => new { m.EnrollDate, m.SponsorMemberId });
```

  En `BuildCustomerQueryable`, el ámbito de equipo (`:600-619`) pasa a partir de los clientes:

```csharp
        // Plan Q: se parte de los clientes (IX_MemberProfiles_Clientes, pocos) y se comprueba que su nodo cae en el rango
        // del que mira. Con un nodo vivo por miembro (Tarea 21), Under es el equipo vigente: sin antijoin. Antes se partia
        // del rango del equipo entero (un millon de filas) para quedarse con los clientes.
        return
            from m in _db.MemberProfiles.AsNoTracking()
            where m.MemberType == MemberType.ExternalMember && m.MemberId != memberId
            join g in _db.GenealogyTree.AsNoTracking().Under(root!) on m.MemberId equals g.MemberId
            select new EnrollmentCustomerView
            {
                MemberId        = m.MemberId,
                FullName        = m.FirstName + " " + m.LastName,
                Email           = m.SponsorMemberId == memberId ? m.Email : null,
                Phone           = m.SponsorMemberId == memberId ? m.Phone : null,
                Country         = m.Country,
                Level           = g.Level - rootLevel,
                IsPersonal      = m.SponsorMemberId == memberId,
                EnrollDate      = m.EnrollDate,
                SponsorMemberId = m.SponsorMemberId,
                AccountStatus   = m.Status.ToString()
            };
```

- [ ] **Paso 4.** Eventos. «Solo inscritos» (`:77-96`) y `GetTotalsAsync` (`:116-142`) sustituyen el `let node = … TOP 1 … IsDescendantOf(root)` por una unión con el rango; «Todo el equipo» ordena por estructura con el límite:

```csharp
        // (GetGridAsync, tras RootAsync)
        // Plan Q (DQ7): «Todo el equipo» con mas de TeamGridLimits.FreeMaxRows ordena por estructura (Node) y lo avisa. El
        // tamano lo guarda MemberStatistics.EnrollmentTeamSize del que mira.
        var limitado = !registeredOnly && TeamGridLimits.Limit(request, await TeamSizeAsync(memberId, ct));
        Sanitize(request, defaultSort: !limitado);

        var regs = _db.CorporateEventRegistrations.AsNoTracking().Where(r => r.EventId == eventId);

        // «Solo inscritos» parte de las inscripciones del EVENTO (pocas) y solo sobre esas comprueba la pertenencia al
        // equipo, ahora uniendo con el rango (un nodo vivo por miembro, Tarea 21), sin el TOP 1 correlacionado.
        var query = registeredOnly
            ? from g in regs.GroupBy(r => r.MemberId)
                  .Select(g => new { MemberId = g.Key, First = g.Min(r => r.RegisteredAt), Tickets = g.Sum(r => r.Tickets) })
              join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
              join n in _db.GenealogyTree.AsNoTracking().Under(root) on m.MemberId equals n.MemberId
              select new TeamEventRegistrationView
              {
                  MemberId     = m.MemberId,
                  FirstName    = m.FirstName,
                  LastName     = m.LastName,
                  EnrollDate   = m.EnrollDate,
                  Registered   = true,
                  RegisteredAt = g.First,
                  Tickets      = g.Tickets
              }
            : from m in Team(root, byStructure: limitado)
              select new TeamEventRegistrationView
              {
                  MemberId     = m.MemberId,
                  FirstName    = m.FirstName,
                  LastName     = m.LastName,
                  EnrollDate   = m.EnrollDate,
                  Registered   = regs.Any(r => r.MemberId == m.MemberId),
                  RegisteredAt = regs.Where(r => r.MemberId == m.MemberId).Min(r => (DateTime?)r.RegisteredAt),
                  Tickets      = regs.Where(r => r.MemberId == m.MemberId).Sum(r => (int?)r.Tickets) ?? 0
              };

        var page = await query.ToGridResultAsync(request, SearchableFields, ct: ct);
        var rows = page.Items as List<TeamEventRegistrationView> ?? page.Items.ToList();
        await EnrichAsync(rows, ct);
        page.Items   = rows;
        page.Limited = limitado;
        return Result<PagedResult<TeamEventRegistrationView>>.Success(page);
```

```csharp
        // (GetTotalsAsync, tras RootAsync)
        // Primero las inscripciones del evento y despues, solo para ellas, la pertenencia al equipo por el rango.
        var inTeam =
            from r in _db.CorporateEventRegistrations.AsNoTracking()
            where r.EventId == eventId
            join n in _db.GenealogyTree.AsNoTracking().Under(root) on r.MemberId equals n.MemberId
            select r;
```

  Y los ayudantes (`Team`, `:151-155`, y `Sanitize`, `:178-184`), más el tamaño:

```csharp
    /// <summary>Los perfiles del equipo, el incluido; con <paramref name="byStructure"/>, en el orden del arbol (Node).</summary>
    private IQueryable<Domain.Entities.Member.MemberProfile> Team(HierarchyId root, bool byStructure = false)
    {
        var equipo = _db.GenealogyTree.AsNoTracking().Under(root);
        if (byStructure) equipo = equipo.OrderBy(g => g.Node);   // antes del join, como en la Tarea 22
        return from g in equipo
               join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
               select m;
    }

    /// <summary>Plan Q (DQ7): el tamano del equipo de inscripcion del que mira (diferido; basta para un limite).</summary>
    private Task<int> TeamSizeAsync(string memberId, CancellationToken ct) =>
        _db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == memberId)
            .OrderBy(s => s.Id).Select(s => s.EnrollmentTeamSize).FirstOrDefaultAsync(ct);

    private static void Sanitize(GridDataRequest request, bool defaultSort = true)
    {
        request.Sorts   = request.Sorts.Where(s => DbFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        request.Filters = request.Filters.Where(f => DbFields.Contains(f.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        if (defaultSort && request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(TeamEventRegistrationView.LastName), Direction = "asc" });
    }
```

- [ ] **Paso 5.** En verde (tras la migración de la Tarea 25, que crea el índice). Commit (con la Tarea 25): `Plan Q: Clientes parte de los clientes y Eventos une con el rango del equipo en lugar del TOP 1 con IsDescendantOf`.

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
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/EnrollmentActivityServiceTests.cs:61` (siembra) y `SqlServerActividadTests.cs` (pruebas nuevas)
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/ReportesDb.cs` (`NivelesDeAltaAsync`)
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Reportes/SignupMembershipLevelTests.cs`
- Sin cambios: `MLMConquerorGlobalEdition.Repository/Services/Teams/EnrollmentActivityDtos.cs` (`EnrollmentActivityRowView` ya tiene todos los `set` públicos)

- [ ] **Paso 1.** Pruebas en rojo.
  - **Regla N2** (`SignupMembershipLevelTests`, InMemory, nuevo en `BizCenter.Tests/Reportes`):

```csharp
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q (DQ10): SignupMembershipLevel aplica la regla N2 del Plan J.</summary>
public class SignupMembershipLevelTests
{
    [Fact]
    public async Task El_nivel_de_alta_es_el_mas_alto_del_primer_pedido_de_membresia()
    {
        await using var db = ReportesDb.Nueva();
        var m = ReportesDb.Miembro(db, "AMB-000010");
        ReportesDb.Orden(db, "AMB-000010", new DateTime(2026, 2, 1), (int?)null);   // sin productos de membresia: no cuenta
        ReportesDb.Orden(db, "AMB-000010", new DateTime(2026, 3, 1), 2, 1);         // el primero: VIP y Lifestyle
        ReportesDb.Orden(db, "AMB-000010", new DateTime(2026, 4, 1), 4);            // despues, Turbo: no lo mueve
        await db.SaveChangesAsync();

        await SignupMembershipLevel.RefreshAsync(db, m);

        m.SignupMembershipLevelId.Should().Be(2);
    }

    [Fact]
    public async Task Sin_pedidos_de_membresia_no_hay_nivel()
    {
        await using var db = ReportesDb.Nueva();
        var m = ReportesDb.Miembro(db, "AMB-000011");
        ReportesDb.Orden(db, "AMB-000011", new DateTime(2026, 3, 1), (int?)null);
        await db.SaveChangesAsync();

        await SignupMembershipLevel.RefreshAsync(db, m);

        m.SignupMembershipLevelId.Should().BeNull();
    }
}
```

  - **Actividad**, en `SqlServerActividadTests`, con el capturador de la Tarea 24 (`CapturadorDeSql`): el SQL que manda no lleva `IsDescendantOf` y ningún parámetro lleva el id de un inscrito (hoy viajan como lista en `enrolledIds.Contains`, un parámetro por id en EF 10, igual que en la Tarea 1).

```csharp
    [SqlServerFact]
    public Task La_actividad_no_manda_IsDescendantOf_ni_listas_de_ids() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        var cap = new CapturadorDeSql();
        await using var db = cap.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId();
        var a  = SqlServerReportes.NuevoId();
        var prefijo = SqlServerReportes.NuevoId("TN")[..13];   // los inscritos son {prefijo}1 .. {prefijo}6
        await EnrollmentActivityServiceTests.EquipoAsync(db, yo, a, SqlServerReportes.NuevoId(), prefijo);
        cap.Limpiar();

        var r = await new EnrollmentActivityService(db, EnrollmentActivityServiceTests.Reloj).GetGridAsync(
            yo, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), new GridDataRequest { PageSize = 50 });

        r.IsSuccess.Should().BeTrue();
        cap.Textos.Should().NotContain(t => t.Contains("IsDescendantOf"));
        cap.Valores.Should().NotContain(v => v.Contains(prefijo), "las altas del rango se cuentan en SQL, no viajan como lista de ids");
        await tx.RollbackAsync();
    });
```

  - Las pruebas existentes de Actividad (`EnrollmentActivityServiceTests` y `SqlServerActividadTests`) mantienen sus aserciones, pero **su siembra cambia**: el servicio deja de leer los pedidos y lee `SignupMembershipLevelId`, así que la siembra lo rellena como lo harían el alta y el script, con un ayudante nuevo en `ReportesDb`:

```csharp
    /// <summary>
    /// Plan Q (DQ10): rellena SignupMembershipLevelId de los perfiles seguidos con la regla N2, como hacen el alta
    /// (SignupMembershipLevel) y 02-nivel-de-alta.sql. Guarda antes, porque la regla consulta los pedidos en la base.
    /// </summary>
    public static async Task NivelesDeAltaAsync(AppDbContext db)
    {
        await db.SaveChangesAsync();
        foreach (var perfil in db.MemberProfiles.Local.ToList())
            await Repository.Queries.SignupMembershipLevel.RefreshAsync(db, perfil);
        await db.SaveChangesAsync();
    }
```

    En `EnrollmentActivityServiceTests.EquipoAsync` (`:61`), el `await db.SaveChangesAsync();` final pasa a `await ReportesDb.NivelesDeAltaAsync(db);`. Es la única siembra con pedidos de esas dos suites; las demás pruebas de Actividad no siembran pedidos (sus inscritos quedan en «Otro», como hoy).
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

  - **`SelectProductsHandler`:** el perfil se lee con `AsNoTracking` (`:40-42`) y `ComputeAsync` consulta la base, donde los detalles nuevos aún no están hasta el `SaveChangesAsync` de `:111`. Así que, justo después de ese guardado y antes del `return`:

```csharp
        // Plan Q (DQ10): el nivel de alta con los productos ya guardados (ComputeAsync lee la base). El perfil de arriba
        // va sin seguimiento: se carga seguido para asignarlo.
        var perfil = await _db.MemberProfiles.FirstAsync(m => m.MemberId == order.MemberId, ct);
        await SignupMembershipLevel.RefreshAsync(_db, perfil, ct);
        await _db.SaveChangesAsync(ct);
```
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

  **Contraste (solo lectura) tras ejecutarlo en local:** para 2.000 miembros al azar, `SignupMembershipLevel.ComputeAsync` coincide con la columna. Va en `SqlServerActividadTests`:

```csharp
    /// <summary>Plan Q (DQ10): tras 02-nivel-de-alta.sql la columna coincide con la regla N2 (2.000 miembros al azar).</summary>
    [SqlServerFact]
    public async Task El_nivel_de_alta_guardado_coincide_con_la_regla()
    {
        await using var db = SqlServerReportes.Db();
        var muestra = await db.MemberProfiles.AsNoTracking()
            .OrderBy(m => Guid.NewGuid()).Take(2000)
            .Select(m => new { m.MemberId, m.SignupMembershipLevelId }).ToListAsync();

        foreach (var m in muestra)
            m.SignupMembershipLevelId.Should().Be(await SignupMembershipLevel.ComputeAsync(db, m.MemberId), m.MemberId);
    }
```

  (`using MLMConquerorGlobalEdition.Repository.Queries;`.)
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
    - las columnas de la vista se asignan en la proyección: `EnrollmentActivityRowView` (`Repository/Services/Teams/EnrollmentActivityDtos.cs:9-24`) tiene todas sus propiedades con `set` público, así que no cambia.
  - Se borran los dos `ToDictionary` y el pivote en memoria (`:93-160`), y `Empty` deja de usarse salvo en la salida sin nodo.
- [ ] **Paso 6.** Ejecutar el script en local. Suites `Reportes` en verde. Commit con las Tareas 23 y 24: `Plan Q: nivel de alta guardado, busqueda por prefijo, clientes y actividad agrupada en SQL con su migracion y su relleno`.

### Tarea 26: Mi equipo de inscripción, total y página

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerPlanesDeEjecucionTests.cs` (prueba nueva)
- Sin cambios de código: el orden por estructura ya va sobre el nodo, antes del `join`, en el `BuildMyTeamQueryable` de la Tarea 22, y `CurrentUnder` ya es el rango sin antijoin desde la Tarea 21. Esta tarea comprueba que el plan resultante no ordena el rango.

- [ ] **Paso 1.** La prueba, de plan, sobre la raíz mayor local (AMB-700829, unos 119.400 debajo). Dos partes:
  - el SQL que manda el servicio para la página 1.000 (sin búsqueda, sin orden pedido) ordena por `Node` y ni la página ni el conteo llevan la antijoin (`NOT EXISTS`) del nodo vigente;
  - la misma forma de consulta, compilada con `PlanDeEjecucion.CompiladoAsync`, hace un *Index Seek* sobre `IX_GenealogyTree_Node`, no recorre `GenealogyTree` y no tiene un operador `Sort`.

```csharp
    [SqlServerFact]
    public async Task Mi_equipo_de_inscripcion_pagina_por_Node_sin_ordenar_el_rango()
    {
        const string raizMayor = "AMB-700829";
        var cap = new CapturadorDeSql();
        await using (var db = cap.Db())
        {
            var servicio = new EnrollmentTeamService(db, CalificadoEnLosEquiposTests.Rangos(db),
                new EnrollmentTeamPointsService(db), CalificadoEnLosEquiposTests.Calificacion(db));
            await servicio.GetMyTeamGridAsync(raizMayor, new GridDataRequest { Page = 1000, PageSize = 20 });
        }
        var delEquipo = cap.Textos.Where(t => t.Contains("[GenealogyTree]") && t.Contains("[MemberProfiles]")).ToList();
        delEquipo.Should().Contain(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"ORDER BY \[\w+\]\.\[Node\]"));
        delEquipo.Should().NotContain(t => t.Contains("NOT EXISTS"), "con un nodo vivo por miembro no hay antijoin (Tarea 21)");

        await using var db2 = SqlServerReportes.Db();
        var raiz = await db2.GenealogyTree.AsNoTracking().NodeOfAsync(raizMayor);
        var pagina =
            (from g in db2.GenealogyTree.AsNoTracking().CurrentUnder(raiz).Where(g => g.MemberId != raizMayor).OrderBy(g => g.Node)
             join m in db2.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
             select new { m.MemberId, m.FirstName, m.LastName, g.Level })
            .Skip(19_980).Take(20);

        var plan = await PlanDeEjecucion.CompiladoAsync(db2, pagina);

        plan.Should().Contain("IX_GenealogyTree_Node").And.Contain(PlanDeEjecucion.Seek);
        PlanDeEjecucion.Recorridos(plan, "GenealogyTree").Should().BeEmpty();
        plan.Should().NotContain("PhysicalOp=\"Sort\"", "la pagina sale en el orden del indice");
    }
```

  (`using MLMConquerorGlobalEdition.Repository.Grid;`, `using MLMConquerorGlobalEdition.Repository.Services.Ranks;` y `using MLMConquerorGlobalEdition.Repository.Services.Teams;`.) Se ejecuta **después** de las Tareas 21 y 22, así que se espera en verde a la primera: es la comprobación de su efecto conjunto, no una prueba en rojo.
- [ ] **Paso 2.** `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter FullyQualifiedName~Mi_equipo_de_inscripcion_pagina_por_Node`. Si falla por un `Sort`, es una desviación de la Tarea 22 (el `OrderBy` no quedó antes del `join`): se corrige allí y se anota en «Desviaciones». En verde, commit: `Plan Q: prueba de que Mi equipo de inscripcion pagina por Node sin ordenar el rango`.

### Tarea 27: ficha de lo que cambia para el usuario

**Files:**
- Modify: este plan (sección «Riesgos»: Q-R5 a Q-R8 ya lo recogen)

- [ ] **Paso 1.** Revisar con las rejillas abiertas en local (BizCenter, usuario con equipo grande, AMB-700829):
  - el aviso de equipo grande sale;
  - la búsqueda «lucia» encuentra a quien tiene un nombre completo que empieza por «Lucía» o un apellido que empieza por «Lucía» (no a «Ana Lucía …»: es por el comienzo del nombre completo, DQ8), sin tildes ni mayúsculas;
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
- [ ] **Paso 3.** El motivo de la excepción de `migrate-rds.sql` en `SinRutaEnElBinarioTests` (Tarea 14) deja de ser «desactualizado»: regenerado, el script es el historial de migraciones entero (crea la columna en el `CREATE TABLE` de `DualTeamTree` y la quita en `RetiraRutaDelBinario`), igual que `Migrations/`.

```csharp
        ["DeploymentScripts/migrate-rds.sql"] = "es el historial de migraciones regenerado como script (Tarea 31): crea la columna y la quita en RetiraRutaDelBinario, como Migrations/",
```

  La regla sigue en verde.
- [ ] **Paso 4.** Commit: `Plan Q: script idempotente de migraciones regenerado y nota de la base de escala al dia`.

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
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SinRutaEnElBinarioTests.cs` (se quitan las excepciones temporales de `g-binario.sql` y `02-verificar-escala.sql` que puso la Tarea 14; la regla sigue en verde tras los Pasos 3 y 5)

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
  - **Cola y Avisos:** `WHERE Distancia <= 10`. El 10 es `GenealogyUpline.EnrollmentLevels`: SQL no puede leer la constante, así que si la respuesta a Q1 la cambia, se cambia también aquí y en `02-verificar-escala.sql` (Paso 5).
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
| Progreso de rango de ESC-M | < 1 s | > 30 s ❌ | > 30 s ❌ | 17.718 ❌ ² | | |
| Colocar una hoja a 8.507 niveles | < 300 ms | 1.549 | 3.457 ❌ | 1.639 ❌ | | |
| 10 colocaciones a la vez en la línea profunda (p95) | < 2 s | 14.249 ❌ | 29.682 ❌ | 13.695 ❌ | | |
| Vaciar 1.000 deltas en la línea profunda | < 60 s | — | — | — | | |
| Mover ESC-P (50.000) | < 30 s | 126.621 ❌ | 227.134 ❌ | 254.031 ❌ | | |
| Colocaciones durante el movimiento (fallan) | 0 de 15 | 13 ❌ | 13 ❌ | 15 ❌ | | |
| Mi equipo binario, primera página | < 2 s | 2.089 ❌ | 1.692 ✅ ¹ | 2.061 ❌ | | |
| Mi equipo binario, página 1.000 | < 2 s | 2.792 ❌ | 2.323 ❌ ¹ | 2.410 ❌ | | |
| Mi equipo binario, búsqueda por nombre | < 2 s | 4.313 ❌ | 18.510 ❌ | 6.522 ❌ | | |
| Mi equipo de inscripción: conteo / p1 / p1000 | < 2 s | 468 / 1.049 / 1.868 ✅ | 2.776 / 4.545 / 7.082 ❌ | 797 / 1.519 / 2.882 ❌ | | |
| Actividad, 3 meses | < 2 s | 5.344 ❌ | > 30 s ❌ | 7.591 ❌ | | |
| Clientes de todo el equipo | < 2 s | 4.883 ❌ | 24.231 ❌ | 6.735 ❌ | | |
| Eventos (todo el equipo, 200 inscritos) | < 2 s | 1.457 ✅ | 7.163 ❌ | 3.265 ❌ | | |
| Cola sin caché (residual, sin cambio) | — | 8.631 | 12.479 | 4.753 | | |
| Tamaño de DualTeamTree | — | 29,7 GB | 77,9 GB | 39,6 GB | | |
| Filas por tabla de ascendientes (cola, deltas, avisos) | — | 11,7 M | 29,4 M | 14,6 M | | |

¹ Tras el Plan B del conteo (Plan P, Bloque D).

² Con 500.000 no se agota: 37.200 / 16.351 / 17.718 ms (la primera, en frío, pasa de 30 s).

Tarea 0 (500.000 antes, código de be932cc): generación 2.899 s y `02-verificar-escala.sql` a 0 en todo (ESC-M con 500.000 en el binario y 550.000 en inscripción; profundidad máxima 8.506, clave máxima 1.068 B, nivel máximo de inscripción 429; base de 63.112 MB y registro de 36.936 MB). El índice agrupado de `DualTeamTree` ocupa 40.536 MB (669.435 filas) y `HierarchyPath`, 32.875 MB. Las 15 colocaciones durante el movimiento fallan tras unos 15,1 s con «Another dual-tree write holds dual-tree.»; el movimiento tardó 149.282 / 397.268 / 254.031 ms. Las 10 simultáneas: p50 7.874 / 9.364 / 4.284 ms y p95 13.695 / 26.202 / 11.581 ms, ninguna falla. Fuera de la tabla: `PlaceBulkAsync` con 1.000 pendientes, mediana 903,7 s (831 ms por colocación, 0 fallidas); conteo de Mi equipo binario 215 ms; página 1.000 por clave 91 ms; puntos y contadores de pierna 2 ms; visualizador del admin con 6 niveles 44 ms. Suites: BizCenter 5 de 14 en verde (4.390 s), AdminAPI 1 de 1 (17 s), RankEngine 0 de 1 (78 s).

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
| 3 | `01-nodos-de-inscripcion-repetidos.sql` con `SoloInforme=1` | En producción **lo ejecuta Nick**, nunca nosotros (Q2). Es solo lectura y puede adelantarse a la ventana. **Parar aquí si sale más de 0** y decidir con el número |
| 4 | El mismo script con `SoloInforme=0` | Solo si el paso 3 lo pide; en producción también lo ejecuta Nick |
| 5 | Migraciones, en orden: `SuscripcionesActivasPorMiembro`, `ColaDeDeltasDePierna`, `RetiraRutaDelBinario`, `DeltaPorLineaDePatrocinio`, `UnNodoPorMiembro`, `BusquedaYNivelDeAlta` | `dotnet ef database update … --connection …`. Las guardas 51100 y 51108 detienen sin tocar nada |
| 6 | `02-nivel-de-alta.sql` | Por lotes; idempotente |
| 7 | Verificación: 0 miembros con dos nodos vivos, `DualLegDeltas` vacía, columnas e índices nuevos presentes | Solo lectura |
| 8 | Arrancar los servicios, todos con el código del plan | El trabajo `apply-dual-leg-deltas` empieza a correr. SignupAPI (activación, Tarea 18) y el trabajo de deltas (Tarea 19) salen en el mismo despliegue: nunca uno sin el otro (Q-R23) |
| 9 | `03-recuperar-espacio-de-la-ruta.sql`, con los servicios en marcha | Fuera de la ventana; puede tardar con mucha ruta. Vigilar el registro (`log_reuse_wait_desc`) y hacer copias del registro durante la ejecución si la base está en recuperación completa |

Los deltas antiguos de `MemberStatisticDeltas` que queden sin aplicar (`Scope = 0`) los aplica el trabajo nuevo igual que antes.

Si en algún momento hay que ejecutar `02-recalcular-piernas.sql` o `04-contadores-y-colas.sql` del Plan P con la cola de piernas en uso, cada uno neutraliza en la cola la parte que recalcula (Tarea 14, Q-R19); no hace falta vaciarla antes.

## Riesgos

| Id | Riesgo | Mitigación |
|---|---|---|
| Q-R1 | **Piernas diferidas (DQ1).** Lo que se muestra puede ir hasta 1 minuto más el atraso de la cola por detrás: Mi equipo (su total sin filtros sale de los contadores de pierna, `DualTeamService.cs:110-111`, y también el tamaño con el que `TeamGridLimits` decide el límite), visualizador, estadísticas y progreso de rango. La evaluación de rango lee `LeftLegPoints`/`RightLegPoints` (`RankQualificationService.cs:81-82`). La regla de colocación no lee piernas. | Las comisiones y la instantánea vacían la cola antes de leer (Tarea 9). `ProcessRankQueueJob` vacía hasta 5 tandas antes de evaluar (Tarea 9); si el árbol está ocupado por un movimiento, evalúa con lo aplicado y lo registra. Un total o un límite con un minuto de atraso no cambia nada que se pague. Si hiciera falta exactitud en una pantalla, su manejador llama a `DrainAsync(maxBatches: 1)`. |
| Q-R2 | **Coste del vaciado.** Una tanda calcula Σ (profundidad + 1) de sus arranques, y un pico de altas en la línea profunda puede acumular atraso. | `BatchSize` (200 arranques) y `MaxAncestorRowsPerBatch` (400.000 filas) acotan cada tanda; el tiempo de comando cubre la espera del candado más 300 s, y cada pasada vacía solo lo encolado al empezar (revisiones I1 e I2). Se mide «vaciar 1.000» en el Bloque 7. Si no cumple, la mejora es agrupar en C# los arranques de la misma línea antes de expandir: solo hace falta el ascendiente común más profundo y las ramas por debajo. |
| Q-R3 | **Un movimiento grande sigue tomando el árbol en exclusivo** (punto 9 del Plan P). Sin la ruta ni la subida, mover 50.000 debería bajar de 127 s a unos 15 a 40 s; mientras dura, las colocaciones esperan hasta 15 s y fallan con `PLACEMENT_BUSY`. | Medir en el Bloque 7. Si sigue por encima de 15 s, la siguiente tanda debe encolar el movimiento como trabajo de fondo con aviso al usuario, o reescribir las claves por tandas fuera del candado exclusivo. |
| Q-R4 | **Cola sin caché** (punto 7 del Plan P): 8,6 y 12,5 s. | Sin cambio. La caché `DualTeamLegFrontiers` es obligatoria; `04` la reconstruye y los movimientos solo vacían las afectadas. |
| Q-R5 | **Evaluación de rango de los ascendientes a más de 10 niveles** (DQ5): pasa de unos 5 minutos a la fase 2 nocturna de `RankEvaluationSweepJob`. Un rango alcanzado el último día de un periodo podría constar al día siguiente. | Pregunta Q1; hasta que Nick conteste, el plan sigue con 10 (el valor por defecto). Cambiarlo es cambiar una constante, `GenealogyUpline.EnrollmentLevels`, que usan el alta, `DownstreamTriggers`, `EvaluateRankHandler` y el trabajo de deltas (por el parámetro `@niveles`); en SQL solo la repiten el generador y la verificación de escala (Tarea 32). Si Nick lo rechaza, se sube la constante o se encola por encima de la cota solo a quien esté a menos de X puntos de su siguiente rango. |
| Q-R6 | **`DOWNLINE_ENROLLED` solo a 10 niveles.** Los más lejanos dejan de recibir el aviso. | Nadie lee la tabla; el aviso es informativo. Misma cota que I3. |
| Q-R7 | **La búsqueda pasa a ser por el comienzo del nombre completo** («nombre apellido» o «apellido nombre») **o del id**, sin tildes, en equipos grandes y en el buscador del visualizador. No es por el comienzo de cada palabra: «ucia» ya no encuentra «Lucía» y «luci» no encuentra «Ana Lucía Gómez» (sí «gómez» o «ana»). Correo, teléfono y país solo se buscan en equipos de 20.000 o menos. | Se avisa en la rejilla (Tarea 22). En equipos pequeños se mantiene además la búsqueda de siempre. Si hiciera falta el comienzo de cada palabra, haría falta una tabla de palabras por miembro con índice (Full-Text no está instalado). |
| Q-R8 | **Orden limitado por encima de 20.000** (DQ7). | Aviso en la rejilla. El orden por estructura es el que el Plan P midió en 50 y 406 ms. |
| Q-R9 | **Nodos de inscripción repetidos en producción.** La migración `UnNodoPorMiembro` falla cerrada (51108) si los hay. | Script `01` con `SoloInforme` (pregunta Q2). Marca como borrados, no borra: es reversible. |
| Q-R10 | **Eliminar `HierarchyPath` no se deshace con la migración.** El `Down` vuelve a crear la columna vacía. | Volver atrás es restaurar la copia del paso 2 del despliegue. Ningún lector la necesita (Tareas 11 a 14, con la regla). |
| Q-R11 | **La base vacía puede diferir de la real.** `HelpdeskModule` reproduce `fix_helpdesk_tables.sql`, pero las bases reales pudieron recibir otros arreglos a mano. | La prueba compara la base vacía con el modelo, no con la local. Se puede comparar además con la local en solo lectura y anotar las diferencias. |
| Q-R12 | **Escrituras por ascendiente que no vienen del alta** siguen sin cota: `UplineAggregator` (puntos de facturación), `CancelMembershipHandler` y `ProcessScheduledCancellationsJob` (restas por ascendiente). | Fuera del alcance (DQ12). El mismo patrón del Bloque 4 (un delta `SponsorLine` con signo) las resolvería; queda para otro plan. |
| Q-R13 | **La fase 2 del barrido nocturno evalúa a todos los embajadores activos**, sin medir con 1.000.000. | Sin medir. Si Q1 se acepta, pasa a ser la vía de los ascendientes lejanos: medirlo antes de producción con la base de escala. |
| Q-R14 | **`SignupMembershipLevelId` debe mantenerse.** Un camino nuevo que cree el pedido de alta sin llamar a `SignupMembershipLevel` dejaría al miembro en «Otro» en Actividad. | Un único componente, llamado desde la elección de productos y la activación. `02-verificar-escala.sql` y la prueba de contraste lo vigilan. |
| Q-R15 | **Actividad con 1.000.000** puede seguir por encima de 2 s si la ventana de 3 meses abarca casi todo el equipo (el peor caso sintético). | Si no cumple: una tabla resumen diaria por patrocinador, nivel y tipo, que alimenta el alta. Se decide con la cifra. |
| Q-R16 | **Progreso de rango < 1 s con 1.000.000** puede no cumplirse: une un millón de nodos con las activas. | Si no cumple: los dos contadores de calificados pasan a la instantánea nocturna (`MemberStatisticSnapshotJob`) y el progreso los lee de ahí, con la fecha de cálculo. |
| Q-R17 | **Una retirada descarta los deltas pendientes con arranque en el nodo retirado** (revisión C1). Es lo correcto (en el valor verdadero ya se sumaron y se restaron), pero cambia lo que hay en la cola. | `RemoveAsync` los borra en su transacción, con `dual-tree` en exclusivo. Prueba `Retirar_y_recolocar_antes_de_vaciar_no_cuenta_dos_veces` (Tarea 6). |
| Q-R18 | **El vaciado se trocea y espera más** (revisiones I1, I2 e I9): cada pasada vacía solo lo encolado al empezar, una tanda espera el candado hasta su tiempo y el `UPDATE` va por trozos de 4.000 filas para no escalar a candado de tabla. Lo encolado durante una pasada espera a la siguiente (como mucho, 1 minuto más). | La tanda sigue siendo atómica (una transacción). Prueba de concurrencia `Vaciar_en_bucle_durante_diez_colocaciones_a_gran_profundidad` (Tarea 10). |
| Q-R19 | **Los recálculos del Plan P y la cola de piernas** (revisión I3): `02-recalcular-piernas.sql` y `04-contadores-y-colas.sql` recalculan desde cero puntos y contadores; un pendiente aplicado después contaría dos veces. | Cada script toma `dual-leg-deltas` en exclusivo y pone a cero en la cola solo la parte que recalcula (Tarea 14). Prueba manual descrita allí. |
| Q-R20 | **Interbloqueos entre el vaciado de piernas y el trabajo de estadísticas** (revisión C2): los dos actualizan filas de `MemberStatistics` en distinto orden. | Cada tanda de los dos va en una transacción con `XACT_ABORT` y se reintenta entera si es víctima (1205), hasta 3 veces. |
| Q-R21 | **El índice filtrado de suscripciones activas no filtra `IsDeleted`** (revisión I4): una suscripción borrada lógicamente y activa cuenta como calificada. | Es lo que se cuenta hoy: ni las consultas ni un filtro global de la entidad excluyen las borradas. Cambiarlo sería un cambio de regla, fuera de este plan. |
| Q-R22 | **Un segundo nodo vivo para el mismo miembro falla** (DQ9): `UX_GenealogyTree_MemberId_Vivo` lo rechaza con 2601. | En el alta solo puede pasar con dos altas que sacan el mismo id libre a la vez: se reintenta con otro id, igual que con `AK_MemberProfiles_MemberId` (Tarea 21). Nada más escribe nodos de inscripción para un miembro existente. |
| Q-R23 | **Las Tareas 18 y 19 no se despliegan por separado.** Con la activación nueva y el trabajo viejo, la fila `SponsorLine` se aplicaría solo al patrocinador y la línea perdería sus puntos de inscripción. | Un único despliegue con todo el plan (paso 8 del despliegue); en local, commits seguidos sin arrancar SignupAPI entre medias. |
| Q-R24 | **`03-recuperar-espacio-de-la-ruta.sql` hace crecer el registro** en recuperación completa (decenas de GB a escala). | Se ejecuta fuera de la ventana, vigilando `log_reuse_wait_desc` y con copias del registro durante la ejecución si hace falta (Tarea 13). |

## Preguntas para Nick

- **Q1. Evaluación de rango a más de 10 niveles (DQ5).** Tras un alta, los ascendientes del 11.º nivel en adelante se evaluarían en el barrido nocturno en lugar de en 5 minutos. ¿Es aceptable, o hay algún corte (fin de mes, campaña) en el que un rango tenga que constar el mismo día para alguien lejano en la línea?
  - **Mientras no haya respuesta, el plan sigue con su valor por defecto: 10 niveles** y el resto en el barrido nocturno. El Bloque 4 se puede hacer igual.
  - **Ajustarlo es cambiar un número**: `GenealogyUpline.EnrollmentLevels` (Tarea 16). Lo leen el alta (Tarea 17), `DownstreamTriggers`, `EvaluateRankHandler` y el trabajo de deltas (Tarea 19, por el parámetro `@niveles`). Fuera de C#, solo el generador y la verificación de escala (Tarea 32) repiten el 10.
- **Q2. Producción.** ¿Se puede ejecutar allí, en solo lectura, `01-nodos-de-inscripcion-repetidos.sql` con `SoloInforme=1`, para saber si hay miembros con más de un nodo de inscripción antes de la ventana?
  - **Es un paso del despliegue que ejecuta Nick, nunca nosotros**: el plan solo toca la base local, la de escala y las dos desechables de la prueba de migraciones. Pasos 3 y 4 del despliegue.
  - Si sale más de 0, se para antes de las migraciones (la guarda 51108 de `UnNodoPorMiembro` también lo impediría) y se decide con el número.
