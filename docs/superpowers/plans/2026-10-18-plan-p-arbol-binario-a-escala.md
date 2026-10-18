# Plan P: árbol binario a escala

## Decisiones de Nick (2026-10-18)

- **Quién se queda el hueco.** Cuando un hueco tiene varios hijos, se lo queda **el más antiguo por (CreationDate, Id)** (responde a P1, segunda parte).
- **Adónde van los demás.** Cada intruso baja **con su subárbol** a la hoja más profunda de la misma pierna, según la regla de derrame del motor (responde a P1, primera parte).
- **Base local.** **Se repara**; no se regenera (responde a P8).
- **`DevSeedController`.** Exige SuperAdmin **también en Development**, salvo el arranque inicial del superadmin si no existe ninguno. Ese caso queda cerrado en cuanto exista uno (responde a P6).

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que el binario (`DualTeamTree`) aguante equipos de 400.000 nodos o más debajo de un miembro, con la vista puesta en millones. Eso exige tres cosas:
- que no vuelvan a aparecer huecos repetidos;
- que el equipo de un miembro sea un rango de un índice;
- que ninguna lectura recorra la tabla por `HierarchyPath`.

El objetivo medible es que Mi equipo (conteo y página), los puntos de pierna y el visualizador respondan en menos de 2 s con 400.000 nodos debajo.

**Diagnóstico de partida:** `docs/arquitectura/diagnostico-huecos-binario.md`. Hay 939 huecos repetidos en local, y los fallos que siguen vivos son estos:
- `PlacementService.PlaceBulkAsync`, que usan `AutoPlacementJob` y `run-auto-now`;
- `RunAutoPlacementHandler`;
- la comprobación de hueco sin candado en los manejadores de colocación de SignupAPI, BizCenter y AdminAPI;
- ningún índice único `(ParentMemberId, Side)` que lo impida.

**Architecture:**
- **Bloque A.** Se reparan los huecos con un script local con guardas.
  - Se crea `UX_DualTeamTree_ParentMemberId_Side`, con una guarda en la migración.
  - Toda escritura del binario pasa por `DualTeamGate`: candado compartido del árbol más candado exclusivo del hueco, se vuelve a comprobar dentro de la transacción y se reintenta.
  - `PlaceBulkAsync` deja de tener estado en memoria: coloca de uno en uno con el mismo núcleo que `PlaceAsync`.
- **Bloque B.** Clave compacta `BinaryPath varbinary(1700)`: 4 bytes de ordinal de raíz, 1 bit por nivel y un bit terminador. Va con `Depth` y contadores de pierna.
  - El subárbol de un nodo es el rango abierto `(lo, hi)` de un índice único filtrado.
  - La escribe un único componente (`DualTeamNodes`), y una regla de prueba impide que se escriba en otro sitio.
- **Bloque C.** Todos los lectores por ruta y por CTE pasan a la clave. Se retira la CTE de 20.000 y el recalculador por ruta. Una regla de prueba prohíbe `HierarchyPath` en los filtros del binario.
- **Bloque D.** Medición en una BD aparte (`MLMConquerorGlobalEditionDb_Escala`) con binarios sintéticos de 400.000 y 1.000.000 de nodos.
- **Bloque E.** El bono de coche se calcula con sumas acumuladas en SQL, sin cargar a todos los miembros activos en memoria.
- **Cierre.** Índice único de `PaymentHistories (GatewayName, GatewayTransactionId)` (R7 del Plan M). Con eso y el Bloque A, RCSI queda desbloqueado en producción.

**Tech Stack:**
- .NET 10 y EF Core 10.0.11 sobre SQL Server.
- xUnit, FluentAssertions, EF InMemory y `[SqlServerFact]` (`BizCenter.Tests/Reportes/SqlServerReportes.cs`).
- `sqlcmd -b -I` para los scripts.
- Rama: `plan-p-arbol-binario-a-escala`, desde `master`.

| Bloque | Tareas | Entrega | Despliegue |
|---|---|---|---|
| A. Huecos | 1–8 | Pruebas en rojo, `DualTeamGate`, colocación corregida en los 6 escritores, reparación, índice único, generadores retirados | Ventana única (ver «Despliegue») |
| B. Clave compacta | 9–14 | `DualBinaryPath`, columnas e índice, `DualTeamNodes`, relleno, `DualSubtree` por rango, colas y contadores | En la misma ventana |
| C. Lectores | 15–17 | Lectores por la clave, CTE retirada, regla contra `HierarchyPath` | Con B |
| D. Escala | 18–19 | BD de escala, generador y mediciones < 2 s | Solo local |
| E. Bono de coche | 20 | Cálculo agregado en SQL | Cualquiera |
| Cierre | 21–22 | R7 de `PaymentHistories`, guion de despliegue, verificación | Ventana única |

**Reglas que se aplican en todo el plan** (las mismas del Plan M):
- **TDD.** Primero la prueba en rojo y después el mínimo código que la pone en verde. Las pruebas `[SqlServerFact]` siembran sus datos con prefijo propio dentro de una transacción que se deshace (`await using var tx …; … await tx.RollbackAsync()`), salvo las de concurrencia, que necesitan varias conexiones. Esas limpian sus filas por prefijo en `finally`.
- **Hora del servidor:** `IDateTimeProvider.Now` en C# y `GETDATE()` en SQL. Nunca UTC.
- **Commits:** en español, sin tildes, describiendo solo lo que se hizo, **sin** `Co-Authored-By` ni menciones de herramientas. `git add` con rutas explícitas.
- **Una sola base: la local** (`MLMConquerorGlobalEditionDb` en `.`). La única excepción es la BD de escala del Bloque D, y su script exige `DB_NAME() = 'MLMConquerorGlobalEditionDb_Escala'`. **Nunca** la RDS comentada en `Signups/appsettings.json`.
- **Componentes unificados:** cada regla del binario vive en un único sitio.
  - `Domain/Services/DualTeamPlacementRules` (decisión del hueco).
  - `Domain/Services/DualBinaryPath` (codificación).
  - `Repository/Queries/DualTeamGate` (candados).
  - `Repository/Queries/DualTeamNodes` (construir y reubicar nodos).
  - `Repository/Queries/DualSubtree` (rangos).
- **Migraciones:** se crean con `dotnet ef migrations add` y se aplican a mano con `--connection` antes de arrancar ningún servicio (los ocho `Program.cs` hacen `MigrateAsync`).

**Comandos:**
```bash
# pruebas
dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter "FullyQualifiedName~DualTeam|FullyQualifiedName~DualBinaryPath"
dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~Binario"
# migraciones
dotnet ef migrations add <Nombre> --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.AdminAPI
dotnet ef database update --project MLMConquerorGlobalEdition.Repository --startup-project MLMConquerorGlobalEdition.AdminAPI \
  --connection "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True"
# scripts locales
sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-p/<script>.sql
```

---

## Bloque A: reparación de los huecos y prevención

### Tarea 1: pruebas que reproducen los huecos (en rojo)

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerColocacionBinarioTests.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/BinarioDb.cs`
- Modify: `MLMConquerorGlobalEdition.Domain.Tests/DualTeamPlacementRulesTests.cs:55-63`
- Create: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/Placement/RunAutoPlacementHandlerTests.cs`

- [ ] **Paso 1.** Sustituir la prueba que da por bueno el fallo (`Decide_WhenSponsorFullButNoCachedTail_FallsBackToSponsor`):

```csharp
    [Fact]
    public void Decide_WhenSponsorFullButNoTail_Throws()
    {
        // Un derrame sin cola no tiene destino: caer en el patrocinador repetía su hueco izquierdo
        // (diagnostico-huecos-binario.md, §1.2 fallo 1).
        var act = () => DualTeamPlacementRules.Decide(Sponsor, sponsorHasLeftChild: true, sponsorHasRightChild: true,
                                                      deepestNodeId: null, deepestNodeHasLeftChild: false);
        act.Should().Throw<InvalidOperationException>();
    }
```

- [ ] **Paso 2.** Crear `BinarioDb.cs`, la ayuda de siembra (prefijo propio y sin perfiles, porque `DualTeamTree` no tiene FK):

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

public static class BinarioDb
{
    public static string Prefijo(string etiqueta) => $"{etiqueta}-{Guid.NewGuid():N}"[..14];

    /// <summary>Árbol pequeño por (id, padre, lado). La raíz va con padre null.</summary>
    public static async Task SembrarAsync(AppDbContext db, params (string Id, string? Padre, TreeSide Lado)[] nodos)
    {
        var rutas = new Dictionary<string, string>();
        foreach (var (id, padre, lado) in nodos)
        {
            var ruta = padre is null ? $"/{id}/" : $"{rutas[padre]}{id}/";
            rutas[id] = ruta;
            db.DualTeamTree.Add(new DualTeamEntity
            {
                MemberId = id, ParentMemberId = padre, Side = lado, HierarchyPath = ruta,
                CreationDate = DateTime.Now, CreatedBy = "plan-p-prueba", LastUpdateDate = DateTime.Now
            });
        }
        await db.SaveChangesAsync();
    }

    public static Task<int> HijosAsync(AppDbContext db, string padre, TreeSide lado) =>
        Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            db.DualTeamTree, d => d.ParentMemberId == padre && d.Side == lado);
}
```

Cuando el Bloque B añada `BinaryPath`, `SembrarAsync` pasará a construir los nodos con `DualTeamNodes` (Tarea 11).

- [ ] **Paso 3.** Crear `SqlServerColocacionBinarioTests.cs` con los tres escenarios del diagnóstico. Están reproducidos y en verde en el scratchpad, y aquí se escriben esperando **1** hijo:

```csharp
using FluentAssertions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerColocacionBinarioTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [SqlServerFact]
    public async Task Bulk_dos_patrocinados_con_el_izquierdo_ocupado_no_repiten_hueco()
    {
        var x = BinarioDb.Prefijo("PP1");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));

        var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(new[] { ($"{x}A", $"{x}P"), ($"{x}B", $"{x}P") });

        r.IsSuccess.Should().BeTrue(r.Error);
        (await BinarioDb.HijosAsync(db, $"{x}P", TreeSide.Left)).Should().Be(1);
        (await BinarioDb.HijosAsync(db, $"{x}P", TreeSide.Right)).Should().Be(1);
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Bulk_derrame_y_alta_directa_bajo_el_mismo_nodo_no_repiten_hueco()
    {
        var x = BinarioDb.Prefijo("PP3");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left),
            ($"{x}L", $"{x}P", TreeSide.Left), ($"{x}R", $"{x}P", TreeSide.Right));

        var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(new[] { ($"{x}Y", $"{x}P"), ($"{x}Z", $"{x}L") });

        r.IsSuccess.Should().BeTrue(r.Error);
        (await BinarioDb.HijosAsync(db, $"{x}L", TreeSide.Left)).Should().Be(1);
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Bulk_cola_cacheada_con_hijo_izquierdo_recibe_el_derecho()
    {
        var x = BinarioDb.Prefijo("PP2");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left),
            ($"{x}R", $"{x}P", TreeSide.Right), ($"{x}LL", $"{x}L", TreeSide.Left));
        db.DualTeamLegFrontiers.Add(new DualTeamLegFrontier
        {
            SponsorMemberId = $"{x}P", PreferredSide = TreeSide.Left, DeepestMemberId = $"{x}L", DeepestDepth = 3,
            CreationDate = DateTime.Now, CreatedBy = "plan-p-prueba", LastUpdateDate = DateTime.Now
        });
        await db.SaveChangesAsync();

        var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(new[] { ($"{x}Q", $"{x}P") });

        r.IsSuccess.Should().BeTrue(r.Error);
        (await BinarioDb.HijosAsync(db, $"{x}L", TreeSide.Left)).Should().Be(1);
        await tx.RollbackAsync();
    }
}
```

- [ ] **Paso 4.** `RunAutoPlacementHandlerTests` (InMemory): dos miembros con la ventana vencida y el mismo patrocinador, que tiene hijo izquierdo. Después de `Handle`, el patrocinador tiene un hijo por lado. Hoy salen dos a la izquierda, porque las lecturas `AsNoTracking` no ven lo pendiente y el `SaveChanges` es único (`RunAutoPlacementHandler.cs:113`). Se usa el `DbContext` InMemory de `AdminAPI.Tests/Helpers` como en las pruebas de colocación vecinas.

- [ ] **Paso 5.** Ejecutar y ver el rojo:
  - `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter FullyQualifiedName~Binario`: **3 fallan**, con `Expected 1, but found 2`.
  - Domain.Tests: `Decide_WhenSponsorFullButNoTail_Throws` falla.
  - AdminAPI.Tests: `RunAutoPlacement…` falla.

- [ ] **Paso 6.** Commit: `Plan P: pruebas que reproducen los huecos repetidos del binario (en rojo)`.

### Tarea 2: `DualTeamGate`

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Queries/DualTeamGate.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerDualTeamGateTests.cs`

**Diseño:**
- Toda colocación toma, dentro de su transacción, `dual-tree` en modo **compartido** y `dual-slot:{padre}:{lado}` en modo **exclusivo**.
- Un movimiento de subárbol, una retirada o una reparación toma `dual-tree` en **exclusivo**: espera a que terminen las colocaciones y las detiene mientras reescribe rutas y claves.
- El orden es siempre árbol y después hueco, así que los usuarios del candado no se bloquean entre sí.
- El hueco se calcula **antes** del candado. Dentro del candado se vuelve a comprobar y, si alguien lo ha ocupado, se deshace y se recalcula.
- El índice único de la Tarea 5 es la red de seguridad.

- [ ] **Paso 1.** Prueba en rojo. Dos conexiones: la A toma el hueco `(P, Left)` y no confirma; la B intenta el mismo hueco con un tiempo de espera de 500 ms y recibe `DualTeamBusyException`. Un hueco distinto `(P, Right)` sí lo obtiene. El movimiento (`EnterTreeExclusiveAsync`) espera mientras A tenga su candado compartido.

- [ ] **Paso 2.** Implementación:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan P: serializa las escrituras del binario. Una colocación toma "dual-tree" compartido y
/// "dual-slot:{padre}:{lado}" exclusivo; un movimiento o retirada de subárbol toma "dual-tree"
/// exclusivo. Dueño Transaction: se sueltan al confirmar o deshacer. Quien llama vuelve a comprobar
/// el hueco DENTRO del candado; UX_DualTeamTree_ParentMemberId_Side es la red. Mismo patrón que
/// GenealogyChildrenGate y MemberFundsGate. Fuera de SQL Server (InMemory) no hace nada.
/// </summary>
public static class DualTeamGate
{
    public const int DefaultLockTimeoutMs = 15000;
    public const string TreeResource = "dual-tree";

    public static string SlotResource(string parentMemberId, TreeSide side) => $"dual-slot:{parentMemberId}:{(int)side}";

    public static async Task EnterSlotAsync(AppDbContext db, string parentMemberId, TreeSide side,
        CancellationToken ct = default, int lockTimeoutMs = DefaultLockTimeoutMs)
    {
        if (!Applies(db)) return;
        await LockAsync(db, TreeResource, "Shared", lockTimeoutMs, ct);
        await LockAsync(db, SlotResource(parentMemberId, side), "Exclusive", lockTimeoutMs, ct);
    }

    public static async Task EnterTreeExclusiveAsync(AppDbContext db, CancellationToken ct = default,
        int lockTimeoutMs = DefaultLockTimeoutMs)
    {
        if (!Applies(db)) return;
        await LockAsync(db, TreeResource, "Exclusive", lockTimeoutMs, ct);
    }

    private static bool Applies(AppDbContext db)
    {
        if (!db.Database.IsSqlServer()) return false;
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("DualTeamGate needs the caller's transaction.");
        return true;
    }

    private static async Task LockAsync(AppDbContext db, string resource, string mode, int timeoutMs, CancellationToken ct)
    {
        var r = await db.Database
            .SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = {mode}, @LockOwner = 'Transaction', @LockTimeout = {timeoutMs}; SELECT @r AS [Value]")
            .ToListAsync(ct);
        if (r.Count != 1 || r[0] < 0) throw new DualTeamBusyException(resource);
    }
}

public sealed class DualTeamBusyException(string resource)
    : Exception($"Another dual-tree write holds {resource}.");
```

- [ ] **Paso 3.** Las pruebas del Paso 1 pasan a verde. Commit: `Plan P: DualTeamGate serializa las escrituras del binario por hueco y por arbol`.

### Tarea 3: script de reparación de los huecos

**Files:**
- Create: `Scripts/sql/plan-p/01-reparar-huecos-binario.sql`
- Create: `Scripts/sql/plan-p/02-recalcular-piernas.sql`

La regla y sus razones están en `diagnostico-huecos-binario.md` §5. Resumen:
- K (el más antiguo por `(CreationDate, Id)`) conserva el hueco.
- Cada intruso baja con su subárbol a la hoja más profunda del subárbol de K: izquierda si está libre y si no derecha. Empate por `(CreationDate, Id)`.
- Va un hueco por lote, en una transacción por lote, y primero el hueco con el P menos profundo.

- [ ] **Paso 1.** Escribir `01-reparar-huecos-binario.sql`:

```sql
/*
Plan P, Tarea 3. Repara los huecos repetidos del binario: en cada (ParentMemberId, Side) con varios
hijos vivos, el mas antiguo por (CreationDate, Id) se queda y los demas bajan CON SU SUBARBOL a la hoja
mas profunda del subarbol del que se queda (regla c de DualTeamPlacementRules, en la pierna del padre).
Un hueco por lote, una transaccion por lote. Idempotente: si no hay huecos repetidos no cambia nada.

SOLO base local. En produccion NO se ejecuta sin decision de Nick (diagnostico §5.4).
Cerrojos: DB_NAME(), -v Confirmo=LOCAL, SERVERPROPERTY('MachineName') = HOST_NAME().
-v SoloInforme=1 simula todo en una transaccion que se deshace e informa de la profundidad resultante.

  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL SoloInforme=1 -i Scripts/sql/plan-p/01-reparar-huecos-binario.sql
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL SoloInforme=0 -i Scripts/sql/plan-p/01-reparar-huecos-binario.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_NAME() <> N'MLMConquerorGlobalEditionDb'
    THROW 51040, N'Este script es solo para MLMConquerorGlobalEditionDb.', 1;
IF N'$(Confirmo)' <> N'LOCAL'
    THROW 51041, N'Falta la confirmacion explicita: ejecutar con -v Confirmo=LOCAL.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51042, N'El servidor no es la maquina local.', 1;
IF N'$(SoloInforme)' NOT IN (N'0', N'1')
    THROW 51043, N'SoloInforme debe ser 0 o 1.', 1;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Informe antes
SELECT 'antes' AS Fase,
       (SELECT COUNT(*) FROM (SELECT 1 x FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL
                              GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1) h) AS HuecosRepetidos,
       (SELECT MAX(LEN(HierarchyPath) - LEN(REPLACE(HierarchyPath, '/', ''))) - 1 FROM DualTeamTree WHERE IsDeleted = 0) AS ProfundidadMaxima,
       (SELECT COUNT(*) FROM DualTeamTree c WHERE c.IsDeleted = 0 AND c.ParentMemberId IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM DualTeamTree p WHERE p.MemberId = c.ParentMemberId AND p.IsDeleted = 0)) AS Huerfanos;

DECLARE @simular bit = CASE N'$(SoloInforme)' WHEN N'1' THEN 1 ELSE 0 END;
IF @simular = 1 BEGIN TRAN;   -- todo en una, que se deshace al final

DECLARE @P nvarchar(900), @Side int, @K nvarchar(900), @I nvarchar(900), @T nvarchar(900), @TSide int,
        @RutaVieja nvarchar(max), @RutaNueva nvarchar(max), @Lotes int = 0, @Movidos int = 0;

WHILE 1 = 1
BEGIN
    IF @simular = 0 BEGIN TRAN;

    -- El siguiente hueco: el de padre menos profundo (la profundidad cambia con cada lote).
    SELECT TOP 1 @P = h.ParentMemberId, @Side = h.Side
    FROM (SELECT ParentMemberId, Side FROM DualTeamTree WITH (UPDLOCK, HOLDLOCK)
          WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL
          GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1) h
    JOIN DualTeamTree p ON p.MemberId = h.ParentMemberId AND p.IsDeleted = 0
    ORDER BY LEN(p.HierarchyPath) - LEN(REPLACE(p.HierarchyPath, '/', '')), h.ParentMemberId, h.Side;

    IF @@ROWCOUNT = 0 BEGIN IF @simular = 0 COMMIT; BREAK; END;

    SELECT TOP 1 @K = MemberId FROM DualTeamTree
    WHERE ParentMemberId = @P AND Side = @Side AND IsDeleted = 0 ORDER BY CreationDate, Id;

    -- Primer destino: la hoja mas profunda del subarbol de K.
    ;WITH s AS (
        SELECT MemberId, 0 AS D, CreationDate, Id FROM DualTeamTree WHERE MemberId = @K AND IsDeleted = 0
        UNION ALL
        SELECT c.MemberId, s.D + 1, c.CreationDate, c.Id FROM s JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0)
    SELECT TOP 1 @T = s.MemberId FROM s
    WHERE (SELECT COUNT(*) FROM DualTeamTree k WHERE k.ParentMemberId = s.MemberId AND k.IsDeleted = 0) < 2
    ORDER BY s.D DESC, s.CreationDate, s.Id
    OPTION (MAXRECURSION 32767);

    DECLARE intrusos CURSOR LOCAL FAST_FORWARD FOR
        SELECT MemberId FROM DualTeamTree
        WHERE ParentMemberId = @P AND Side = @Side AND IsDeleted = 0 AND MemberId <> @K
        ORDER BY CreationDate, Id;
    OPEN intrusos;
    FETCH NEXT FROM intrusos INTO @I;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @TSide = CASE WHEN EXISTS (SELECT 1 FROM DualTeamTree WHERE ParentMemberId = @T AND Side = 0 AND IsDeleted = 0) THEN 1 ELSE 0 END;
        SELECT @RutaVieja = HierarchyPath FROM DualTeamTree WHERE MemberId = @I AND IsDeleted = 0;
        SELECT @RutaNueva = HierarchyPath + @I + N'/' FROM DualTeamTree WHERE MemberId = @T AND IsDeleted = 0;

        UPDATE DualTeamTree SET ParentMemberId = @T, Side = @TSide, LastUpdateDate = GETDATE(), LastUpdateBy = N'plan-p-reparacion'
        WHERE MemberId = @I AND IsDeleted = 0;

        -- Rutas del subarbol movido (I incluido): por adyacencia, no por LIKE.
        ;WITH s AS (
            SELECT MemberId FROM DualTeamTree WHERE MemberId = @I AND IsDeleted = 0
            UNION ALL
            SELECT c.MemberId FROM s JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0)
        UPDATE d SET HierarchyPath = @RutaNueva + SUBSTRING(d.HierarchyPath, LEN(@RutaVieja) + 1, LEN(d.HierarchyPath))
        FROM DualTeamTree d JOIN s ON s.MemberId = d.MemberId
        OPTION (MAXRECURSION 32767);

        -- Nuevo destino: la hoja mas profunda del subarbol de I (si I es hoja, I mismo).
        ;WITH s AS (
            SELECT MemberId, 0 AS D, CreationDate, Id FROM DualTeamTree WHERE MemberId = @I AND IsDeleted = 0
            UNION ALL
            SELECT c.MemberId, s.D + 1, c.CreationDate, c.Id FROM s JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0)
        SELECT TOP 1 @T = s.MemberId FROM s
        WHERE (SELECT COUNT(*) FROM DualTeamTree k WHERE k.ParentMemberId = s.MemberId AND k.IsDeleted = 0) < 2
        ORDER BY s.D DESC, s.CreationDate, s.Id
        OPTION (MAXRECURSION 32767);

        SET @Movidos += 1;
        FETCH NEXT FROM intrusos INTO @I;
    END
    CLOSE intrusos; DEALLOCATE intrusos;

    SET @Lotes += 1;
    IF @simular = 0 COMMIT;
    IF @Lotes % 50 = 0 PRINT CONCAT('Lotes: ', @Lotes, '  movidos: ', @Movidos);
END

DECLARE @Prof int = (SELECT MAX(LEN(HierarchyPath) - LEN(REPLACE(HierarchyPath, '/', ''))) - 1 FROM DualTeamTree WHERE IsDeleted = 0);
SELECT 'despues' AS Fase, @Lotes AS Lotes, @Movidos AS Movidos, @Prof AS ProfundidadMaxima,
       (SELECT COUNT(*) FROM (SELECT 1 x FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL
                              GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1) h) AS HuecosRepetidos,
       (SELECT COUNT(*) FROM DualTeamTree c JOIN DualTeamTree p ON p.MemberId = c.ParentMemberId AND p.IsDeleted = 0
          WHERE c.IsDeleted = 0 AND c.HierarchyPath <> p.HierarchyPath + c.MemberId + N'/') AS RutasIncoherentes;

IF @simular = 1
BEGIN
    ROLLBACK;
    IF @Prof > 12000
        THROW 51044, N'La reparacion dejaria mas de 12.000 niveles (limite de BinaryPath 13.567): decide Nick la regla (diagnostico §5.4).', 1;
    PRINT 'Simulacion: nada se ha guardado.';
END
```

- [ ] **Paso 2.** Escribir `02-recalcular-piernas.sql`. Mismos cerrojos, con los números 51045 a 51047. Recalcula todo desde cero, de abajo arriba y nivel a nivel, con una transacción por nivel:

```sql
-- (cerrojos como en 01, numeros 51045-51047)
SET NOCOUNT ON; SET XACT_ABORT ON;
IF OBJECT_ID('tempdb..#n') IS NOT NULL DROP TABLE #n;
SELECT d.MemberId, d.ParentMemberId, d.Side,
       LEN(d.HierarchyPath) - LEN(REPLACE(d.HierarchyPath, '/', '')) - 1 AS Nivel,
       CAST(ISNULL(s.PersonalPoints, 0) AS decimal(18,4)) AS Propios,
       CAST(0 AS decimal(18,4)) AS Izq, CAST(0 AS decimal(18,4)) AS Der
INTO #n
FROM DualTeamTree d LEFT JOIN MemberStatistics s ON s.MemberId = d.MemberId
WHERE d.IsDeleted = 0;
CREATE CLUSTERED INDEX cx ON #n (Nivel, MemberId);
CREATE INDEX ix_p ON #n (ParentMemberId, Side) INCLUDE (Propios, Izq, Der);

DECLARE @nivel int = (SELECT MAX(Nivel) FROM #n);
WHILE @nivel >= 0
BEGIN
    UPDATE p SET Izq = ISNULL(l.Total, 0), Der = ISNULL(r.Total, 0)
    FROM #n p
    OUTER APPLY (SELECT SUM(c.Propios + c.Izq + c.Der) AS Total FROM #n c WHERE c.ParentMemberId = p.MemberId AND c.Side = 0) l
    OUTER APPLY (SELECT SUM(c.Propios + c.Izq + c.Der) AS Total FROM #n c WHERE c.ParentMemberId = p.MemberId AND c.Side = 1) r
    WHERE p.Nivel = @nivel;
    SET @nivel -= 1;
END

DECLARE @hecho int = 1;
WHILE @hecho > 0
BEGIN
    BEGIN TRAN;
    UPDATE TOP (20000) d SET LeftLegPoints = n.Izq, RightLegPoints = n.Der, LastUpdateDate = GETDATE(), LastUpdateBy = N'plan-p-piernas'
    FROM DualTeamTree d JOIN #n n ON n.MemberId = d.MemberId
    WHERE d.IsDeleted = 0 AND (d.LeftLegPoints <> n.Izq OR d.RightLegPoints <> n.Der);
    SET @hecho = @@ROWCOUNT;
    COMMIT;
END

UPDATE s SET DualTeamPoints = CAST(n.Izq + n.Der AS int)
FROM MemberStatistics s JOIN #n n ON n.MemberId = s.MemberId
WHERE s.DualTeamPoints <> CAST(n.Izq + n.Der AS int);

DELETE FROM DualTeamLegFrontiers;   -- se reconstruye en la Tarea 14 (04-colas-y-contadores.sql)
SELECT COUNT(*) AS NodosRecalculados FROM #n;
```

- [ ] **Paso 3.** Ejecutar en local, primero en simulación:

```bash
sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL SoloInforme=1 -i Scripts/sql/plan-p/01-reparar-huecos-binario.sql
```

- **Esperado:** antes, `HuecosRepetidos = 939`; después, `HuecosRepetidos = 0`, `Movidos = 44243` y `RutasIncoherentes = 0`.
- **Anotar `ProfundidadMaxima` en el informe de la tarea.** Si pasa de 12.000, el script lanza 51044 y **la tarea se detiene** hasta que Nick decida (pregunta P1).
- Sin `-v SoloInforme` falla (variable sin definir) y no toca nada.

- [ ] **Paso 4.** Con Nick de acuerdo con la regla, aplicar `SoloInforme=0` y después `02-recalcular-piernas.sql`. Ejecutar el `01` una segunda vez da `Lotes = 0` (idempotente).

- [ ] **Paso 5.** Commit: `Plan P: scripts locales de reparacion de los huecos del binario y recalculo de piernas`.

### Tarea 4: `DualTeamPlacementRules` sin caída al patrocinador

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/Services/DualTeamPlacementRules.cs:29-42`

- [ ] **Paso 1.** La prueba de la Tarea 1, Paso 1, está en rojo.

- [ ] **Paso 2.** Cambiar `Decide`:

```csharp
    public static SlotDecision Decide(
        string sponsorMemberId,
        bool sponsorHasLeftChild,
        bool sponsorHasRightChild,
        string? deepestNodeId,
        bool deepestNodeHasLeftChild)
    {
        if (!sponsorHasLeftChild) return new SlotDecision(sponsorMemberId, TreeSide.Left, false);
        if (!sponsorHasRightChild) return new SlotDecision(sponsorMemberId, TreeSide.Right, false);

        // Plan P: un derrame exige la cola ya resuelta. Antes caía en el patrocinador y repetía su hueco
        // izquierdo (diagnostico-huecos-binario.md §1.2, fallo 1).
        if (deepestNodeId is null)
            throw new InvalidOperationException("Spill without a resolved leg tail.");
        var side = deepestNodeHasLeftChild ? TreeSide.Right : TreeSide.Left;
        return new SlotDecision(deepestNodeId, side, true);
    }
```

- [ ] **Paso 3.** `dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter FullyQualifiedName~DualTeamPlacementRules` en verde. Commit: `Plan P: un derrame sin cola resuelta falla en vez de caer en el hueco ocupado del patrocinador`.

### Tarea 5: índice único `(ParentMemberId, Side)` con guarda

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/DualTeamConfiguration.cs`
- Create: migración `HuecoUnicoDelBinario`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerHuecoUnicoTests.cs`

- [ ] **Paso 1.** Prueba en rojo (`[SqlServerFact]`, dentro de una transacción): insertar dos hijos vivos en `(P, Left)` lanza `DbUpdateException` con `SqlException.Number` 2601, y el mensaje contiene `UX_DualTeamTree_ParentMemberId_Side`. Un hijo vivo y uno con `IsDeleted = 1` en el mismo hueco no lanzan nada.

- [ ] **Paso 2.** En `DualTeamConfiguration.Configure`:

```csharp
        // Plan P: un hijo vivo por hueco. Es la red de DualTeamGate (que serializa por hueco) y lo que hace
        // segura la colocación con o sin RCSI (R6 del Plan M). Filtrado: un nodo borrado no ocupa el hueco
        // y las raíces (ParentMemberId NULL) no cuentan.
        builder.HasIndex(x => new { x.ParentMemberId, x.Side })
            .IsUnique()
            .HasDatabaseName("UX_DualTeamTree_ParentMemberId_Side")
            .HasFilter("[IsDeleted] = 0 AND [ParentMemberId] IS NOT NULL");
```

- [ ] **Paso 3.** `dotnet ef migrations add HuecoUnicoDelBinario`. Al principio de `Up`, antes del `CreateIndex` que genera EF, va la guarda:

```csharp
            // Plan P: no se crea la clave sobre datos que no la cumplen.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [DualTeamTree] WHERE [IsDeleted] = 0 AND [ParentMemberId] IS NOT NULL
           GROUP BY [ParentMemberId], [Side] HAVING COUNT(*) > 1)
    THROW 51048, N'DualTeamTree tiene huecos repetidos: ejecutar Scripts/sql/plan-p/01-reparar-huecos-binario.sql (Plan P, Tarea 3).', 1;");
```

`ParentMemberId` es `nvarchar(900)`, así que la clave puede declararse de 1.804 bytes y SQL Server avisa del límite de 1.700. Los ids reales tienen menos de 20 caracteres. Se acepta el aviso; está anotado en «Riesgos».

- [ ] **Paso 4.** Aplicar en local con `--connection`. **Sin la reparación aplicada, la migración se detiene con 51048**: comprobarlo una vez antes del Paso 4 de la Tarea 3. Después, la prueba del Paso 1 queda en verde.

- [ ] **Paso 5.** Commit: `Plan P: indice unico de hueco en el binario con guarda de huecos repetidos`.

### Tarea 6: `PlacementService` con el candado por hueco y sin estado en memoria

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/PlacementService.cs` (se reescriben `PlaceAsync`, `ResolveTargetAsync`, `ResolveDeepestAsync` y `PlaceBulkAsync`, y se eliminan `LegState`, `GetOrSeedStateAsync` y `FlushFrontiersAsync`)
- Create: `MLMConquerorGlobalEdition.Repository/Queries/DualTeamSlotTaken.cs`

- [ ] **Paso 1.** Comprobar que las pruebas de la Tarea 1, Paso 3, siguen en rojo.

- [ ] **Paso 2.** `DualTeamSlotTaken.cs`, que reconoce la violación del índice:

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MLMConquerorGlobalEdition.Repository.Queries;

public static class DualTeamSlotTaken
{
    public const string IndexName = "UX_DualTeamTree_ParentMemberId_Side";

    public static bool Is(DbUpdateException ex) =>
        ex.InnerException is SqlException s && (s.Number is 2601 or 2627)
        && s.Message.Contains(IndexName, StringComparison.Ordinal);
}
```

- [ ] **Paso 3.** El núcleo de `PlaceAsync`. La firma no cambia; sustituye a las líneas 61-127:

```csharp
    private const int MaxIntentos = 5;

    public async Task<Result<PlacementOutcome>> PlaceAsync(
        string memberId, string sponsorMemberId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(memberId))
            return Result<PlacementOutcome>.Failure("INVALID_MEMBER", "memberId is required.");
        if (string.IsNullOrWhiteSpace(sponsorMemberId))
            return Result<PlacementOutcome>.Failure("INVALID_SPONSOR", "sponsorMemberId is required.");

        for (var intento = 1; intento <= MaxIntentos; intento++)
        {
            _db.ChangeTracker.Clear();
            var existing = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
            if (existing is not null)
                return Result<PlacementOutcome>.Success(new PlacementOutcome(
                    memberId, existing.ParentMemberId ?? "", existing.Side, Depth(existing.HierarchyPath), true));

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var target = await ResolveTargetAsync(sponsorMemberId, ct);

                // Plan P: el candado es del HUECO destino (antes, del patrocinador: dos patrocinadores que
                // derramaban al mismo nodo no se esperaban). Dentro, se vuelve a comprobar todo.
                await DualTeamGate.EnterSlotAsync(_db, target.ParentId, target.Side, ct);
                if (await _db.DualTeamTree.AnyAsync(d => d.MemberId == memberId, ct)
                    || await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == target.ParentId && d.Side == target.Side, ct))
                {
                    await tx.RollbackAsync(ct);
                    continue;   // otro escritor llegó antes: se recalcula el hueco
                }

                var parent = await _db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == target.ParentId, ct);
                var node = DualTeamNodes.Child(parent, memberId, target.Side, _clock.Now, "placement-service");
                _db.DualTeamTree.Add(node);
                _db.PlacementLogs.Add(NewLog(memberId, target.ParentId, target.Side));
                await UpsertFrontierAsync(sponsorMemberId, target, memberId, ct);
                await _db.SaveChangesAsync(ct);
                await LegPointMaintenance.ApplyIncrementalAsync(_db, new[] { memberId }, ct);
                await tx.CommitAsync(ct);
                return Result<PlacementOutcome>.Success(new PlacementOutcome(
                    memberId, target.ParentId, target.Side, Depth(node.HierarchyPath), false));
            }
            catch (DbUpdateException ex) when (DualTeamSlotTaken.Is(ex) || IsDuplicateMember(ex))
            {
                await tx.RollbackAsync(ct);   // la red del índice: se recalcula
            }
            catch (DualTeamBusyException ex)
            {
                await tx.RollbackAsync(ct);
                return Result<PlacementOutcome>.Failure("PLACEMENT_BUSY", ex.Message);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                return Result<PlacementOutcome>.Failure("PLACEMENT_FAILED", ex.Message);
            }
        }
        return Result<PlacementOutcome>.Failure("PLACEMENT_CONFLICT", "The slot kept changing; retry later.");
    }

    private static bool IsDuplicateMember(DbUpdateException ex) =>
        ex.InnerException is SqlException s && (s.Number is 2601 or 2627)
        && s.Message.Contains("IX_DualTeamTree_MemberId", StringComparison.Ordinal);
```

`DualTeamNodes.Child` llega en la Tarea 11. Hasta entonces, el Paso 3 usa el `NewNode(...)` de hoy (líneas 255-270), y la Tarea 11 lo sustituye.

- [ ] **Paso 4.** `ResolveTargetAsync` y `ResolveDeepestAsync`:
  - **Siempre** se resuelve la cola si el patrocinador tiene los dos lados, y `deepHasLeft` sale de la BD, como hoy en las líneas 158-164.
  - La BFS de una consulta por nodo (líneas 194-223) se sustituye por **una** consulta. En el Bloque B pasa a ser el rango de la clave (Tarea 14).

```csharp
    private async Task<(string Id, string Path)> ResolveDeepestAsync(
        string sponsorMemberId, TreeSide preferred, CancellationToken ct)
    {
        var frontier = await _db.DualTeamLegFrontiers.AsNoTracking()
            .FirstOrDefaultAsync(f => f.SponsorMemberId == sponsorMemberId, ct);
        if (frontier?.DeepestMemberId is not null)
        {
            var cached = await _db.DualTeamTree.AsNoTracking()
                .FirstOrDefaultAsync(d => d.MemberId == frontier.DeepestMemberId, ct);
            if (cached is not null
                && await _db.DualTeamTree.CountAsync(d => d.ParentMemberId == cached.MemberId, ct) < 2)
                return (cached.MemberId, cached.HierarchyPath);
        }

        // Plan P: una sola consulta (antes, una por nodo). Desempate determinista por (CreationDate, Id).
        var lado = (int)preferred;
        var filas = await _db.Database.SqlQuery<string>($"""
            WITH s AS (
                SELECT d.MemberId, 1 AS D, d.CreationDate, d.Id FROM DualTeamTree d
                WHERE d.ParentMemberId = {sponsorMemberId} AND d.Side = {lado} AND d.IsDeleted = 0
                UNION ALL
                SELECT c.MemberId, s.D + 1, c.CreationDate, c.Id FROM s
                JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0)
            SELECT TOP 1 s.MemberId AS [Value] FROM s
            WHERE (SELECT COUNT(*) FROM DualTeamTree k WHERE k.ParentMemberId = s.MemberId AND k.IsDeleted = 0) < 2
            ORDER BY s.D DESC, s.CreationDate, s.Id
            OPTION (MAXRECURSION 32767)
            """).ToListAsync(ct);
        var id = filas.Count == 1 ? filas[0] : sponsorMemberId;
        var n = await _db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == id, ct);
        return (n.MemberId, n.HierarchyPath);
    }
```

En InMemory, `SqlQuery` no está disponible. Se conserva la BFS actual solo cuando `!_db.Database.IsSqlServer()`, con su comentario, para las pruebas de lógica.

- [ ] **Paso 5.** `PlaceBulkAsync`, sin estado en memoria:

```csharp
    /// <summary>
    /// Plan P: cada miembro pasa por el mismo núcleo que PlaceAsync (transacción, candado del hueco,
    /// comprobación dentro y reintento). El estado en memoria por patrocinador de la versión anterior
    /// no veía lo añadido en la tanda y repetía huecos (diagnostico §1.2).
    /// </summary>
    public async Task<Result<BulkPlacementOutcome>> PlaceBulkAsync(
        IReadOnlyList<(string MemberId, string SponsorMemberId)> pairs, CancellationToken ct = default)
    {
        if (pairs is null || pairs.Count == 0)
            return Result<BulkPlacementOutcome>.Success(new BulkPlacementOutcome(0, 0, 0));

        int placed = 0, skipped = 0, failed = 0;
        foreach (var (memberId, sponsorId) in pairs.DistinctBy(p => p.MemberId))
        {
            var r = await PlaceAsync(memberId, sponsorId, ct);
            if (!r.IsSuccess) failed++;
            else if (r.Value!.AlreadyPlaced) skipped++;
            else placed++;
        }
        return Result<BulkPlacementOutcome>.Success(new BulkPlacementOutcome(placed, skipped, failed));
    }
```

El resumen de la interfaz (líneas 36-41) cambia: «Coloca de uno en uno con PlaceAsync; seguro en concurrencia.» Hay que borrar `BulkSaveChunk`, `LegState`, `GetOrSeedStateAsync` y `FlushFrontiersAsync`.

- [ ] **Paso 6.** Las 3 pruebas de la Tarea 1 quedan en verde. Además se añade la prueba de concurrencia `[SqlServerFact]` `Colocaciones_concurrentes_no_repiten_hueco`. Sin transacción envolvente; limpia por prefijo en `finally`:
  - 20 tareas en paralelo, cada una con su `AppDbContext`, colocan 20 miembros con el mismo patrocinador P, que es raíz.
  - Otras 20 colocan miembros patrocinados por el hijo izquierdo de P, cuya cola coincide.
  - Al acabar: 0 huecos con más de un hijo entre las filas del prefijo, 41 nodos (la raíz más 40) y `PlacementLogs` = 40.

- [ ] **Paso 7.** Commit: `Plan P: PlacementService coloca de uno en uno bajo candado de hueco y sin estado en memoria`.

### Tarea 7: los demás escritores pasan por el candado

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Placement/Commands/PlaceMember/PlaceMemberHandler.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Features/Placement/PlaceMember/PlaceMemberHandler.cs:71-193`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Features/Placement/AdminPlaceMember/AdminPlaceMemberHandler.cs:46-178`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Features/Placement/RunAutoPlacement/RunAutoPlacementHandler.cs` (se reescribe)
- Modify: `AdminRemovePlacementHandler.cs`, `BizCenter/.../RemovePlacementHandler.cs`, `SignupAPI/.../UnplaceMemberHandler.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Jobs/AutoPlacementJob.cs` (sin cambio de lógica; solo el comentario de la línea 79)

- [ ] **Paso 1.** Pruebas `[SqlServerFact]` de concurrencia, en rojo:
  - **BizCenter y Admin.** Dos colocaciones manuales simultáneas al mismo `(P, Left)`: una acaba con éxito y la otra con `SLOT_OCCUPIED`, y queda 1 hijo.
  - **SignupAPI.** Dos peticiones simultáneas con el mismo padre y lado pedidos: los dos miembros quedan colocados en huecos distintos.

- [ ] **Paso 2.** **SignupAPI.** La regla del producto no cambia (el miembro elige el lado y se busca el hueco menos profundo de ese lado bajo el padre pedido), pero cambia cómo se hace:
  - **Todo dentro de una transacción**, con este bucle de reintento:

```csharp
        for (var intento = 1; intento <= 5; intento++)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var slot = await FindFirstEmptySlotAsync(command.PlaceUnderMemberId, side, ct);
            if (slot is null) { await tx.RollbackAsync(ct); return Result<bool>.Failure("NO_AVAILABLE_SLOT", "…"); }
            await DualTeamGate.EnterSlotAsync(_db, slot.Value.ParentMemberId, side, ct);
            if (await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == slot.Value.ParentMemberId && d.Side == side, ct))
            { await tx.RollbackAsync(ct); continue; }
            // … alta del nodo (DualTeamNodes.Child), log, RankEvaluationQueue, SaveChanges, piernas …
            await tx.CommitAsync(ct);
            return Result<bool>.Success(true);
        }
```

  - **`FindFirstEmptySlotAsync` decide por adyacencia, no por una instantánea de rutas.** Hasta el Bloque B es una CTE por `ParentMemberId` ordenada por profundidad y `(CreationDate, Id)`; después, el rango de la clave ordenado por `Depth` (Tarea 15).
  - **Se elimina el tope de 1.500 bytes de `HierarchyPath`** (`:25`, `:190`, `:217`): el límite real pasa a ser la clave (Tarea 9).

- [ ] **Paso 3.** **BizCenter y Admin.** La comprobación de `slotOccupied` se mueve **dentro** de la transacción, detrás del candado:
  - con un movimiento (`existingNode != null`), `EnterTreeExclusiveAsync`;
  - con un alta, `EnterSlotAsync(target, side)`.
  - `catch (DbUpdateException ex) when (DualTeamSlotTaken.Is(ex))` devuelve `SLOT_OCCUPIED`.
  - El recálculo de piernas por ruta (`_legPoints.RecalculateForUplinesAsync`) se sustituye en la Tarea 16.

- [ ] **Paso 4.** **`RunAutoPlacementHandler`.** Delega en el motor único, porque hoy tiene otra regla (BFS por anchura) y un `SaveChanges` único:

```csharp
public class RunAutoPlacementHandler(AppDbContext db, IDateTimeProvider clock, IPlacementService placement)
    : IRequestHandler<RunAutoPlacementCommand, Result<RunAutoPlacementResult>>
{
    private const int PlacementWindowDays = 30;

    public async Task<Result<RunAutoPlacementResult>> Handle(RunAutoPlacementCommand request, CancellationToken ct)
    {
        var cutoff = clock.Now.AddDays(-PlacementWindowDays);
        var pairs = await db.MemberProfiles.AsNoTracking()
            .Where(m => !m.IsDeleted && m.EnrollDate < cutoff && m.SponsorMemberId != null)
            .Where(m => !db.DualTeamTree.Any(d => d.MemberId == m.MemberId))
            .Select(m => new { m.MemberId, m.SponsorMemberId })
            .ToListAsync(ct);

        var placedIds = new List<string>();
        foreach (var p in pairs)
        {
            var r = await placement.PlaceAsync(p.MemberId, p.SponsorMemberId!, ct);
            if (r.IsSuccess && !r.Value!.AlreadyPlaced) placedIds.Add(p.MemberId);
        }
        return Result<RunAutoPlacementResult>.Success(new RunAutoPlacementResult(placedIds.Count, placedIds));
    }
}
```

`IPlacementService` ya está registrado en AdminAPI, porque lo usa `DevSeedController`. La prueba de la Tarea 1, Paso 4, pasa a verde.

- [ ] **Paso 5.** **Retiradas** (Admin, BizCenter y SignupAPI Unplace): `EnterTreeExclusiveAsync` dentro de su transacción. A Unplace se le añade una transacción. El tratamiento de la clave de los hijos que quedan flotando llega en la Tarea 11.

- [ ] **Paso 6.** Todas las pruebas de la tarea quedan en verde. Commit: `Plan P: SignupAPI, BizCenter y AdminAPI colocan bajo DualTeamGate; RunAutoPlacement usa el motor unico`.

### Tarea 8: los generadores de prueba y `DevSeedController`

**Files:**
- Delete: `place_members.sql` (raíz)
- Create: `MLMConquerorGlobalEdition.AdminAPI/Filters/DevelopmentOnlyAttribute.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/DevSeedController.cs` (atributo de clase; se quitan las 10 comprobaciones sueltas)
- Create: `MLMConquerorGlobalEdition.AdminAPI.Tests/Controllers/DevSeedSoloEnDesarrolloTests.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/EscritoresDelBinarioTests.cs`

- [ ] **Paso 1.** Prueba en rojo `DevSeedSoloEnDesarrolloTests`. Con `IWebHostEnvironment.EnvironmentName = "Production"`, el filtro de `DevSeedController` devuelve `NotFoundResult` para cualquier acción (se prueba el filtro con un `ActionExecutingContext` simulado). Además, por reflexión, la clase tiene `[DevelopmentOnly]`.

- [ ] **Paso 2.** El filtro:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MLMConquerorGlobalEdition.AdminAPI.Filters;

/// <summary>Plan P: fuera de Development el controlador no existe (404), sin depender de que cada acción
/// recuerde comprobarlo.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DevelopmentOnlyAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment()) context.Result = new NotFoundResult();
    }
    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
```

Se pone `[DevelopmentOnly]` en `DevSeedController` y se quitan los `if (!_env.IsDevelopment())` de las 10 acciones. Que el controlador siga siendo anónimo en Development es la pregunta P6 para Nick.

- [ ] **Paso 3.** Borrar `place_members.sql`. Los `Scripts/backfill-*.ps1` y `promote-*.ps1` llaman al endpoint de SignupAPI y quedan cubiertos por la Tarea 7. `place-bulk` y `force-place-all` pasan por `PlaceBulkAsync`, ya corregido.

- [ ] **Paso 4.** Regla `EscritoresDelBinarioTests`: ningún fichero fuera de `Repository/Queries/DualTeamNodes.cs`, `Repository/Services/Teams/PlacementService.cs` y los manejadores de colocación o retirada listados puede contener `DualTeamTree.Add`, `DualTeamTree.AddAsync`, `new DualTeamEntity` ni `INSERT INTO DualTeamTree`.
  - Mismo recorrido de ficheros que `SinPrefijoEnInscripcionTests`; se excluyen `Migrations`, `bin`, `obj` y los proyectos de pruebas.
  - También mira los `.sql` de `Scripts/` y la raíz, excepto `Scripts/sql/plan-p/`.
  - En la Tarea 11 la lista se reduce a `DualTeamNodes.cs`.

- [ ] **Paso 5.** Commit: `Plan P: DevSeedController solo en Development por atributo, place_members.sql retirado y regla de escritores del binario`.

---

### Bloque A: desviaciones y notas de la implementación (2026-10-18)

**Estado.** Las tareas 1 a 8 están hechas en la rama, salvo dos pasos que quedan para Nick, con los servicios parados: la reparación en modo aplicar sobre la base local (Tarea 3, Paso 4) y la aplicación local de `HuecoUnicoDelBinario` (Tarea 5, Paso 4). Orden: `01` con `SoloInforme=0`, `02` y después la migración. **No se debe arrancar ningún servicio con este código antes de reparar.** Los ocho `Program.cs` aplican migraciones al arrancar, y la guarda 51048 haría fallar el arranque (falla cerrada, que es lo buscado).

- **RCSI ya está activo en local** (`is_read_committed_snapshot_on = 1`, lo activó el coordinador a mitad de la tanda). La exclusión sale solo del applock del hueco y del índice único, nunca de que una lectura espere. La prueba `Con_RCSI_la_exclusion_sale_del_candado_y_no_de_una_lectura_que_espera` exige RCSI, hace coincidir dos colocaciones justo antes de guardar y está comprobada en rojo sin el candado. Usa la regla b (lado derecho del patrocinador): con la regla a, el índice único de `DualTeamLegFrontiers.SponsorMemberId` serializaba las dos colocaciones por casualidad y escondía el fallo.
- **`PlaceAsync` con la transacción de quien llama.** Si ya hay una transacción abierta (las pruebas que lo deshacen todo, o un manejador), trabaja dentro de ella con un punto de guardado. En vez de `ChangeTracker.Clear()`, al deshacer suelta del seguimiento solo las entidades que añadió, para no tirar los cambios de quien llama; al confirmar también las suelta, para que una tanda larga no acumule entidades.
- **Reintentos de `PlaceAsync`: 50, no 5,** con una espera de 2 a 20 ms al azar. Los derrames de varios patrocinadores convergen en la misma cola, y con N escritores sobre ella en cada ronda gana uno. Con 5 intentos, la prueba de 40 colocaciones simultáneas agotaba los reintentos (`PLACEMENT_CONFLICT`). También reintenta ante cualquier violación de índice único (hueco, miembro o fila de cola) y ante un interbloqueo (1205), siempre que la transacción sea suya.
- **`IPlacementService.PlaceAsync(…, PlacementAudit, …)`.** Es una sobrecarga nueva, para que `RunAutoPlacementHandler` siga firmando `AutoPlaced` / «Admin-triggered auto-placement» con el administrador. Además, `RunAutoPlacementHandler` sigue saltándose a un miembro cuyo patrocinador no está en el árbol (el motor lo crearía como raíz nueva) y coloca por fecha de alta.
- **`LegPointMaintenance` solo en SQL Server.** `PlaceAsync` la omite fuera de un proveedor relacional (InMemory), igual que el candado, para que el motor real sirva en las pruebas de lógica.
- **La cola sin caché sin resultado falla** (`InvalidOperationException`) en vez de devolver el patrocinador. Devolverlo habría vuelto a apuntar a un hueco ocupado.
- **SignupAPI: se mantiene el tope de 1.500 bytes de `HierarchyPath`** hasta que la Tarea 9 traiga el límite real. Quitarlo antes de tener la clave compacta dejaba sin ningún límite. La búsqueda del hueco va por `ParentMemberId` nivel a nivel y se detiene en el primer nivel con hueco (no hace falta CTE). No baja por nodos que superan el tope.
- **BizCenter y Admin: comprobaciones repetidas dentro del candado.** Además del hueco, se vuelven a comprobar el destino y la referencia circular sobre las entidades **recargadas** (`ReloadAsync`): con otra consulta, una entidad ya rastreada no se refresca. El admin toma el árbol en exclusivo también cuando el miembro tiene descendientes flotando bajo `/{miembro}/`, porque eso reescribe rutas.
- **Retiradas.** Las tres (Admin, BizCenter y SignupAPI Unplace) tienen su prueba de que esperan a una colocación en curso.
- **Recalculador de piernas determinista.** `DualTeamPointsRecalculator` y la suma de `RemovePlacementHandler` eligen el hijo de cada lado por `(CreationDate, Id)`. Ojo: `AuditInterceptor` pone `CreationDate = Now` al insertar, así que las filas de un mismo `SaveChanges` empatan y decide el `Id`.
- **Script de reparación (`01`).**
  - Calcula sobre una copia en una tabla temporal (`#a`) en vez de sobre `DualTeamTree`.
  - `SoloInforme=1` no escribe ni bloquea la tabla real: con 11 servicios contra la base local, la simulación en una transacción del plan habría retenido candados exclusivos sobre 140.000 filas durante minutos.
  - `SoloInforme=0` toma `dual-tree` en exclusivo (dueño `Session`) durante todo el script. Tras calcular cada hueco, escribe en `DualTeamTree` las filas que ese hueco cambió, en su propia transacción, y comprueba que el número de filas casa (51051).
  - El destino es siempre una hoja: el nodo más profundo de un subárbol lo es, así que la búsqueda filtra hojas en vez de nodos con menos de 2 hijos.
  - En modo aplicar, la guarda de 12.000 niveles se comprueba lote a lote, antes de escribir. Los lotes anteriores quedan confirmados y coherentes; por eso primero se ejecuta `SoloInforme`.
- **Lista blanca de bases.** `01` y `02` admiten `MLMConquerorGlobalEditionDb` y la base de ensayo `MLMConquerorGlobalEditionDb_PlanPEnsayo`.
- **`02`.**
  - Se niega a ejecutarse si quedan huecos (51052).
  - Toma un solo `MemberStatistics` por miembro.
  - Vacía `DualTeamLegFrontiers`. Hasta la Tarea 14, el motor reconstruye cada cola con una consulta en el primer derrame de cada patrocinador.
- **Índice único: pruebas de SQL Server.** `SqlServerHuecoUnicoTests` se salta, con su motivo, en una base sin el índice (la local, hasta la reparación). Se ejecutó en verde contra la base de ensayo reparada. La prueba de modelo `HuecoUnicoModeloTests` corre siempre.
- **`DevSeedController`.**
  - Lleva `[DevelopmentOnly]` (filtro de recurso) y `[Authorize(Roles = "SuperAdmin")]`.
  - `seed-superadmin` es `[AllowAnonymous]`, pero responde 401 sin sesión y 403 con otro rol en cuanto existe un SuperAdmin.
  - El filtro corre después del middleware de autorización: fuera de Development, una petición anónima recibe 401 y no 404. En ningún caso se ejecuta nada.
- **Resultados medidos (2026-10-18).**
  - **`SoloInforme` en la base local** (sin escribir nada, 14 min 40 s, con los 11 servicios corriendo).
    - Antes: 119.433 nodos, 939 huecos, 45.182 filas en huecos, 44.243 intrusos (44.119 `rank-climb`, 116 `placement-service`, 5 `AMB-…` y 3 `system`), profundidad 934, 308 MB de rutas, 0 huérfanos y 3 raíces.
    - Después: 939 lotes, 44.243 intrusos movidos y **94.726 filas** cambian de ruta, padre o lado. **La profundidad máxima resultante es 8.223**, muy por encima de la cota inferior de ≈1.481 del diagnóstico, aunque bajo la guarda de 12.000.
    - Las rutas pasan de **308 MB a 2.290 MB**, y la más larga tiene 90.454 caracteres.
    - Quedan 0 huecos, 0 rutas incoherentes, 0 huérfanos y 3 raíces.
  - **Ensayo** (`MLMConquerorGlobalEditionDb_PlanPEnsayo`, restaurada de una copia `COPY_ONLY`, ya borrada junto con su backup).
    - La migración antes de reparar se detiene con 51048 y no aplica nada.
    - `01` aplicado: 939 lotes, 115.955 filas escritas (algunas filas se escriben en varios lotes), 17 min 46 s. El mismo resultado que la simulación. Una segunda pasada da Lotes = 0.
    - `02`: 101.758 piernas y 100.911 `DualTeamPoints` cambian, 2.118 colas vaciadas, 73 s. Una segunda pasada no cambia nada.
    - Después la migración se aplica y crea el índice.
    - Verificación: 0 duplicados y 0 huérfanos. Las 119.433 filas se alcanzan exactamente una vez desde las raíces, así que no hay ciclos. Rutas, piernas y `DualTeamPoints` coherentes.
    - Las pruebas `Binario` de BizCenter (24, ninguna saltada), las de SQL de AdminAPI y las de colocación de Signups pasan en verde contra el ensayo.
- **Tarea 21 (R7) no se hace en esta tanda.** En local, `PaymentHistories` tiene 22.769 filas y 0 repetidas por `(GatewayName, GatewayTransactionId)`.

## Bloque B: clave de ruta compacta

### Tarea 9: `DualBinaryPath` (codificación) y su demostración

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Services/DualBinaryPath.cs`
- Create: `MLMConquerorGlobalEdition.Domain.Tests/DualBinaryPathTests.cs`

**La codificación.**
- Clave = `R(4 bytes, big-endian)` ‖ `b1 b2 … bn` ‖ `1` ‖ `0…0` hasta completar el byte.
  - `R` es el ordinal de la raíz (secuencia `DualTeamRootOrdinal`).
  - `bi` es el lado del nivel i: izquierda 0, derecha 1.
  - El `1` final es el **terminador**, el marcador de longitud.
- Profundidad n = posición del último bit a 1 de la clave. El último byte nunca es 0x00.
- Máximo: `(1700 − 4) × 8 − 1 = 13.567` niveles.

**Ejemplos (R = 1):**

| Nodo (camino) | Bits tras `R` | Clave |
|---|---|---|
| raíz (vacío) | `1` | `0x00000001 80` |
| L | `0 1` | `0x00000001 40` |
| R | `1 1` | `0x00000001 C0` |
| L L | `0 0 1` | `0x00000001 20` |
| L R | `0 1 1` | `0x00000001 60` |
| R L | `1 0 1` | `0x00000001 A0` |
| R R | `1 1 1` | `0x00000001 E0` |
| L×7, R | `0000000 1 1` | `0x00000001 01 80` |

**Orden.** SQL Server compara `varbinary` byte a byte y **trata los ceros finales como relleno**. Comprobado en local: `0x80 = 0x8000` y un índice único rechaza el segundo valor. Con esa semántica, la comparación equivale a comparar las fracciones binarias `0.R b1 … bn 1`. El orden resultante es el **recorrido en orden** (inorden): subárbol izquierdo, nodo y subárbol derecho. Para la raíz y sus hijos queda `LL (20) < L (40) < LR (60) < raíz (80) < RL (A0) < R (C0) < RR (E0)`.

**Rango de un subárbol.** Sea X con camino p de n bits y raíz R.
- `lo(X)` = `R ‖ p ‖ 0…` es la clave de X con el terminador a cero.
- `hi(X)` = `R ‖ (p + 1) ‖ 0…`, el sucesor de p como entero de n bits. Si p son todo unos (o n = 0), el acarreo sube a `R`, y `hi = R + 1`.
- **Subárbol de X (X incluido) = `lo(X) < clave < hi(X)`.** Las dos cotas son **exclusivas**.
- **Pierna izquierda** = `lo(X) < clave < clave(X)`. **Pierna derecha** = `clave(X) < clave < hi(X)`.

**Por qué el rango contiene exactamente el subárbol.**
1. **Todo descendiente cae dentro.** Un descendiente tiene camino `p q` con terminador detrás. Su fracción es `0.R p q 1 > 0.R p = lo` y es menor que `0.R (p+1) = hi`.
2. **Lo que tiene otro prefijo de n bits cae fuera.** Si el prefijo es menor que p, la clave es menor o igual que `0.R p' 111… < lo`. Si es mayor, la clave es mayor o igual que `hi`.
3. **El único nodo con fracción exactamente igual a `lo` es el antepasado A del que X cuelga por «derecha y luego solo izquierdas».** Su terminador cae dentro de p. La cota exclusiva lo deja fuera. Ningún otro nodo da exactamente `lo` ni `hi`, porque toda clave tiene su terminador después de su último bit de camino.
4. **No hay dos claves iguales para SQL Server**, ni siquiera ignorando los ceros finales: la posición del último 1 fija n y los bits anteriores fijan p. Por eso el índice puede ser único.
5. **Árboles distintos no se mezclan**, porque los 4 primeros bytes difieren y `hi ≤ R + 1`.

- [ ] **Paso 1.** Pruebas en rojo (`DualBinaryPathTests`):
  - los ejemplos de la tabla, byte a byte;
  - `Depth(Child^k(Root)) = k`;
  - `Child` con n = 13.567 lanza `DualTreeTooDeepException`;
  - **propiedad**, con un comparador que emula a SQL Server (rellena con ceros) y 50 árboles aleatorios de 300 nodos cada uno, mezclando cadenas largas (hasta 200 niveles) con ramas balanceadas. Para todo nodo X:
    - `{ k : lo < k < hi }` es exactamente el subárbol de X;
    - las dos piernas son exactamente los subárboles de sus hijos;
    - no hay dos claves iguales con el comparador;
    - `hi(X)` de un nodo del borde derecho es `R + 1`.

```csharp
    private static int CompararComoSqlServer(byte[] a, byte[] b)
    {
        var n = Math.Max(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var x = i < a.Length ? a[i] : (byte)0;
            var y = i < b.Length ? b[i] : (byte)0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }
```

- [ ] **Paso 2.** Implementación:

```csharp
using System.Buffers.Binary;
using System.Numerics;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Services;

/// <summary>
/// Plan P: clave compacta del binario. R (ordinal de raíz, 4 bytes big-endian) + 1 bit por nivel
/// (izquierda 0, derecha 1) + bit terminador 1 + ceros hasta el byte. SQL Server ignora los ceros
/// finales al comparar varbinary, así que el orden es el de las fracciones binarias: inorden.
/// Subárbol de X = (Lo, Hi) exclusivo; piernas = (Lo, X) y (X, Hi). Demostración en el Plan P, Tarea 9.
/// </summary>
public static class DualBinaryPath
{
    public const int MaxBytes = 1700;
    public const int RootBytes = 4;
    public const int MaxDepth = (MaxBytes - RootBytes) * 8 - 1;   // 13.567
    public const int WarnDepth = 12_000;

    public static byte[] Root(int ordinal)
    {
        var k = new byte[RootBytes + 1];
        BinaryPrimitives.WriteInt32BigEndian(k, ordinal);
        k[RootBytes] = 0x80;
        return k;
    }

    public static int RootOrdinal(ReadOnlySpan<byte> key) => BinaryPrimitives.ReadInt32BigEndian(key);

    /// <summary>Número de niveles bajo la raíz (0 = raíz): la posición del terminador.</summary>
    public static int Depth(ReadOnlySpan<byte> key)
    {
        var last = key.Length - 1;
        if (last < RootBytes || key[last] == 0) throw new ArgumentException("Not a dual-tree key.", nameof(key));
        return (last - RootBytes) * 8 + 7 - BitOperations.TrailingZeroCount((uint)key[last]);
    }

    public static byte[] Child(ReadOnlySpan<byte> parent, TreeSide side)
    {
        var n = Depth(parent);
        if (n + 1 > MaxDepth) throw new DualTreeTooDeepException(n + 1);
        var k = new byte[RootBytes + (n + 1) / 8 + 1];
        parent.CopyTo(k);
        SetBit(k, n, side == TreeSide.Right);   // el terminador del padre pasa a ser el bit del lado
        SetBit(k, n + 1, true);                 // terminador del hijo
        return k;
    }

    /// <summary>Cotas exclusivas del subárbol de <paramref name="key"/> (él incluido).</summary>
    public static (byte[] Lo, byte[] Hi) SubtreeBounds(ReadOnlySpan<byte> key)
    {
        var n = Depth(key);
        var lo = key.ToArray();
        SetBit(lo, n, false);                   // R ‖ p ‖ 0…
        var hi = (byte[])lo.Clone();
        var bit = RootBytes * 8 + n - 1;        // último bit de p; con n = 0, el último bit de R
        var i = bit / 8;
        var add = 0x80 >> (bit % 8);
        while (i >= 0)
        {
            var v = hi[i] + add;
            hi[i] = (byte)v;
            if (v <= 0xFF) break;
            add = 1; i--;
        }
        return (lo, hi);
    }

    /// <summary>Cotas exclusivas de una pierna de <paramref name="key"/>.</summary>
    public static (byte[] Lo, byte[] Hi) LegBounds(ReadOnlySpan<byte> key, TreeSide side)
    {
        var (lo, hi) = SubtreeBounds(key);
        return side == TreeSide.Left ? (lo, key.ToArray()) : (key.ToArray(), hi);
    }

    private static void SetBit(byte[] k, int pathBit, bool on)
    {
        var i = RootBytes + pathBit / 8;
        var mask = (byte)(0x80 >> (pathBit % 8));
        k[i] = on ? (byte)(k[i] | mask) : (byte)(k[i] & ~mask);
    }
}

public sealed class DualTreeTooDeepException(int depth)
    : Exception($"Dual-tree depth {depth} exceeds BinaryPath capacity ({DualBinaryPath.MaxDepth}).");
```

- [ ] **Paso 3.** `dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter FullyQualifiedName~DualBinaryPath` en verde. Commit: `Plan P: DualBinaryPath, clave compacta del binario en inorden con rango contiguo por subarbol y por pierna`.

### Tarea 10: columnas, función, secuencia e índice

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Tree/DualTeamEntity.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/DualTeamConfiguration.cs`
- Create: migración `ClaveCompactaDelBinario`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SqlServerClaveCompactaTests.cs`

- [ ] **Paso 1.** Pruebas en rojo (`[SqlServerFact]`):
  - **(a)** `dbo.DualChildKey(p, depth, side)` da los mismos bytes que `DualBinaryPath.Child` para 2.000 claves aleatorias de hasta 600 niveles. Se cargan en una tabla temporal y se comparan.
  - **(b)** Con las claves de un árbol aleatorio en una tabla temporal con índice único, `WHERE k > @lo AND k < @hi` devuelve exactamente el subárbol de cada nodo. Es la misma propiedad de la Tarea 9, pero con la comparación real de SQL Server y sus parámetros `varbinary(1700)`.
  - **(c)** El plan de `COUNT(*)` de un rango usa `Index Seek` sobre `UX_DualTeamTree_BinaryPath`, comprobado con `PlanDeEjecucion` de `BizCenter.Tests/Rendimiento`.

- [ ] **Paso 2.** En la entidad, después de `RightLegPoints`:

```csharp
    /// <summary>Plan P: clave compacta (DualBinaryPath). La escribe solo DualTeamNodes.</summary>
    public byte[]? BinaryPath { get; set; }
    /// <summary>Plan P: niveles bajo su raíz (0 = raíz).</summary>
    public int? Depth { get; set; }
    /// <summary>Plan P: nodos en cada pierna (mantenidos como los puntos, por LegPointMaintenance).</summary>
    public int LeftLegCount { get; set; }
    public int RightLegCount { get; set; }
```

- [ ] **Paso 3.** En la configuración:

```csharp
        builder.Property(x => x.BinaryPath).HasMaxLength(DualBinaryPath.MaxBytes);   // varbinary(1700)
        builder.HasIndex(x => x.BinaryPath)
            .IsUnique()
            .HasDatabaseName("UX_DualTeamTree_BinaryPath")
            .HasFilter("[IsDeleted] = 0 AND [BinaryPath] IS NOT NULL")
            .IncludeProperties(x => new { x.MemberId, x.Depth });
        builder.HasSequence<int>("DualTeamRootOrdinal").StartsAt(1);
```

- [ ] **Paso 4.** `dotnet ef migrations add ClaveCompactaDelBinario`. Al final de `Up` se crea la función que usan el relleno y los movimientos. Es el mismo algoritmo que `DualBinaryPath.Child`:

```csharp
            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION dbo.DualChildKey(@p varbinary(1700), @depth int, @side int)
RETURNS TABLE AS RETURN
WITH a AS (
    SELECT 4 + @depth / 8 + 1        AS bi,   -- byte (base 1) del terminador del padre = bit del lado
           7 - @depth % 8            AS si,
           4 + (@depth + 1) / 8 + 1  AS bj,   -- byte del terminador del hijo
           7 - (@depth + 1) % 8      AS sj)
SELECT CAST(
    SUBSTRING(@p, 1, a.bi - 1)
    + CASE WHEN a.bi = a.bj
           THEN CAST(CAST(((CAST(SUBSTRING(@p, a.bi, 1) AS int) & ~(CASE WHEN @side = 0 THEN POWER(2, a.si) ELSE 0 END))
                           | POWER(2, a.sj)) AS binary(1)) AS varbinary(1))
           ELSE CAST(CAST((CAST(SUBSTRING(@p, a.bi, 1) AS int) & ~(CASE WHEN @side = 0 THEN POWER(2, a.si) ELSE 0 END))
                          AS binary(1)) AS varbinary(1)) + 0x80
      END AS varbinary(1700)) AS ChildKey
FROM a;");
```

En `Down`: `DROP FUNCTION IF EXISTS dbo.DualChildKey;`.

El cuerpo de la función se comprobó en `tempdb` el 2026-10-18 con los ejemplos de la tabla de la Tarea 9, incluidos los cambios de byte a los 7 y 8 niveles: todos coinciden. También se comprobó el rango del subárbol de L (`0x…01 00 < k < 0x…01 80`): incluye LL y deja fuera a la raíz.

- [ ] **Paso 5.** Aplicar en local y dejar en verde las pruebas (a) y (b). La (c) se cierra tras el relleno (Tarea 12). Commit: `Plan P: columnas BinaryPath, Depth y contadores de pierna, funcion DualChildKey, secuencia de raices e indice unico de la clave`.

### Tarea 11: `DualTeamNodes`, el único que escribe la clave

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Queries/DualTeamNodes.cs`
- Modify: `PlacementService.cs` (`NewNode` y la raíz del patrocinador), `SignupAmbassadorHandler.cs:285-298`, `RootAmbassadorSeeder.cs:80-91`, `SignupAPI PlaceMemberHandler`, `BizCenter PlaceMemberHandler`, `AdminPlaceMemberHandler`, `AdminRemovePlacementHandler`, `BizCenter RemovePlacementHandler`, `UnplaceMemberHandler`, `BinarioDb.SembrarAsync`
- Modify: `EscritoresDelBinarioTests.cs` (la lista se reduce a `DualTeamNodes.cs`)

- [ ] **Paso 1.** Pruebas en rojo (InMemory y `[SqlServerFact]`):
  - una colocación deja `BinaryPath = Child(parent.BinaryPath, side)` y `Depth = parent.Depth + 1`;
  - una raíz nueva recibe un ordinal nuevo;
  - **mover** un subárbol de 50 nodos reescribe todas sus claves: el rango nuevo devuelve los 50 y el viejo ninguno;
  - **retirar** un nodo con hijos deja a los hijos en una raíz flotante con ordinal nuevo, y al volver a colocarlo recuperan clave bajo su nueva posición;
  - la regla de escritores solo admite `DualTeamNodes.cs`.

- [ ] **Paso 2.** Implementación:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan P: el único sitio que construye nodos del binario y reescribe sus claves (HierarchyPath,
/// BinaryPath, Depth). Lo vigila EscritoresDelBinarioTests. Los movimientos y retiradas se llaman
/// con DualTeamGate.EnterTreeExclusiveAsync tomado.
/// </summary>
public static class DualTeamNodes
{
    private static int _ordinalEnMemoria;   // InMemory (pruebas de lógica): no hay secuencia

    public static async Task<DualTeamEntity> RootAsync(AppDbContext db, string memberId, DateTime now, string createdBy, CancellationToken ct)
    {
        var ordinal = await NextRootOrdinalAsync(db, ct);
        return new DualTeamEntity
        {
            MemberId = memberId, ParentMemberId = null, Side = TreeSide.Left,
            HierarchyPath = $"/{memberId}/", BinaryPath = DualBinaryPath.Root(ordinal), Depth = 0,
            CreationDate = now, CreatedBy = createdBy, LastUpdateDate = now
        };
    }

    public static DualTeamEntity Child(DualTeamEntity parent, string memberId, TreeSide side, DateTime now, string createdBy)
    {
        if (parent.BinaryPath is null || parent.Depth is null)
            throw new InvalidOperationException($"Parent {parent.MemberId} has no BinaryPath (backfill pending).");
        return new DualTeamEntity
        {
            MemberId = memberId, ParentMemberId = parent.MemberId, Side = side,
            HierarchyPath = DualTeamPlacementRules.ChildPath(parent.HierarchyPath, memberId),
            BinaryPath = DualBinaryPath.Child(parent.BinaryPath, side), Depth = parent.Depth + 1,
            CreationDate = now, CreatedBy = createdBy, LastUpdateDate = now
        };
    }

    /// <summary>
    /// Reescribe la clave de todo el subárbol de <paramref name="subtreeRootId"/> a partir de su nodo,
    /// que ya tiene la clave nueva (o de <paramref name="virtualRootKey"/> si el nodo ya no existe: hijos
    /// flotantes de una retirada). Pone a NULL las claves del rango viejo y las rellena nivel a nivel
    /// con dbo.DualChildKey, igual que el relleno. Nivel a nivel, dentro de la transacción de quien llama.
    /// </summary>
    public static async Task RekeySubtreeAsync(AppDbContext db, string subtreeRootId, byte[] oldKey,
        byte[]? virtualRootKey, CancellationToken ct)
    {
        var (lo, hi) = DualBinaryPath.SubtreeBounds(oldKey);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE DualTeamTree SET BinaryPath = NULL, Depth = NULL
            WHERE BinaryPath > {lo} AND BinaryPath < {hi} AND MemberId <> {subtreeRootId} AND IsDeleted = 0
            """, ct);
        if (virtualRootKey is not null)
        {
            var d0 = DualBinaryPath.Depth(virtualRootKey);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE c SET BinaryPath = k.ChildKey, Depth = {d0} + 1
                FROM DualTeamTree c CROSS APPLY dbo.DualChildKey({virtualRootKey}, {d0}, c.Side) k
                WHERE c.ParentMemberId = {subtreeRootId} AND c.IsDeleted = 0
                """, ct);
        }
        int n;
        do
        {
            n = await db.Database.ExecuteSqlRawAsync("""
                UPDATE c SET BinaryPath = k.ChildKey, Depth = p.Depth + 1
                FROM DualTeamTree c
                JOIN DualTeamTree p ON p.MemberId = c.ParentMemberId AND p.IsDeleted = 0
                CROSS APPLY dbo.DualChildKey(p.BinaryPath, p.Depth, c.Side) k
                WHERE c.IsDeleted = 0 AND c.BinaryPath IS NULL AND p.BinaryPath IS NOT NULL
                """, ct);
        } while (n > 0);
    }

    public static async Task<int> NextRootOrdinalAsync(AppDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return Interlocked.Increment(ref _ordinalEnMemoria);
        var r = await db.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR dbo.DualTeamRootOrdinal AS [Value]").ToListAsync(ct);
        return r[0];
    }
}
```

**Cuidado.** El bucle de `RekeySubtreeAsync` rellena **cualquier** fila con `BinaryPath IS NULL` cuyo padre tenga clave. Tras el relleno de la Tarea 12 solo quedan a NULL las que acaba de anular, así que no toca nada más. Hasta que el relleno termine **no se despliega** este código, y la ventana única lo garantiza.

- [ ] **Paso 3.** Llevar a todos los escritores a `DualTeamNodes`:
  - **`PlacementService`.** `Child` y `RootAsync`. La raíz del patrocinador de las líneas 139-143 se conserva, pero sale de `RootAsync`.
  - **SignupAmbassador y RootAmbassadorSeeder.** `RootAsync`.
  - **Movimientos (BizCenter y Admin).** Con el candado exclusivo del árbol, el nodo movido se crea con `Child(targetParent, …)`. Después se llama a `RekeySubtreeAsync(db, memberId, oldKey, null, ct)`.
    - Las rutas de texto de los descendientes se siguen reescribiendo, pero el filtro pasa a ser el rango de la clave vieja y no `StartsWith`.
    - Los puntos y contadores se mueven con `LegPointMaintenance.MoveSubtreeAsync` (Tarea 16).
  - **Retiradas.** Antes de borrar el nodo se guarda `oldKey` y `virtual = DualBinaryPath.Root(await NextRootOrdinalAsync(...))`. Después se llama a `RekeySubtreeAsync(db, memberId, oldKey, virtual, ct)`. Los hijos quedan como un árbol flotante con ordinal propio. La colocación posterior (`existingNode == null` con descendientes flotantes, `AdminPlaceMemberHandler.cs:113-129`) llama a `RekeySubtreeAsync(db, memberId, virtualKey, newNode.BinaryPath, ct)`, donde la clave virtual se recalcula con el ordinal guardado en `PlacementLogs`.
    - **Decisión técnica:** el ordinal virtual se guarda en una columna nueva `PlacementLogs.FloatingRootOrdinal int NULL`, en la misma migración de la Tarea 10.
  - **`BinarioDb.SembrarAsync`.** Pasa a `RootAsync` y `Child`.

- [ ] **Paso 4.** En verde, más toda la batería de colocación de las Tareas 1, 6 y 7. Commit: `Plan P: DualTeamNodes construye los nodos y reescribe la clave en altas, movimientos y retiradas`.

### Tarea 12: relleno de la clave, idempotente y por niveles

**Files:**
- Create: `Scripts/sql/plan-p/03-relleno-clave-binario.sql`

- [ ] **Paso 1.** Escribir el script. Cerrojos 51049 a 51051, y además:

```sql
IF EXISTS (SELECT 1 FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL
           GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1)
    THROW 51052, N'Hay huecos repetidos: primero 01-reparar-huecos-binario.sql.', 1;
GO
SET NOCOUNT ON; SET XACT_ABORT ON;

-- 1. Raices (y huerfanos por adyacencia, que pasan a raiz flotante) sin clave: un ordinal cada una,
--    en orden (CreationDate, Id). Idempotente: solo las que no tienen clave.
IF OBJECT_ID('tempdb..#r') IS NOT NULL DROP TABLE #r;
CREATE TABLE #r (MemberId nvarchar(900) PRIMARY KEY, Ordinal int NOT NULL);
INSERT INTO #r (MemberId, Ordinal)
SELECT d.MemberId, NEXT VALUE FOR dbo.DualTeamRootOrdinal OVER (ORDER BY d.CreationDate, d.Id)
FROM DualTeamTree d
WHERE d.IsDeleted = 0 AND d.BinaryPath IS NULL
  AND (d.ParentMemberId IS NULL
       OR NOT EXISTS (SELECT 1 FROM DualTeamTree p WHERE p.MemberId = d.ParentMemberId AND p.IsDeleted = 0));
BEGIN TRAN;
UPDATE d SET BinaryPath = CAST(r.Ordinal AS binary(4)) + 0x80, Depth = 0
FROM DualTeamTree d JOIN #r r ON r.MemberId = d.MemberId;
COMMIT;

-- 2. Niveles: cada pasada da clave a los hijos de los que ya la tienen. Una transaccion por lote:
--    si se corta, la siguiente ejecucion sigue donde quedo.
DECLARE @n int = 1, @pasadas int = 0;
WHILE @n > 0
BEGIN
    BEGIN TRAN;
    UPDATE TOP (50000) c SET BinaryPath = k.ChildKey, Depth = p.Depth + 1
    FROM DualTeamTree c
    JOIN DualTeamTree p ON p.MemberId = c.ParentMemberId AND p.IsDeleted = 0
    CROSS APPLY dbo.DualChildKey(p.BinaryPath, p.Depth, c.Side) k
    WHERE c.IsDeleted = 0 AND c.BinaryPath IS NULL AND p.BinaryPath IS NOT NULL;
    SET @n = @@ROWCOUNT;
    COMMIT;
    SET @pasadas += 1;
    IF @pasadas % 100 = 0 PRINT CONCAT('Pasadas: ', @pasadas);
END

SELECT (SELECT COUNT(*) FROM DualTeamTree WHERE IsDeleted = 0 AND BinaryPath IS NULL) AS SinClave,
       (SELECT MAX(Depth) FROM DualTeamTree WHERE IsDeleted = 0) AS ProfundidadMaxima,
       (SELECT MAX(DATALENGTH(BinaryPath)) FROM DualTeamTree WHERE IsDeleted = 0) AS BytesMaximos,
       @pasadas AS Pasadas;
```

- [ ] **Paso 2.** Ejecutar en local. **Esperado:** `SinClave = 0`, `ProfundidadMaxima` igual a la de la Tarea 3 y `BytesMaximos = 4 + ⌈(prof + 1) / 8⌉`, unos 300 bytes con 2.000 niveles. Repetirlo da `Pasadas = 1` y 0 filas. Cortarlo a mitad con Ctrl+C y relanzarlo termina igual.

- [ ] **Paso 3.** Comprobación extra con SQL: para 1.000 nodos al azar, el recuento por rango coincide con el de la CTE de `DualSubtree`, que aún existe. Va en `SqlServerClaveCompactaTests` como prueba (d). Se cierra también la (c) de la Tarea 10.

- [ ] **Paso 4.** Commit: `Plan P: relleno idempotente y por niveles de la clave compacta del binario`.

### Tarea 13: `DualSubtree` por rango y `OPTION (RECOMPILE)`

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Queries/DualSubtree.cs` (se reescribe)
- Modify: `MLMConquerorGlobalEdition.Repository/Interceptors/RecompileTaggedQueriesInterceptor.cs:19-23`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/RecompilarElEquipoTests.cs`

- [ ] **Paso 1.** Pruebas en rojo:
  - `Under(key)` devuelve el subárbol sin la raíz;
  - `Leg(key, Left/Right)`;
  - el SQL generado contiene la etiqueta y termina en `OPTION (RECOMPILE)`;
  - el plan es `Index Seek` sobre `UX_DualTeamTree_BinaryPath`.

- [ ] **Paso 2.** `DualSubtree`:

```csharp
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan P: el equipo binario de un nodo es el rango (Lo, Hi) de UX_DualTeamTree_BinaryPath
/// (DualBinaryPath). Sustituye a la CTE con tope de 20.000 del Plan M. Las consultas llevan
/// RecompileTag: el tamaño del rango va de 1 a millones.
/// </summary>
public static class DualSubtree
{
    public const string RecompileTag = "DualSubtree.Range: recompilar por tamano de equipo";

    /// <summary>Descendientes de <paramref name="node"/> (él no).</summary>
    public static IQueryable<DualTeamEntity> Under(AppDbContext db, DualTeamEntity node)
    {
        var (lo, hi) = DualBinaryPath.SubtreeBounds(Key(node));
        return Range(db, lo, hi).Where(d => d.MemberId != node.MemberId);
    }

    /// <summary>Los nodos de una pierna de <paramref name="node"/>.</summary>
    public static IQueryable<DualTeamEntity> Leg(AppDbContext db, DualTeamEntity node, TreeSide side)
    {
        var (lo, hi) = DualBinaryPath.LegBounds(Key(node), side);
        return Range(db, lo, hi);
    }

    /// <summary>¿Está <paramref name="candidate"/> dentro del subárbol de <paramref name="root"/> (él incluido)?</summary>
    public static bool Contains(byte[] root, byte[] candidate)
    {
        var (lo, hi) = DualBinaryPath.SubtreeBounds(root);
        return SqlCompare(candidate, lo) > 0 && SqlCompare(candidate, hi) < 0;
    }

    private static IQueryable<DualTeamEntity> Range(AppDbContext db, byte[] lo, byte[] hi) =>
        db.DualTeamTree
            .FromSqlRaw("SELECT * FROM DualTeamTree WHERE BinaryPath > @lo AND BinaryPath < @hi",
                Param("@lo", lo), Param("@hi", hi))
            .TagWith(RecompileTag);

    private static SqlParameter Param(string name, byte[] value) =>
        new(name, SqlDbType.VarBinary, DualBinaryPath.MaxBytes) { Value = value };

    private static byte[] Key(DualTeamEntity n) =>
        n.BinaryPath ?? throw new InvalidOperationException($"{n.MemberId} has no BinaryPath (backfill pending).");

    /// <summary>Comparación de varbinary como SQL Server (ceros finales = relleno).</summary>
    public static int SqlCompare(byte[] a, byte[] b)
    {
        var n = Math.Max(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }
}
```

En InMemory no hay `FromSqlRaw`. Las pruebas de lógica que hoy dependen de `DualSubtree` devolviendo `null` pasan a SQL Server, o usan `Contains` en memoria sobre un conjunto sembrado.

- [ ] **Paso 3.** En el interceptor:

```csharp
    public static string Apply(string commandText) =>
        (commandText.Contains(GenealogySubtree.RecompileTag, StringComparison.Ordinal)
         || commandText.Contains(DualSubtree.RecompileTag, StringComparison.Ordinal))
        && !commandText.TrimEnd().EndsWith(Hint, StringComparison.Ordinal)
            ? commandText.TrimEnd().TrimEnd(';') + "\n" + Hint
            : commandText;
```

- [ ] **Paso 4.** En verde. Commit: `Plan P: DualSubtree por rango de la clave compacta con recompilacion por etiqueta`.

### Tarea 14: colas de derrame y contadores de pierna por la clave

**Files:**
- Modify: `PlacementService.ResolveDeepestAsync` (la consulta de la Tarea 6 pasa al rango)
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/LegPointMaintenance.cs`
- Create: `Scripts/sql/plan-p/04-colas-y-contadores.sql`

- [ ] **Paso 1.** Pruebas en rojo:
  - la cola sin caché sale de **una** consulta sobre el rango de la pierna preferida (se mide con un contador de comandos del interceptor de pruebas);
  - tras colocar una hoja, `LeftLegCount`/`RightLegCount` de todos sus antepasados suben en 1 en la pierna correcta.

- [ ] **Paso 2.** Cola por rango:
  - `SELECT TOP 1 d.MemberId … FROM DualTeamTree d WHERE d.BinaryPath > @lo AND d.BinaryPath < @hi AND d.IsDeleted = 0 AND (SELECT COUNT(*) FROM DualTeamTree k WHERE k.ParentMemberId = d.MemberId AND k.IsDeleted = 0) < 2 ORDER BY d.Depth DESC, d.CreationDate, d.Id`, con `(@lo, @hi) = LegBounds(sponsor, preferred)` y la etiqueta de recompilación.
  - Es O(pierna), pero solo con la caché vacía o caducada. Se mide en el Bloque D.

- [ ] **Paso 3.** `LegPointMaintenance.ApplyIncrementalAsync` suma también los conteos. En el SQL de las líneas 41-74, `#deltas` gana `COUNT(*) AS C` y el `UPDATE` añade:

```sql
    LeftLegCount   = d.LeftLegCount  + COALESCE(l.C, 0),
    RightLegCount  = d.RightLegCount + COALESCE(r.C, 0)
```

- [ ] **Paso 4.** `04-colas-y-contadores.sql` (cerrojos 51053 a 51055) hace dos cosas:
  - recalcula `LeftLegCount`/`RightLegCount` nivel a nivel, igual que `02-recalcular-piernas.sql`, pero contando nodos;
  - reconstruye `DualTeamLegFrontiers`: por cada patrocinador con los dos hijos, la hoja más profunda de su pierna preferida con desempate `(CreationDate, Id)`, por rango de la clave, en lotes de 500 patrocinadores con una transacción por lote.
- Ejecutar en local. **Esperado:** las colas coinciden con las que da `ResolveDeepestAsync` sin caché para 200 patrocinadores al azar (va en `SqlServerClaveCompactaTests` como prueba (e)).

- [ ] **Paso 5.** Commit: `Plan P: colas de derrame y contadores de pierna por la clave compacta`.

---

## Bloque C: los lectores, por la clave

### Tarea 15: todos los lectores por ruta o por CTE pasan al rango

**Files** (cada fila es un cambio; el patrón es `DualSubtree.Under/Leg` o, para mirar si un nodo está dentro, `DualSubtree.Contains`):

| Fichero:línea | Hoy | Después |
|---|---|---|
| `Repository/Services/Teams/DualTeamService.cs:150-163` (Mi equipo) | CTE con tope y, por encima, `HierarchyPath.StartsWith` | `Project(DualSubtree.Under(_db, myNode))`. Se quitan `MaterializedSubtree` y la rama del tope. El nivel sale de `d.Depth - myNode.Depth` en vez de contar `/` (`:247`, `:270`) y la pierna de `LegBounds` |
| `AdminAPI/Controllers/AdminMemberDualTeamController.cs:84-130` | `StartsWith(pathPrefix)` | `DualSubtree.Under`. Nivel = `Depth - rootNode.Depth` |
| `AdminAPI/Controllers/NetworkController.cs:41-80` (visualizador, profundidad ≤ 6) | Carga **todo** el subárbol y cuenta las piernas en O(n²) | Baja por `ParentMemberId` solo `depth` niveles (como mucho 2⁶ = 64 nodos, un *seek* por nivel). Los conteos salen de `LeftLegCount`/`RightLegCount` |
| `RankEngine/Features/GetRankProgress/GetRankProgressHandler.cs:141-153` | Lista de ids por `StartsWith` y luego `Contains` | `COUNT` con una unión entre `DualSubtree.Under` y las suscripciones activas, en una sola consulta |
| `Repository/Services/Teams/TeamStatusChangesService.cs:120-145` | `dualPath.StartsWith(prefix)` por fila | Unión con `DualSubtree.Under(_db, node)` |
| `BizCenter/Services/DownlineGuard.cs:84-100` | `rutaObjetivo.StartsWith(raiz)` | `DualSubtree.Contains(nodoPropio.BinaryPath, nodoObjetivo.BinaryPath)` |
| `BizCenter/Features/Placement/GetAvailableNodes/GetAvailableNodesHandler.cs:121` | Profundidad contando `/` | `node.Depth` |
| `SignupAPI/.../PlaceMemberHandler.cs` `FindFirstEmptySlotAsync` (Tarea 7) | CTE | Rango de la pierna pedida, ordenado por `Depth` y luego `BinaryPath`, y `TOP 1` con hueco en ese lado |
| `BizCenter PlaceMemberHandler.cs:105-111, 199-216` (`HasOwnEnrolledDownlineAsync`) y `:94` (ciclo) | `StartsWith` | `DualSubtree.Under` y `DualSubtree.Contains` |
| `AdminPlaceMemberHandler.cs:65-69` (ciclo) | `StartsWith` | `DualSubtree.Contains` |

- [ ] **Paso 1.** Por cada fila, primero la prueba que fija el resultado actual en un árbol sembrado (InMemory si se puede; si no, `[SqlServerFact]`), después el cambio y la prueba en verde.
- [ ] **Paso 2.** `EvaluateRankHandler.cs:240-241` y `SignupAPI PlaceMemberHandler` (avisos a la línea ascendente) **leen** los antepasados troceando `HierarchyPath`. No es un filtro y se mantiene: la ruta sigue escribiéndose. Se anota para el plan que retire `HierarchyPath`.
- [ ] **Paso 3.** Commit por grupo coherente, por ejemplo `Plan P: Mi equipo y el listado del admin por el rango de la clave`, `Plan P: visualizador por adyacencia acotada y contadores de pierna`, etc.

### Tarea 16: piernas al mover o retirar, sin el recalculador por ruta

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Teams/LegPointMaintenance.cs` (nuevo `MoveSubtreeAsync`)
- Delete: `MLMConquerorGlobalEdition.Repository/Services/Trees/DualTeamPointsRecalculator.cs`, `IDualTeamPointsRecalculator.cs` y sus registros (`BizCenter/Program.cs:111-112` y los de AdminAPI y SignupAPI)

`DualTeamPointsRecalculator` recorre la línea ascendente y, por cada antepasado, suma el subárbol con `StartsWith` (`:61-72`), eligiendo el hijo con un `FirstOrDefault` sin orden. Con 400.000 nodos y 2.000 niveles serían 2.000 sumas de O(subárbol).

- [ ] **Paso 1.** Prueba en rojo: mover un subárbol con 10 nodos y 37 puntos. Los antepasados viejos pierden 37 puntos y 10 nodos en la pierna por la que colgaba; los nuevos los ganan; el resto no cambia.

- [ ] **Paso 2.** `MoveSubtreeAsync(db, subtreeRoot, sign)`:
  - los totales del subárbol salen de `SUM(PersonalPoints)` y `COUNT(*)` sobre el rango de su clave;
  - después se sube desde su padre con la CTE de `ApplyIncrementalAsync`, aplicando el signo, y se actualiza `MemberStatistics.DualTeamPoints`.
- Los movimientos llaman con `-1` antes de soltar el nodo (padre viejo) y con `+1` después de colgarlo. Las retiradas solo con `-1`.

- [ ] **Paso 3.** Sustituir los 5 usos de `RecalculateForUplinesAsync`. En verde. Commit: `Plan P: las piernas se mueven con el subarbol y se retira el recalculador por ruta`.

### Tarea 17: retirar la CTE y la regla contra `HierarchyPath` en el binario

**Files:**
- Modify: `Repository/Queries/DualSubtree.cs` (se borran `DescendantsAsync`, `CountUpToAsync`, `MaterializeAsync`, `MaxRowsForCte` y `MaxRecursion`)
- Delete: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerDualSubtreeTests.cs`, cuyo contenido útil pasa a `SqlServerClaveCompactaTests`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/SinRutaEnElBinarioTests.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SinPrefijoEnInscripcionTests.cs:9` (el comentario «El binario no entra» deja de ser cierto)

- [ ] **Paso 1.** La regla, en rojo hasta terminar la Tarea 15. Recorre igual que `SinPrefijoEnInscripcionTests`: por sentencia, sin comentarios, solo si la sentencia nombra `DualTeamTree` o `DualTeamEntity`.

```csharp
    private static readonly Regex RutaEnFiltro = new(
        @"\bHierarchyPath\s*\.\s*(StartsWith|Contains|IndexOf|EndsWith)\s*\(|EF\s*\.\s*Functions\s*\.\s*Like\s*\(\s*\w+\s*\.\s*HierarchyPath|HierarchyPath\s+LIKE\b|\b(path|ruta|prefix|pathPrefix|rutaObjetivo)\s*\.\s*StartsWith\s*\(",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
```

Excepciones con nombre y motivo: `Repository/Queries/DualTeamNodes.cs`, que **escribe** la ruta de texto, y `Migrations/`. También mira los `.sql` de `Scripts/` salvo `Scripts/sql/plan-p/` y los del Plan M ya cerrados.

- [ ] **Paso 2.** Borrar la CTE y las pruebas que dependían de ella. Todo en verde. Commit: `Plan P: se retira la CTE del binario y una regla prohibe filtrar el binario por HierarchyPath`.

---

## Bloque D: medición a escala (BD aparte)

### Tarea 18: BD de escala y generador sintético

**Files:**
- Create: `Scripts/sql/plan-p/escala/00-crear-bd-escala.sql`
- Create: `Scripts/sql/plan-p/escala/01-generar-binario.sql`

- [ ] **Paso 1.** `00-crear-bd-escala.sql`. Se ejecuta contra `master` y exige `-v Confirmo=ESCALA` y que la máquina sea la local:

```sql
IF N'$(Confirmo)' <> N'ESCALA' THROW 51060, N'Ejecutar con -v Confirmo=ESCALA.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME() THROW 51061, N'Solo en la maquina local.', 1;
IF DB_ID(N'MLMConquerorGlobalEditionDb_Escala') IS NULL CREATE DATABASE MLMConquerorGlobalEditionDb_Escala;
ALTER DATABASE MLMConquerorGlobalEditionDb_Escala SET RECOVERY SIMPLE;
```

Después se aplican las migraciones a esa base, y **solo** a esa: `dotnet ef database update … --connection "Server=.;Database=MLMConquerorGlobalEditionDb_Escala;…"`.

- [ ] **Paso 2.** `01-generar-binario.sql` exige `DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala'` (51062) y recibe `-v Nodos=400000` o `1000000`.
  - **Forma, que imita lo que produce la regla de la cadena más profunda.** Una raíz `ESC-0`. Bajo su pierna izquierda, un miembro `ESC-M` (el que se mide) con N nodos debajo, formados por:
    - una **columna** de 5.000 niveles por la izquierda (la cadena de derrames);
    - de cada nodo de la columna, por la derecha, un **subárbol completo** hasta completar N.
  - **Generación por conjuntos y por niveles.** Primero la columna (5.000 pasadas de un `INSERT`), después los subárboles por niveles con `INSERT … SELECT` desde los nodos del nivel anterior (las dos ramas con `CROSS JOIN (VALUES (0), (1))`).
  - **Ids:** `ESC-{n}`.
  - **Filas mínimas** en `MemberProfiles` (nombre, `Status`, `EnrollDate`) y `MemberStatistics` (`PersonalPoints` entre 0 y 300, determinista por `n % 7`).
  - Al final, `03-relleno-clave-binario.sql` y `04-colas-y-contadores.sql`, con sus cerrojos de base adaptados a `_Escala` mediante `-v Base=ESCALA`, que los dos aceptan (lista blanca de dos nombres).
  - **Informe:** nodos, profundidad máxima, bytes máximos de la clave y tamaño de `UX_DualTeamTree_BinaryPath` (`sys.dm_db_partition_stats`).

- [ ] **Paso 3.** Generar 400.000 y anotar los tiempos. Commit: `Plan P: base de escala y generador sintetico del binario`.

### Tarea 19: mediciones con objetivo

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Binario/EscalaBinarioTests.cs`

Un atributo `[EscalaFact]` salta la prueba si no está definida `MLM_ESCALA_SQLSERVER`, que debe apuntar a `…Database=MLMConquerorGlobalEditionDb_Escala…`. El atributo **comprueba el nombre de la base** y se niega a ejecutar con cualquier otra. Cada prueba se repite 3 veces (la primera calienta la caché) y se toma la mediana.

| Medición | Cómo | Objetivo |
|---|---|---|
| Mi equipo, conteo (400.000) | `DualTeamService` → `CountAsync` sobre `DualSubtree.Under` | < 2 s |
| Mi equipo, primera página y página 1.000 | Orden por `BinaryPath`, paginación por clave (`BinaryPath > @ultima`) | < 2 s cada una |
| Mi equipo con búsqueda por nombre | `Under` unido a perfiles con `LIKE` | < 2 s (si no se cumple, anotar y proponer el índice de texto en el informe) |
| Puntos de pierna (lectura) | `LeftLegPoints`/`RightLegPoints` y contadores de `ESC-M` | < 50 ms |
| Colocar una hoja a 5.000 niveles | `PlaceAsync`, incluida `ApplyIncrementalAsync` | < 2 s |
| Visualizador, profundidad 6 | `NetworkController` | < 2 s |
| Cola sin caché | `ResolveDeepestAsync` con `DualTeamLegFrontiers` vacía | < 2 s; si no, documentar que la caché es obligatoria y que `04` la reconstruye |
| 1.000.000 | Repetir las cuatro primeras con `Nodos=1000000` | < 2 s |

- [ ] **Paso 1.** Escribir las pruebas con el objetivo como aserción (en rojo si no se cumple).
- [ ] **Paso 2.** Ejecutar con 400.000 y con 1.000.000, y escribir los resultados en una sección «Resultados medidos» de este plan, como en el Plan M.
- [ ] **Paso 3.** **Plan B si el conteo pasa de 2 s:** Mi equipo muestra `LeftLegCount + RightLegCount` del propio miembro (exacto por construcción) en vez de contar el rango, y solo cuenta el rango cuando hay un filtro.
- [ ] **Paso 4.** Commit: `Plan P: mediciones del binario con 400.000 y 1.000.000 nodos`.

---

## Bloque E: bono de coche sin cargar todo en memoria

### Tarea 20: sumas acumuladas en SQL

**Files:**
- Modify: `MLMConquerorGlobalEdition.CommissionEngine/Features/CalculateCarBonus/CalculateCarBonusHandler.cs:69-160`
- Create: `MLMConquerorGlobalEdition.BizCenter.Tests/Rendimiento/SqlServerBonoDeCocheTests.cs` (o en `CommissionEngine.Tests`, si existe)

Hoy el manejador carga en memoria a **todos** los miembros activos con su `Node` (`:86-92`) y hace una consulta por embajador para su propia suscripción (`:124-131`). El cálculo por tramos ya es correcto; lo que se cambia es dónde se hace.

- [ ] **Paso 1.** Prueba en rojo `[SqlServerFact]`: en un árbol de inscripción sembrado de 30 nodos con niveles 3 y 4 mezclados, los puntos de equipo por embajador coinciden con el cálculo actual, que se guarda como oráculo en la prueba. Además el manejador no materializa más de `ambassadors.Count` filas (se comprueba con un contador de filas del interceptor de pruebas).

- [ ] **Paso 2.** Una sola consulta SQL con sumas acumuladas. Los puntos por nivel se pasan como `VALUES`:

```csharp
        var niveles = string.Join(",", LevelPoints.Select(kv => $"({kv.Key},{kv.Value})"));
        var filas = await _db.Database.SqlQuery<EquipoDeEmbajador>($"""
            CREATE TABLE #act (Node hierarchyid PRIMARY KEY, Pts int NOT NULL, Acum bigint NULL);
            INSERT INTO #act (Node, Pts)
            SELECT x.Node, x.Pts FROM (
                SELECT g.Node, ISNULL(lp.Pts, 0) AS Pts,
                       ROW_NUMBER() OVER (PARTITION BY g.MemberId ORDER BY g.CreationDate DESC, g.Id DESC) AS rg,
                       ROW_NUMBER() OVER (PARTITION BY g.MemberId, s.MemberId ORDER BY s.CreationDate DESC) AS rs
                FROM GenealogyTree g
                JOIN MembershipSubscriptions s ON s.MemberId = g.MemberId AND s.SubscriptionStatus = {(int)MembershipStatus.Active} AND s.IsDeleted = 0
                LEFT JOIN (VALUES {niveles}) lp(Nivel, Pts) ON lp.Nivel = s.MembershipLevelId
                WHERE g.IsDeleted = 0 AND g.Node IS NOT NULL) x
            WHERE x.rg = 1 AND x.rs = 1;
            UPDATE a SET Acum = t.Acum FROM #act a
            JOIN (SELECT Node, SUM(CAST(Pts AS bigint)) OVER (ORDER BY Node ROWS UNBOUNDED PRECEDING) AS Acum FROM #act) t ON t.Node = a.Node;
            SELECT e.MemberId, e.PersonalPoints,
                   ISNULL((SELECT TOP 1 Acum FROM #act WHERE e.Siguiente IS NULL OR Node < e.Siguiente ORDER BY Node DESC), 0)
                 - ISNULL((SELECT TOP 1 Acum FROM #act WHERE Node <= e.Node ORDER BY Node DESC), 0) AS TeamPoints
            FROM (SELECT g.MemberId, g.Node, g.Node.GetAncestor(1).GetDescendant(g.Node, NULL) AS Siguiente,
                         ISNULL((SELECT TOP 1 lp.Pts FROM MembershipSubscriptions s
                                 JOIN (VALUES {niveles}) lp(Nivel, Pts) ON lp.Nivel = s.MembershipLevelId
                                 WHERE s.MemberId = g.MemberId AND s.SubscriptionStatus = {(int)MembershipStatus.Active} AND s.IsDeleted = 0
                                 ORDER BY s.CreationDate DESC), 0) AS PersonalPoints
                  FROM GenealogyTree g
                  WHERE g.IsDeleted = 0 AND g.Node IS NOT NULL AND g.MemberId IN (SELECT MemberId FROM #amb)) e;
            """).ToListAsync(ct);
```

- **Pendientes de la implementación:**
  - `{niveles}` no puede ir como parámetro: se construye con `SqlQueryRaw` a partir de enteros de una constante, así que no hay riesgo de inyección. Hay que anotarlo en un comentario.
  - `#amb` se carga antes con los embajadores de `:69-75`, con un `INSERT … SELECT` en la misma conexión abierta.
  - El nodo vigente debe ser el mismo que el de `CurrentUnder` (R8 del Plan J). Hay que copiar su criterio exacto de `GenealogySubtree.cs:78` en lugar del `ROW_NUMBER` de arriba si difiere.
  - La raíz tiene `GetAncestor(1)` nulo, así que `Siguiente` queda nulo y la cota superior se omite.
- Coste: O(N log N) en SQL y dos *seeks* por embajador, sin listas en memoria.

- [ ] **Paso 3.** El resto del manejador (requisitos, importe y alta de `CommissionEarning`) itera sobre `filas`. En verde. Commit: `Plan P: el bono de coche suma los equipos en SQL con acumulados por Node`.

---

## Cierre

### Tarea 21: índice único de `PaymentHistories` (R7 del Plan M)

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/PaymentHistoryConfiguration.cs`
- Create: migración `PagoUnicoPorPasarela`
- Modify: `MLMConquerorGlobalEdition.SharedAPICenter/Features/ProcessPaymentWebhook/ProcessPaymentWebhookHandler.cs:56-113`

**Datos locales:**
- `GatewayName` es `nvarchar(max)` (la constante `PaymentHistory.GatewayNameMaxLength = 100` existe, pero no se aplica) y su longitud máxima real es 17.
- `GatewayTransactionId` es `nvarchar(200)` y su máximo real es 36.
- Hay 22.769 filas (22.764 `CommissionBalance` y 5 `Entrax`), **0 repetidas** por `(GatewayName, GatewayTransactionId)` y ninguna vacía.

- [ ] **Paso 1.** Prueba en rojo (`[SqlServerFact]`): dos webhooks iguales en paralelo dejan **un** pago, y el segundo devuelve el mismo resultado que el primero. Hoy, con RCSI, insertan los dos.

- [ ] **Paso 2.** Configuración:

```csharp
        builder.Property(p => p.GatewayName).HasMaxLength(PaymentHistory.GatewayNameMaxLength);
        builder.HasIndex(p => new { p.GatewayName, p.GatewayTransactionId })
            .IsUnique()
            .HasDatabaseName("UX_PaymentHistories_GatewayName_GatewayTransactionId")
            .HasFilter("[GatewayName] IS NOT NULL AND [GatewayTransactionId] IS NOT NULL AND [GatewayTransactionId] <> N''");
```

- [ ] **Paso 3.** Guarda al principio de `Up`:

```csharp
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [PaymentHistories] WHERE DATALENGTH([GatewayName]) / 2 > 100)
    THROW 51070, N'PaymentHistories.GatewayName tiene valores de mas de 100 caracteres (Plan P, Tarea 21).', 1;
IF EXISTS (SELECT 1 FROM [PaymentHistories]
           WHERE [GatewayName] IS NOT NULL AND [GatewayTransactionId] IS NOT NULL AND [GatewayTransactionId] <> N''
           GROUP BY [GatewayName], [GatewayTransactionId] HAVING COUNT(*) > 1)
    THROW 51071, N'PaymentHistories repite (GatewayName, GatewayTransactionId): decide Nick (Plan P, pregunta P5).', 1;");
```

- [ ] **Paso 4.** En el manejador, `catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } s && s.Message.Contains("UX_PaymentHistories_GatewayName_GatewayTransactionId"))`: se recarga el pago existente y se responde como duplicado idempotente, igual que la rama de la línea 61. En verde.

- [ ] **Paso 5.** Commit: `Plan P: indice unico de pago por pasarela y transaccion con guarda; el webhook trata el duplicado como idempotente`.

### Tarea 22: guion de despliegue y verificación final

**Files:**
- Create: `Scripts/sql/plan-p/00-despliegue-plan-p.md` (guion paso a paso)
- Modify: `docs/arquitectura/decisiones-rendimiento-pedidos-y-arboles.md`: **no se edita aquí**, porque lo mantiene el coordinador. Se le entrega una nota con D4 nuevo (clave compacta), D6 (R6 y R7 cerrados) y la corrección de que el candado de `PlacementService` no protegía el camino real.

- [ ] **Paso 1.** Verificación completa:
  - `dotnet build`;
  - `dotnet test` de todas las soluciones de pruebas;
  - las dos reglas nuevas en verde;
  - `SinPrefijoEnInscripcionTests` en verde;
  - consulta de §3 del diagnóstico: 0 huecos, 0 huérfanos, 0 rutas incoherentes, 0 nodos sin `BinaryPath`.
- [ ] **Paso 2.** Commit: `Plan P: guion de despliegue del arbol binario a escala`.

---

## Despliegue: ventana única, servicios parados

El orden importa: el índice único no se crea si quedan huecos, el relleno exige que no los haya, y el código nuevo exige que el relleno haya terminado.

1. **Parar los ocho servicios**, incluido Hangfire (`AutoPlacementJob`). Así nadie escribe en el binario y ningún `MigrateAsync` se adelanta.
2. **Copia de seguridad** de la base.
3. **Detección (solo lectura):** consulta de §3 del diagnóstico. En producción, **parar aquí** y llevar el resultado a Nick (P2 y P3).
4. `sqlcmd -b -I … 01-reparar-huecos-binario.sql -v SoloInforme=1`, revisar la profundidad resultante y después `SoloInforme=0`. En producción, solo con la decisión de Nick y unos cerrojos de base adaptados que Nick apruebe; los scripts de este plan rechazan cualquier base que no sea la local.
5. `02-recalcular-piernas.sql`.
6. Migraciones `HuecoUnicoDelBinario`, `ClaveCompactaDelBinario` y `PagoUnicoPorPasarela`, con `dotnet ef database update --connection …` o un script idempotente generado con `dotnet ef migrations script --idempotent` y ejecutado con `sqlcmd -b -I`. Las guardas 51048, 51070 y 51071 detienen el paso sin tocar nada.
7. `03-relleno-clave-binario.sql` y `04-colas-y-contadores.sql`.
8. **Verificación** (Tarea 22, Paso 1).
9. **Arrancar los servicios.**
10. **Con esto, R6 y R7 del Plan M quedan cerrados:** RCSI (`Scripts/sql/plan-m/03-activar-rcsi.sql`) se puede activar en producción en esta misma ventana o en otra, según decida Nick.

---

## Riesgos

| Id | Riesgo | Mitigación |
|---|---|---|
| P-R1 | **Profundidad.** La regla de la cadena más profunda hace crecer la profundidad con cada derrame (hoy 934 con 119.000 nodos). La clave admite 13.567 niveles. Con millones de miembros en una sola pierna puede agotarse. | `DualTreeTooDeepException` hace fallar la colocación de forma cerrada. Se registra un aviso a partir de 12.000 (`DualBinaryPath.WarnDepth`). El Bloque D mide los bytes de la clave a 5.000 niveles. A largo plazo, la decisión es de Nick (P4). |
| P-R2 | La reparación con la regla del motor puede dejar el árbol más profundo de lo previsto (la cota inferior es ≈ 1.481 niveles). **Medido en local: 8.223 niveles y 2.290 MB de rutas** (antes 934 y 308 MB). | El modo `SoloInforme` y la guarda 51044 (> 12.000). Alternativa en P1. |
| P-R3 | `PlaceBulkAsync` de uno en uno es más lento que en bloques de 1.000. | Se mide en el Bloque D. Cada colocación es O(1) con la caché más una subida de O(profundidad). Si no basta, se paraleliza por patrocinador, porque el candado por hueco lo permite. |
| P-R4 | La clave de `UX_DualTeamTree_ParentMemberId_Side` se declara de 1.804 bytes (`nvarchar(900)`). | Los ids reales tienen menos de 20 caracteres. Se acepta el aviso de SQL Server. Acotar `MemberId`/`ParentMemberId` a `nvarchar(100)` queda para otro plan. |
| P-R5 | `RekeySubtreeAsync` rellena cualquier fila con `BinaryPath` nulo. | Solo se despliega después del relleno (ventana única). Queda anotado en el código. |
| P-R6 | Un movimiento de subárbol toma el candado exclusivo del árbol y detiene todas las colocaciones mientras dura. | Los movimientos son raros y manuales. Se mide el de un subárbol de 50.000 nodos en el Bloque D y se anota. |
| P-R7 | La paginación de Mi equipo pasa a ordenarse por `BinaryPath` (inorden). Otros órdenes, como la fecha de alta, con 400.000 nodos obligan a ordenar el rango entero. | El orden por defecto pasa a ser la clave. Los demás quedan disponibles con un aviso de lentitud, o se limitan a equipos de menos de 20.000 (P7). |
| P-R8 | Huérfanos por adyacencia tras una retirada (hoy 0). | El relleno los trata como raíces flotantes con ordinal propio. Las retiradas los reescriben (Tarea 11). |

## Preguntas para Nick

- **P1. Regla de la reparación.** ¿La del motor (hoja más profunda del subárbol del que se queda, es la propuesta) o la menos profunda de la pierna (mantiene el árbol bajo)? ¿Se queda el hueco el más antiguo por `(CreationDate, Id)`?
- **P2. Producción.** ¿Se puede ejecutar allí la consulta de detección (solo lectura) para saber si `AutoPlacementJob` (`PlaceBulkAsync`, desde `5d12257`) ya ha creado huecos?
- **P3. Pagos hechos sobre un árbol mal formado.** Si producción tiene huecos, hay residuales diarios (`DailyResidualEarnings`), rangos y el bono presidencial calculados con `DualTeamPoints` inflados o arbitrarios. ¿Se recalculan y regularizan, o la reparación vale solo de aquí en adelante? ¿Se registra la diferencia de puntos por miembro?
- **P4. Profundidad a largo plazo.** Con la cadena más profunda, la profundidad crece sin límite. ¿Se acepta el tope de 13.567 niveles con fallo cerrado, o se revisa la regla de derrame cuando se acerque (por ejemplo, derrame por anchura a partir de cierta profundidad)?
- **P5. `PaymentHistories`.** ¿Hay algún flujo que repita legítimamente `GatewayTransactionId` en la misma pasarela, por ejemplo un reembolso con el id del cargo? Si lo hay, el índice debe incluir el tipo de movimiento.
- **P6. `DevSeedController`.** ¿Debe exigir `SuperAdmin` también en Development, salvo `seed-superadmin`, que es el arranque? Hoy es anónimo en Development.
- **P7. Orden de Mi equipo con equipos enormes.** ¿Vale el orden por estructura (inorden) por defecto, con los demás órdenes limitados?
- **P8. Base local.** En local, los intrusos son casi todos sintéticos (`lt.…@example.com`). ¿Se repara (propuesta: sirve de ensayo para producción) o se regenera la base?
