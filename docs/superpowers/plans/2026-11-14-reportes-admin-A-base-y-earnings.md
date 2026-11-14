# Reportes del Admin — Plan A: base y reportes de Earnings

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dejar la base de la sección de Reportes del admin (indicador de cuenta corporativa, permiso de exportación, grupo del menú) y entregar los reportes Earnings Lifetime y Earnings 180 Days con exportación a CSV.

**Architecture:** Un servicio de solo lectura en Repository (`EarningsRankingService`) calcula el ranking completo de comisiones por miembro para una fecha de corte y una ventana, lo cachea en memoria 10 minutos y lo pagina/filtra en memoria. El AdminAPI lo expone con una rejilla de servidor (`POST .../grid`, el mismo `ServerGridAdaptor` del resto del admin) y un `GET .../export` que devuelve un CSV. AdminWeb tiene una página con dos rutas (lifetime y por ventana).

**Tech Stack:** .NET 10, EF Core 10 (SQL Server), ASP.NET Core Web API, Blazor Server + Syncfusion Grid, xUnit + FluentAssertions + Moq + EF InMemory.

**Especificación:** `docs/superpowers/specs/2026-11-14-reportes-admin-design.md` (secciones Principios, Reporte 2, Reporte 3).

**Planes siguientes (fuera de este):** B = Lifetime/Current Rank y Top Countries con su foto diaria; C = Club Recognition.

---

## Reglas del repo que este plan respeta

- Hora del servidor (`IDateTimeProvider.Now`), nunca `UtcNow`, para cortes de fecha.
- Toda migración nueva obliga a regenerar `DeploymentScripts/migrate-rds.sql` (lo comprueba `BizCenter.Tests/Migraciones/ScriptDeDespliegueTests.cs`).
- Commits sin coautoría ni menciones de herramientas: solo la descripción de lo que se hizo.
- Componentes y servicios se escriben una vez; lo específico se inyecta.

## Mapa de archivos

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberProfile.cs` | Modificar | propiedad `IsCorporateAccount` |
| `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs` | Modificar | valor por defecto `false` |
| `MLMConquerorGlobalEdition.Repository/Migrations/<ts>_CuentaCorporativa.cs` | Crear (EF) | columna `IsCorporateAccount bit NOT NULL DEFAULT 0` |
| `DeploymentScripts/migrate-rds.sql` | Regenerar | script idempotente |
| `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberCorporateAccountController.cs` | Crear | `PUT api/v1/admin/members/{memberId}/corporate-account` con auditoría |
| `MLMConquerorGlobalEdition.AdminAPI/DTOs/Members/AdminMemberDetailDto.cs` | Modificar | expone `IsCorporateAccount` |
| `MLMConquerorGlobalEdition.AdminAPI/Features/Members/GetMember/GetMemberHandler.cs` | Modificar | mapea `IsCorporateAccount` |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminMemberProfile.razor` | Modificar | interruptor «Corporate account» en la franja de acciones |
| `MLMConquerorGlobalEdition.SharedComponents/Constants/Permissions.cs` | Modificar | `Permissions.Reports.Export` |
| `MLMConquerorGlobalEdition.SharedComponents/Services/ViewContextService.cs` | Modificar | `Reports.Export` solo SuperAdmin y Admin |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsRankingService.cs` | Crear | ranking, página, exportación |
| `MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsRankingViews.cs` | Crear | `EarningsRankRowView` |
| `MLMConquerorGlobalEdition.AdminAPI/Reports/CsvReportWriter.cs` | Crear | genera CSV |
| `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminReportsController.cs` | Crear | `POST reports/earnings/grid`, `GET reports/earnings/export` |
| `MLMConquerorGlobalEdition.AdminAPI/Program.cs` | Modificar | registro del servicio y del writer |
| `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminEarningsReport.razor` | Crear | página con dos rutas |
| `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor` | Modificar | grupo «Reports» |
| `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/*.cs` | Crear | pruebas del servicio, writer y controladores |
| `MLMConquerorGlobalEdition.BizCenter.Tests/ViewContextServiceTests.cs` | Modificar | prueba del permiso |

---

### Task 1: Indicador de cuenta corporativa en el modelo y migración

**Files:**
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Member/MemberProfile.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs`
- Create (EF): `MLMConquerorGlobalEdition.Repository/Migrations/<timestamp>_CuentaCorporativa.cs`
- Regenerate: `DeploymentScripts/migrate-rds.sql`

- [ ] **Step 1: Añadir la propiedad**

En `MemberProfile.cs`, justo después de `public string? ReplicateSiteSlug { get; set; }`:

```csharp
    /// <summary>
    /// Cuenta de la empresa (p. ej. «MWR Corp.»). La activa el admin en el perfil; los reportes que no deben mostrar
    /// cuentas corporativas (rankings, rangos, Club Recognition) la excluyen.
    /// </summary>
    public bool IsCorporateAccount { get; set; }
```

- [ ] **Step 2: Valor por defecto en la configuración**

En `MemberProfileConfiguration.cs`, después de `builder.HasIndex(x => x.ReplicateSiteSlug)...;`:

```csharp
        builder.Property(x => x.IsCorporateAccount).HasDefaultValue(false);
```

- [ ] **Step 3: Crear la migración**

Run: `dotnet ef migrations add CuentaCorporativa --project MLMConquerorGlobalEdition.Repository`
Expected: se crea `Migrations/<timestamp>_CuentaCorporativa.cs` con un único `AddColumn<bool>(name: "IsCorporateAccount", table: "MemberProfiles", ..., defaultValue: false)`. Si trae otros cambios, detenerse: el modelo tenía deriva y hay que revisarla antes de seguir.

- [ ] **Step 4: Regenerar el script de despliegue**

Run: `dotnet ef migrations script --idempotent --project MLMConquerorGlobalEdition.Repository -o DeploymentScripts/migrate-rds.sql`
Expected: el diff de `migrate-rds.sql` solo añade el bloque de `CuentaCorporativa`.

- [ ] **Step 5: Aplicar a la base local y correr la prueba del script**

Run: `dotnet ef database update --project MLMConquerorGlobalEdition.Repository`
Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~Migraciones"`
Expected: PASS (las pruebas `[DesdeCeroFact]` se saltan si no hay SQL Server; si corren, deben pasar).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/Entities/Member/MemberProfile.cs MLMConquerorGlobalEdition.Repository/Configurations/MemberProfileConfiguration.cs MLMConquerorGlobalEdition.Repository/Migrations DeploymentScripts/migrate-rds.sql
git commit -m "Reportes: indicador de cuenta corporativa en el perfil del miembro (columna con valor por defecto 0 y script de despliegue regenerado)"
```

---

### Task 2: Endpoint del admin para marcar la cuenta corporativa

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminMemberCorporateAccountController.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/DTOs/Members/AdminMemberDetailDto.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Features/Members/GetMember/GetMemberHandler.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/CuentaCorporativaTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>El admin marca o desmarca la cuenta corporativa; el cambio queda en GeneralAuditTracking.</summary>
public class CuentaCorporativaTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 0, 0);

    private static AppDbContext Base()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = "AMB-1", FirstName = "MWR", LastName = "Corp.",
            Email = "info@example.com", Country = "US", CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.SaveChanges();
        return db;
    }

    private static AdminMemberCorporateAccountController Controlador(AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        var usuario = new Mock<ICurrentUserService>();
        usuario.SetupGet(u => u.UserId).Returns("admin-1");
        return new AdminMemberCorporateAccountController(db, reloj.Object, usuario.Object);
    }

    [Fact]
    public async Task Marcar_guarda_el_indicador_y_audita_el_cambio()
    {
        using var db = Base();

        var r = await Controlador(db).Set("AMB-1", new AdminMemberCorporateAccountController.SetRequest(true), default);

        r.Should().BeOfType<OkObjectResult>();
        (await db.MemberProfiles.SingleAsync()).IsCorporateAccount.Should().BeTrue();
        var audit = await db.GeneralAuditTrackings.SingleAsync();
        audit.EntityName.Should().Be("MemberProfile");
        audit.EntityId.Should().Be("AMB-1");
        audit.Action.Should().Be("CorporateAccountChanged");
        audit.OldValues.Should().Be("{\"IsCorporateAccount\":false}");
        audit.NewValues.Should().Be("{\"IsCorporateAccount\":true}");
        audit.ChangedBy.Should().Be("admin-1");
        audit.ChangedAt.Should().Be(Ahora);
    }

    [Fact]
    public async Task Sin_cambio_no_audita()
    {
        using var db = Base();

        await Controlador(db).Set("AMB-1", new AdminMemberCorporateAccountController.SetRequest(false), default);

        (await db.GeneralAuditTrackings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Miembro_inexistente_da_404()
    {
        using var db = Base();

        var r = await Controlador(db).Set("AMB-9", new AdminMemberCorporateAccountController.SetRequest(true), default);

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Solo_SuperAdmin_y_Admin()
    {
        typeof(AdminMemberCorporateAccountController).GetCustomAttribute<AuthorizeAttribute>()!
            .Roles.Should().Be("SuperAdmin,Admin");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~CuentaCorporativaTests"`
Expected: FAIL de compilación («AdminMemberCorporateAccountController no existe»). Si `db.GeneralAuditTrackings` no compila, buscar el nombre del `DbSet` en `Repository/Context/AppDbContext.cs` (línea ~44) y usar ese en la prueba y en el controlador.

- [ ] **Step 3: Implementar el controlador**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// PUT api/v1/admin/members/{memberId}/corporate-account — marca o desmarca la cuenta corporativa. Los reportes que no
/// deben mostrar cuentas de la empresa las excluyen por este indicador. Cada cambio deja una fila en GeneralAuditTracking.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/corporate-account")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberCorporateAccountController : ControllerBase
{
    private readonly AppDbContext        _db;
    private readonly IDateTimeProvider   _clock;
    private readonly ICurrentUserService _currentUser;

    public AdminMemberCorporateAccountController(AppDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser)
    {
        _db          = db;
        _clock       = clock;
        _currentUser = currentUser;
    }

    public sealed record SetRequest(bool IsCorporateAccount);

    [HttpPut]
    public async Task<IActionResult> Set(string memberId, [FromBody] SetRequest request, CancellationToken ct)
    {
        var member = await _db.MemberProfiles.FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
        if (member is null)
            return NotFound(ApiResponse<object>.Fail("MEMBER_NOT_FOUND", "Member not found."));

        if (member.IsCorporateAccount != request.IsCorporateAccount)
        {
            var now = _clock.Now;
            _db.GeneralAuditTrackings.Add(new GeneralAuditTracking
            {
                EntityName = "MemberProfile",
                EntityId   = memberId,
                Action     = "CorporateAccountChanged",
                OldValues  = $"{{\"IsCorporateAccount\":{member.IsCorporateAccount.ToString().ToLowerInvariant()}}}",
                NewValues  = $"{{\"IsCorporateAccount\":{request.IsCorporateAccount.ToString().ToLowerInvariant()}}}",
                ChangedBy  = _currentUser.UserId,
                ChangedAt  = now
            });
            member.IsCorporateAccount = request.IsCorporateAccount;
            member.LastUpdateDate     = now;
            member.LastUpdateBy       = _currentUser.UserId;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(ApiResponse<object>.Ok(new { member.MemberId, member.IsCorporateAccount }));
    }
}
```

Si el `using` de `IDateTimeProvider` choca con `AdminAPI.Services`, usar el alias que usan los otros controladores: `using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;`.

- [ ] **Step 4: Exponer el indicador en el detalle del miembro**

En `AdminMemberDetailDto.cs`, después de `public int? LifetimeRankId { get; set; }`:

```csharp
    public bool IsCorporateAccount { get; set; }
```

En `GetMemberHandler.cs`, dentro de `new AdminMemberDetailDto { ... }`, después de `LastName = member.LastName,`:

```csharp
            IsCorporateAccount = member.IsCorporateAccount,
```

- [ ] **Step 5: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~CuentaCorporativaTests"`
Expected: PASS (4).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/CuentaCorporativaTests.cs
git commit -m "Admin: endpoint para marcar la cuenta corporativa con auditoria en GeneralAuditTracking; el detalle del miembro la expone"
```

---

### Task 3: Interruptor «Corporate account» en el perfil del admin

**Files:**
- Modify: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminMemberProfile.razor`

- [ ] **Step 1: Añadir el campo al record del detalle**

En el `private record MemberDetailDto(...)` (cerca de la línea 1138), cambiar la última línea `string? LifetimeRank, int? LifetimeRankId);` por:

```csharp
        string? LifetimeRank, int? LifetimeRankId,
        bool IsCorporateAccount = false);
```

- [ ] **Step 2: Mostrar el interruptor en la franja de acciones**

En la franja de acciones (el `<div class="d-flex align-items-center gap-2 flex-wrap">` que contiene Binary Tree / Genealogy / Impersonate), como primer hijo:

```razor
                <AuthorizeView Roles="SuperAdmin,Admin">
                    <Authorized>
                        <div class="form-check form-switch mb-0 me-2" title="Corporate accounts are excluded from rankings and recognition reports">
                            <input class="form-check-input" type="checkbox" id="corporate-account"
                                   checked="@_member.IsCorporateAccount" disabled="@_savingCorporate"
                                   @onchange="ToggleCorporateAsync" />
                            <label class="form-check-label small" for="corporate-account">Corporate account</label>
                        </div>
                    </Authorized>
                </AuthorizeView>
```

- [ ] **Step 3: Código del interruptor**

En el bloque `@code`, junto a `private MemberDetailDto? _member;`:

```csharp
    private bool _savingCorporate;

    private async Task ToggleCorporateAsync(ChangeEventArgs e)
    {
        if (_member is null) return;
        var value = e.Value is bool b && b;
        _savingCorporate = true;
        try
        {
            var resp = await Http.PutAsJsonAsync($"api/v1/admin/members/{MemberId}/corporate-account",
                new { isCorporateAccount = value });
            if (resp.IsSuccessStatusCode)
                _member = _member with { IsCorporateAccount = value };
        }
        finally { _savingCorporate = false; }
    }
```

- [ ] **Step 4: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminMemberProfile.razor
git commit -m "Admin: interruptor de cuenta corporativa en la franja de acciones del perfil del miembro (SuperAdmin y Admin)"
```

---

### Task 4: Permiso de exportación de reportes

**Files:**
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Constants/Permissions.cs`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Services/ViewContextService.cs:139-152`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/ViewContextServiceTests.cs`

- [ ] **Step 1: Pruebas que fallan**

Al final de la clase `ViewContextServiceTests`:

```csharp
    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.IT, false)]
    [InlineData(AppRoles.CommissionManager, false)]
    [InlineData(AppRoles.BillingManager, false)]
    public void Exportar_reportes_es_solo_de_SuperAdmin_y_Admin(string rol, bool puede)
    {
        var svc = CreateService();
        svc.SetContext("mem-1", "usr-1", false, true, [rol]);

        svc.HasPermission(Permissions.Reports.Export).Should().Be(puede);
    }
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~Exportar_reportes"`
Expected: FAIL de compilación («Permissions.Reports no existe»).

- [ ] **Step 3: Constante**

En `Permissions.cs`, después de la clase `SystemUsers`:

```csharp
    public static class Reports
    {
        /// <summary>Exportar cualquier reporte a CSV. Solo SuperAdmin y Admin (no IT, aunque IT vea todo lo demás).</summary>
        public const string Export = "reports.export";
    }
```

- [ ] **Step 4: Regla antes del atajo**

En `ViewContextService.HasPermission`, justo después del `if (permission == Permissions.SystemUsers.Manage) ...;`:

```csharp
        // Exportar reportes: solo SuperAdmin y Admin, evaluado antes del atajo que da todo a IT.
        if (permission == Permissions.Reports.Export)
            return _viewerRoles.Contains(AppRoles.SuperAdmin) || _viewerRoles.Contains(AppRoles.Admin);
```

- [ ] **Step 5: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "FullyQualifiedName~ViewContextServiceTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Constants/Permissions.cs MLMConquerorGlobalEdition.SharedComponents/Services/ViewContextService.cs MLMConquerorGlobalEdition.BizCenter.Tests/ViewContextServiceTests.cs
git commit -m "Reportes: permiso de exportacion solo para SuperAdmin y Admin, evaluado antes del atajo que da todo a IT"
```

---

### Task 5: Servicio del ranking de comisiones

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsRankingViews.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Reports/EarningsRankingService.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/EarningsRankingServiceTests.cs`

Reglas (especificación, Reporte 2 y 3):
- Suma `CommissionEarning.Amount` con `Status` Paid o Pending; las Cancelled no cuentan.
- `EarnedDate` en `[corte − (días − 1), corte]` por día completo; sin días = desde siempre hasta el corte.
- Excluye `IsCorporateAccount`. Solo totales > 0.
- Orden: Total desc y, a igualdad, MemberId asc. Rank = posición en ese orden sobre TODOS (no cambia al buscar).
- Búsqueda: nombre completo o email, sin distinguir mayúsculas.
- `days` válido: `null`, 30, 90, 180, 365; otro valor → 180.

- [ ] **Step 1: Vistas**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Una fila del ranking de comisiones (Earnings Lifetime / Earnings por ventana).</summary>
public sealed class EarningsRankRowView
{
    public int     Rank       { get; init; }
    public string  MemberId   { get; init; } = string.Empty;
    public string? Username   { get; init; }
    public string  MemberName { get; init; } = string.Empty;
    public string  Email      { get; init; } = string.Empty;
    public string  Country    { get; init; } = string.Empty;
    public decimal Total      { get; init; }
}
```

- [ ] **Step 2: Pruebas que fallan**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class EarningsRankingServiceTests
{
    private static readonly DateTime Corte = new(2026, 11, 13);

    private static AppDbContext Base()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Miembro(db, "AMB-1", "Ana", "Lopez", "ana@example.com", "Bulgaria", "ana");
        Miembro(db, "AMB-2", "Beto", "Diaz", "beto@example.com", "France", "beto");
        Miembro(db, "AMB-3", "MWR", "Corp.", "info@example.com", "United States", "corp", corporativa: true);
        Miembro(db, "AMB-4", "Carla", "Ruiz", "carla@example.com", "Japan", null);
        return db;
    }

    private static void Miembro(AppDbContext db, string id, string nombre, string apellido, string email, string pais,
        string? slug, bool corporativa = false) =>
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = id, FirstName = nombre, LastName = apellido, Email = email,
            Country = pais, ReplicateSiteSlug = slug, IsCorporateAccount = corporativa,
            CreatedBy = "seed", CreationDate = Corte, LastUpdateDate = Corte
        });

    private static void Comision(AppDbContext db, string beneficiario, decimal monto, DateTime fecha,
        CommissionEarningStatus estado = CommissionEarningStatus.Paid) =>
        db.CommissionEarnings.Add(new CommissionEarning
        {
            Id = Guid.NewGuid().ToString(), BeneficiaryMemberId = beneficiario, Amount = monto, EarnedDate = fecha,
            PaymentDate = fecha, Status = estado, CommissionTypeId = 1,
            CreatedBy = "seed", CreationDate = fecha, LastUpdateDate = fecha
        });

    private static EarningsRankingService Servicio(AppDbContext db) =>
        new(db, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task Suma_pagadas_y_pendientes_sin_canceladas_ni_cuentas_corporativas()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte.AddDays(-1));
        Comision(db, "AMB-1", 50, Corte.AddDays(-2), CommissionEarningStatus.Pending);
        Comision(db, "AMB-1", 999, Corte.AddDays(-3), CommissionEarningStatus.Cancelled);
        Comision(db, "AMB-2", 300, Corte.AddDays(-5));
        Comision(db, "AMB-3", 5000, Corte.AddDays(-5));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest { Page = 1, PageSize = 20 });

        r.TotalCount.Should().Be(2);
        r.Items.Select(i => (i.Rank, i.MemberId, i.Total)).Should().Equal((1, "AMB-2", 300m), (2, "AMB-1", 150m));
    }

    [Fact]
    public async Task El_corte_incluye_todo_su_dia_y_excluye_lo_posterior()
    {
        using var db = Base();
        Comision(db, "AMB-1", 10, Corte.AddHours(23));
        Comision(db, "AMB-1", 70, Corte.AddDays(1));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest());

        r.Items.Single().Total.Should().Be(10);
    }

    [Fact]
    public async Task La_ventana_cuenta_los_ultimos_N_dias_hasta_el_corte()
    {
        using var db = Base();
        Comision(db, "AMB-1", 10, Corte.AddDays(-179));
        Comision(db, "AMB-1", 70, Corte.AddDays(-180));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, 180, new GridDataRequest());

        r.Items.Single().Total.Should().Be(10);
    }

    [Fact]
    public async Task Una_ventana_no_permitida_se_trata_como_180()
    {
        using var db = Base();
        Comision(db, "AMB-1", 10, Corte.AddDays(-100));
        Comision(db, "AMB-1", 70, Corte.AddDays(-200));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, 7, new GridDataRequest());

        r.Items.Single().Total.Should().Be(10);
    }

    [Fact]
    public async Task Buscar_filtra_por_nombre_o_email_y_conserva_el_rank_global()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte);
        Comision(db, "AMB-2", 300, Corte);
        Comision(db, "AMB-4", 200, Corte);
        await db.SaveChangesAsync();

        var porNombre = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest { Search = "carla ru" });
        var porEmail  = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest { Search = "ANA@EXAMPLE" });

        porNombre.Items.Single().Should().BeEquivalentTo(new { Rank = 2, MemberId = "AMB-4", Username = (string?)null,
            MemberName = "Carla Ruiz", Email = "carla@example.com", Country = "Japan", Total = 200m });
        porEmail.Items.Single().Should().BeEquivalentTo(new { Rank = 3, MemberId = "AMB-1", Username = "ana" });
    }

    [Fact]
    public async Task Empate_de_total_se_ordena_por_MemberId()
    {
        using var db = Base();
        Comision(db, "AMB-2", 100, Corte);
        Comision(db, "AMB-1", 100, Corte);
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest());

        r.Items.Select(i => i.MemberId).Should().Equal("AMB-1", "AMB-2");
    }

    [Fact]
    public async Task Pagina_con_tamano_acotado()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte);
        Comision(db, "AMB-2", 300, Corte);
        Comision(db, "AMB-4", 200, Corte);
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest { Page = 2, PageSize = 2 });

        r.TotalCount.Should().Be(3);
        r.Items.Select(i => i.Rank).Should().Equal(3);
    }

    [Fact]
    public async Task Exportar_devuelve_todas_las_filas_filtradas()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte);
        Comision(db, "AMB-2", 300, Corte);
        await db.SaveChangesAsync();

        var filas = await Servicio(db).GetAllAsync(Corte, null, search: null);

        filas.Select(f => f.MemberId).Should().Equal("AMB-2", "AMB-1");
    }
}
```

Si `CommissionEarning` exige otras propiedades obligatorias en InMemory (p. ej. `CommissionOperationTypeId`), añadirlas en `Comision(...)` con un valor fijo; no cambian la regla.

- [ ] **Step 3: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~EarningsRankingServiceTests"`
Expected: FAIL de compilación («EarningsRankingService no existe»).

- [ ] **Step 4: Implementar el servicio**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IEarningsRankingService
{
    Task<PagedResult<EarningsRankRowView>> GetPageAsync(DateTime cutoff, int? windowDays, GridDataRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<EarningsRankRowView>> GetAllAsync(DateTime cutoff, int? windowDays, string? search,
        CancellationToken ct = default);
}

/// <summary>
/// Ranking de comisiones por miembro hasta una fecha de corte (Earnings Lifetime) o en una ventana de días (Earnings
/// 180 Days). Suma lo pagado y lo pendiente, sin canceladas ni cuentas corporativas. El ranking completo se calcula una
/// vez por (corte, ventana) y se guarda 10 minutos en memoria: buscar y paginar no lo recalculan y el Rank es siempre
/// la posición entre todos.
/// </summary>
public class EarningsRankingService : IEarningsRankingService
{
    public static readonly int[] AllowedWindows = { 30, 90, 180, 365 };
    public const int DefaultWindow = 180;
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public EarningsRankingService(AppDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    public static int? NormalizeWindow(int? days) =>
        days is null ? null : AllowedWindows.Contains(days.Value) ? days : DefaultWindow;

    public async Task<PagedResult<EarningsRankRowView>> GetPageAsync(DateTime cutoff, int? windowDays,
        GridDataRequest request, CancellationToken ct = default)
    {
        var rows = Filter(await RankingAsync(cutoff, windowDays, ct), request.Search);
        var (page, pageSize) = GridQueryableExtensions.ResolvePage(request);
        return new PagedResult<EarningsRankRowView>
        {
            Items      = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = rows.Count,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<IReadOnlyList<EarningsRankRowView>> GetAllAsync(DateTime cutoff, int? windowDays, string? search,
        CancellationToken ct = default) =>
        Filter(await RankingAsync(cutoff, windowDays, ct), search);

    private static List<EarningsRankRowView> Filter(IReadOnlyList<EarningsRankRowView> rows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return rows as List<EarningsRankRowView> ?? rows.ToList();
        var term = search.Trim();
        return rows.Where(r => r.MemberName.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || r.Email.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async Task<IReadOnlyList<EarningsRankRowView>> RankingAsync(DateTime cutoff, int? windowDays, CancellationToken ct)
    {
        var window = NormalizeWindow(windowDays);
        var key = $"reports:earnings:{cutoff:yyyyMMdd}:{window?.ToString() ?? "all"}";
        if (_cache.TryGetValue(key, out IReadOnlyList<EarningsRankRowView>? cached) && cached is not null)
            return cached;

        var to   = cutoff.Date.AddDays(1);                                  // el corte incluye su día entero
        var from = window is null ? (DateTime?)null : cutoff.Date.AddDays(1 - window.Value);

        var earnings = _db.CommissionEarnings.AsNoTracking()
            .Where(c => (c.Status == CommissionEarningStatus.Paid || c.Status == CommissionEarningStatus.Pending)
                     && c.EarnedDate < to);
        if (from is not null) earnings = earnings.Where(c => c.EarnedDate >= from);

        var totals = await (
            from t in earnings.GroupBy(c => c.BeneficiaryMemberId)
                              .Select(g => new { MemberId = g.Key, Total = g.Sum(c => c.Amount) })
            join m in _db.MemberProfiles.AsNoTracking() on t.MemberId equals m.MemberId
            where !m.IsCorporateAccount && t.Total > 0
            select new
            {
                t.MemberId, t.Total, m.ReplicateSiteSlug, m.FirstName, m.LastName, m.Email, m.Country
            }).ToListAsync(ct);

        var ranked = totals
            .OrderByDescending(t => t.Total).ThenBy(t => t.MemberId, StringComparer.Ordinal)
            .Select((t, i) => new EarningsRankRowView
            {
                Rank       = i + 1,
                MemberId   = t.MemberId,
                Username   = t.ReplicateSiteSlug,
                MemberName = $"{t.FirstName} {t.LastName}".Trim(),
                Email      = t.Email,
                Country    = t.Country,
                Total      = t.Total
            }).ToList();

        _cache.Set(key, (IReadOnlyList<EarningsRankRowView>)ranked, CacheFor);
        return ranked;
    }
}
```

- [ ] **Step 5: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~EarningsRankingServiceTests"`
Expected: PASS (8).

- [ ] **Step 6: Índice para la consulta**

En `Repository/Configurations/CommissionEarningConfiguration.cs`, después de los índices existentes:

```csharp
        // Reportes Earnings: suma por beneficiario hasta una fecha. Sin este índice, el agrupado recorre la tabla entera.
        builder.HasIndex(x => new { x.EarnedDate, x.BeneficiaryMemberId })
               .HasDatabaseName("IX_CommissionEarnings_EarnedDate_Beneficiary")
               .IncludeProperties(x => new { x.Amount, x.Status })
               .HasFilter("[IsDeleted] = 0");
```

Run: `dotnet ef migrations add IndiceDeGananciasPorFecha --project MLMConquerorGlobalEdition.Repository`
Run: `dotnet ef migrations script --idempotent --project MLMConquerorGlobalEdition.Repository -o DeploymentScripts/migrate-rds.sql`
Run: `dotnet ef database update --project MLMConquerorGlobalEdition.Repository`
Expected: la migración solo crea ese índice. Si `BizCenter.Tests/Reportes/IndicesDeReportesTests.cs` enumera los índices esperados, añadir este allí.

- [ ] **Step 7: Medir contra la base de escala**

Run (SSMS o sqlcmd contra `MLMConquerorGlobalEditionDb_Escala`, con `SET STATISTICS TIME, IO ON`), la consulta que EF genera para el ranking de por vida y para 180 días (copiarla del log de EF con `LogTo(Console.WriteLine)` en una prueba de consola o de la traza de SQL).
Expected: < 2 s cada una. Si no, anotar los tiempos en la sección «Medidas» del final de este plan y avisar a Nick antes de seguir: la alternativa es una foto diaria del acumulado por miembro.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/EarningsRankingServiceTests.cs DeploymentScripts/migrate-rds.sql
git commit -m "Reportes: servicio del ranking de comisiones (pagadas y pendientes, sin canceladas ni cuentas corporativas, de por vida o por ventana, rank global, busqueda y pagina) e indice por fecha y beneficiario"
```

---

### Task 6: Escritor de CSV

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Reports/CsvReportWriter.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/CsvReportWriterTests.cs`

Formato (decisión de Nick, 2026-11-14: CSV, no Excel):
- UTF-8 con BOM, para que una hoja de cálculo lea bien las tildes.
- Separador coma y fin de línea CRLF.
- Comillas dobles solo cuando el valor lleva coma, comilla o salto de línea, o empieza por `=`, `+`, `-` o `@`. En
  ese último caso se antepone además `'`, para que una hoja de cálculo no lo ejecute como fórmula.
- Números y fechas en cultura invariante.
- Sin paquetes nuevos.

- [ ] **Step 1: Prueba que falla**

```csharp
using System.Text;
using MLMConquerorGlobalEdition.AdminAPI.Reports;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class CsvReportWriterTests
{
    private sealed record Fila(int Rank, string? Name, decimal Total);

    private static string Texto(byte[] bytes)
    {
        bytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF }, "lleva BOM para que se lea como UTF-8");
        return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
    }

    [Fact]
    public void Escribe_encabezados_y_filas_con_numeros_invariantes()
    {
        var csv = Texto(new CsvReportWriter().Write(
            new[] { new Fila(1, "Ana", 1234.5m), new Fila(2, "Beto", 10m) },
            ("Rank", f => f.Rank), ("Member Name", f => f.Name), ("Total", f => f.Total)));

        csv.Should().Be("Rank,Member Name,Total\r\n1,Ana,1234.5\r\n2,Beto,10\r\n");
    }

    [Fact]
    public void Entrecomilla_comas_comillas_y_saltos()
    {
        var csv = Texto(new CsvReportWriter().Write(
            new[] { new Fila(1, "Lopez, \"Ana\"\nJr", 1m) },
            ("Name", f => f.Name)));

        csv.Should().Be("Name\r\n\"Lopez, \"\"Ana\"\"\nJr\"\r\n");
    }

    [Fact]
    public void Neutraliza_formulas()
    {
        var csv = Texto(new CsvReportWriter().Write(
            new[] { new Fila(1, "=HYPERLINK(\"x\")", 1m), new Fila(2, "@cmd", 1m) },
            ("Name", f => f.Name)));

        csv.Should().Be("Name\r\n\"'=HYPERLINK(\"\"x\"\")\"\r\n\"'@cmd\"\r\n");
    }

    [Fact]
    public void Nulos_salen_vacios()
    {
        var csv = Texto(new CsvReportWriter().Write(new[] { new Fila(1, null, 1m) }, ("Name", f => f.Name)));

        csv.Should().Be("Name\r\n\r\n");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~CsvReportWriterTests"`
Expected: FAIL de compilación («CsvReportWriter no existe»).

- [ ] **Step 3: Implementar**

```csharp
using System.Globalization;
using System.Text;

namespace MLMConquerorGlobalEdition.AdminAPI.Reports;

/// <summary>
/// Convierte filas de cualquier reporte del admin en CSV: UTF-8 con BOM, comas, CRLF y cultura invariante. Un texto que
/// empieza por =, +, - o @ se antepone con ' para que una hoja de cálculo no lo ejecute como fórmula; los números no.
/// </summary>
public class CsvReportWriter
{
    public const string ContentType = "text/csv";

    public byte[] Write<T>(IEnumerable<T> rows, params (string Header, Func<T, object?> Value)[] columns)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", columns.Select(c => Escape(c.Header, isNumber: false)))).Append("\r\n");
        foreach (var row in rows)
            sb.Append(string.Join(",", columns.Select(c => Cell(c.Value(row))))).Append("\r\n");

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Cell(object? value) => value switch
    {
        null           => string.Empty,
        DateTime d     => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => Escape(f.ToString(null, CultureInfo.InvariantCulture), isNumber: true),
        _              => Escape(value.ToString() ?? string.Empty, isNumber: false)
    };

    private static string Escape(string value, bool isNumber)
    {
        var formula = !isNumber && value.Length > 0 && value[0] is '=' or '+' or '-' or '@';
        if (formula) value = "'" + value;
        var quote = formula || value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        return quote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
```

- [ ] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~CsvReportWriterTests"`
Expected: PASS (4).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Reports MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/CsvReportWriterTests.cs
git commit -m "Reportes: escritor de CSV comun para los reportes del admin (UTF-8 con BOM, cultura invariante y formulas neutralizadas)"
```

---

### Task 7: Controlador de reportes del admin

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminReportsController.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs` (junto al registro de `ILoyaltyPointsService`, línea ~103)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminReportsControllerTests.cs`

Contrato:
- `POST api/v1/admin/reports/earnings/grid?cutoff=2026-11-13&days=180` con `GridDataRequest` en el cuerpo → `ApiResponse<PagedResult<EarningsRankRowView>>`. Sin `days` = de por vida. Sin `cutoff` = ayer (hora del servidor).
- `GET api/v1/admin/reports/earnings/export?cutoff=...&days=...&search=...` → CSV. Solo SuperAdmin y Admin.
- La clase entera: SuperAdmin y Admin (especificación: acceso a reportes).

- [ ] **Step 1: Pruebas que fallan**

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminReportsControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 30, 0);

    private static (AdminReportsController C, Mock<IEarningsRankingService> S) Montar()
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        var servicio = new Mock<IEarningsRankingService>();
        return (new AdminReportsController(servicio.Object, new CsvReportWriter(), reloj.Object), servicio);
    }

    [Fact]
    public async Task Sin_corte_usa_ayer_con_la_hora_del_servidor()
    {
        var (c, s) = Montar();
        var pedido = new GridDataRequest();
        s.Setup(x => x.GetPageAsync(new DateTime(2026, 11, 13), 180, pedido, It.IsAny<CancellationToken>()))
         .ReturnsAsync(new PagedResult<EarningsRankRowView>());

        var r = await c.EarningsGrid(pedido, cutoff: null, days: 180, default);

        r.Should().BeOfType<OkObjectResult>();
        s.VerifyAll();
    }

    [Fact]
    public async Task Exportar_devuelve_un_csv_con_las_filas_filtradas()
    {
        var (c, s) = Montar();
        s.Setup(x => x.GetAllAsync(new DateTime(2026, 10, 31), null, "ana", It.IsAny<CancellationToken>()))
         .ReturnsAsync(new[] { new EarningsRankRowView { Rank = 1, MemberId = "AMB-1", MemberName = "Ana", Total = 5 } });

        var r = await c.EarningsExport(new DateTime(2026, 10, 31), days: null, search: "ana", default);

        var archivo = r.Should().BeOfType<FileContentResult>().Subject;
        archivo.ContentType.Should().Be(CsvReportWriter.ContentType);
        archivo.FileDownloadName.Should().Be("earnings-lifetime-2026-10-31.csv");
        archivo.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public void Los_reportes_son_de_SuperAdmin_y_Admin_y_exportar_tambien()
    {
        typeof(AdminReportsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
        typeof(AdminReportsController).GetMethod(nameof(AdminReportsController.EarningsExport))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~AdminReportsControllerTests"`
Expected: FAIL de compilación.

- [ ] **Step 3: Implementar el controlador**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>Reportes del admin. Lectura para SuperAdmin y Admin; exportar a CSV también solo para ellos (no IT).</summary>
[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminReportsController : ControllerBase
{
    private readonly IEarningsRankingService _earnings;
    private readonly CsvReportWriter         _csv;
    private readonly IDateTimeProvider       _clock;

    public AdminReportsController(IEarningsRankingService earnings, CsvReportWriter csv, IDateTimeProvider clock)
    {
        _earnings = earnings;
        _csv      = csv;
        _clock    = clock;
    }

    private DateTime Cutoff(DateTime? cutoff) => (cutoff ?? _clock.Now.Date.AddDays(-1)).Date;

    /// <summary>POST reports/earnings/grid?cutoff=yyyy-MM-dd&amp;days=180 — sin days: de por vida.</summary>
    [HttpPost("earnings/grid")]
    public async Task<IActionResult> EarningsGrid([FromBody] GridDataRequest request,
        [FromQuery] DateTime? cutoff, [FromQuery] int? days, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<EarningsRankRowView>>.Ok(
            await _earnings.GetPageAsync(Cutoff(cutoff), days, request, ct)));

    /// <summary>GET reports/earnings/export?cutoff=&amp;days=&amp;search= — CSV con las filas filtradas.</summary>
    [HttpGet("earnings/export")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> EarningsExport([FromQuery] DateTime? cutoff, [FromQuery] int? days,
        [FromQuery] string? search, CancellationToken ct)
    {
        var date   = Cutoff(cutoff);
        var window = EarningsRankingService.NormalizeWindow(days);
        var rows   = await _earnings.GetAllAsync(date, days, search, ct);
        var bytes  = _csv.Write(rows,
            ("Rank", r => r.Rank), ("Username", r => r.Username), ("Member Name", r => r.MemberName),
            ("Email", r => r.Email), ("Country", r => r.Country), ("Total", r => r.Total));
        var file = window is null ? $"earnings-lifetime-{date:yyyy-MM-dd}.csv" : $"earnings-{window}d-{date:yyyy-MM-dd}.csv";
        return File(bytes, CsvReportWriter.ContentType, file);
    }
}
```

- [ ] **Step 4: Registro en Program.cs**

Junto al registro de `ILoyaltyPointsService`:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Reports.IEarningsRankingService,
                            MLMConquerorGlobalEdition.Repository.Services.Reports.EarningsRankingService>();
builder.Services.AddSingleton<MLMConquerorGlobalEdition.AdminAPI.Reports.CsvReportWriter>();
```

(`AddMemoryCache()` ya está registrado en la línea ~295.)

- [ ] **Step 5: Correr las pruebas y compilar**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "FullyQualifiedName~Reports"`
Expected: PASS.
Run: `dotnet build MLMConquerorGlobalEdition.AdminAPI`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI MLMConquerorGlobalEdition.AdminAPI.Tests/Reports/AdminReportsControllerTests.cs
git commit -m "Admin: endpoints del ranking de comisiones (rejilla de servidor y exportacion a CSV) con corte por defecto ayer en hora del servidor"
```

---

### Task 8: Página de Earnings en AdminWeb

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminEarningsReport.razor`

Una página, dos rutas: `/admin/reports/earnings-lifetime` (sin ventana) y `/admin/reports/earnings-window` (selector de ventana, 180 por defecto).

- [ ] **Step 1: Escribir la página**

```razor
@page "/admin/reports/earnings-lifetime"
@page "/admin/reports/earnings-window"
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

<PageTitle>@Title</PageTitle>

<h4 class="mb-1">@Title</h4>
<p class="text-muted mb-4">@Subtitle</p>

<div class="d-flex flex-wrap gap-2 align-items-end mb-3">
    <div class="flex-grow-1" style="min-width:240px">
        <input class="form-control" placeholder="Search name or email..." @bind="_search" @bind:event="oninput"
               @onkeyup="OnSearchKeyAsync" />
    </div>
    @if (IsWindow)
    {
        <div>
            <label class="form-label small mb-1">Window</label>
            <select class="form-select" @bind="_days" @bind:after="RefreshAsync">
                @foreach (var d in Windows) { <option value="@d">Last @d days</option> }
            </select>
        </div>
    }
    <div>
        <label class="form-label small mb-1">Cutoff date</label>
        <input type="date" class="form-control" @bind="_cutoff" @bind:after="RefreshAsync" />
    </div>
    @if (ViewContext.HasPermission(Permissions.Reports.Export))
    {
        <button class="btn btn-outline-success" @onclick="ExportAsync" disabled="@_exporting">
            <i class="ri-file-download-line me-1"></i>Export CSV
        </button>
    }
</div>

<SfGrid TValue="EarningsRow" @ref="_grid" AllowPaging="true">
    <SfDataManager Adaptor="Adaptors.CustomAdaptor">
        <ServerGridAdaptor TValue="EarningsRow" Http="Http" Endpoint="api/v1/admin/reports/earnings/grid"
                           QueryStringProvider="Query" />
    </SfDataManager>
    <GridPageSettings PageSize="20" PageSizes="@(new int[] { 20, 50, 100 })" />
    <GridColumns>
        <GridColumn Field="@nameof(EarningsRow.Rank)" HeaderText="Rank" Width="80">
            <Template>#@((context as EarningsRow)!.Rank)</Template>
        </GridColumn>
        <GridColumn Field="@nameof(EarningsRow.Username)" HeaderText="Username" />
        <GridColumn Field="@nameof(EarningsRow.MemberName)" HeaderText="Member Name" />
        <GridColumn Field="@nameof(EarningsRow.Email)" HeaderText="Email" />
        <GridColumn Field="@nameof(EarningsRow.Country)" HeaderText="Country" />
        <GridColumn Field="@nameof(EarningsRow.Total)" HeaderText="Total" Format="N2" TextAlign="TextAlign.Right" />
    </GridColumns>
</SfGrid>

@code {
    private static readonly int[] Windows = { 30, 90, 180, 365 };

    private SfGrid<EarningsRow>? _grid;
    private string   _search = string.Empty;
    private int      _days   = 180;
    private DateTime _cutoff = DateTime.Today.AddDays(-1);
    private bool     _exporting;

    private bool   IsWindow => Nav.Uri.Contains("/earnings-window", StringComparison.OrdinalIgnoreCase);
    private string Title    => IsWindow ? $"Earnings {_days} Days" : "Earnings Lifetime";
    private string Subtitle => IsWindow ? $"Commissions earned in the last {_days} days up to the cutoff date."
                                        : "Lifetime commissions earned up to the cutoff date.";

    private string Query()
    {
        var q = $"cutoff={_cutoff:yyyy-MM-dd}";
        return IsWindow ? $"{q}&days={_days}" : q;
    }

    private async Task RefreshAsync()
    {
        if (_grid is not null) await _grid.Refresh();
    }

    private async Task OnSearchKeyAsync(KeyboardEventArgs e)
    {
        if (_grid is not null) await _grid.SearchAsync(_search);
    }

    private async Task ExportAsync()
    {
        _exporting = true;
        try
        {
            var url = $"api/v1/admin/reports/earnings/export?{Query()}&search={Uri.EscapeDataString(_search)}";
            var bytes = await Http.GetByteArrayAsync(url);
            var file = IsWindow ? $"earnings-{_days}d-{_cutoff:yyyy-MM-dd}.csv" : $"earnings-lifetime-{_cutoff:yyyy-MM-dd}.csv";
            await Js.InvokeVoidAsync("mlmDownloadFile", file,
                "text/csv", Convert.ToBase64String(bytes));
        }
        finally { _exporting = false; }
    }

    private sealed class EarningsRow
    {
        public int     Rank       { get; set; }
        public string? Username   { get; set; }
        public string  MemberName { get; set; } = string.Empty;
        public string  Email      { get; set; } = string.Empty;
        public string  Country    { get; set; } = string.Empty;
        public decimal Total      { get; set; }
    }
}
```

`_cutoff = DateTime.Today.AddDays(-1)` es solo el valor inicial del control; el corte que vale es el que el servidor recibe. Comprobar en `ServerGridAdaptor.cs` que `SearchAsync` del grid llega como `GridDataRequest.Search`; si el adaptor no lo propaga, pasar la búsqueda por `QueryStringProvider` (`&search=`) y leerla en el controlador con `[FromQuery] string? search` copiándola a `request.Search`.

- [ ] **Step 2: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminEarningsReport.razor
git commit -m "Admin: pagina de Earnings (de por vida y por ventana) con busqueda, fecha de corte, rejilla de servidor y exportacion solo con permiso"
```

---

### Task 9: Grupo «Reports» en el menú del admin

**Files:**
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor`

- [ ] **Step 1: Añadir el grupo**

Justo antes de `<AuthorizeView Roles="SuperAdmin,Admin,CommissionManager">` (el grupo Commissions):

```razor
        <AuthorizeView Roles="SuperAdmin,Admin">
            <Authorized>
                @* ── Reports ── *@
                <li class="menu-item @(IsSection("/admin/reports") ? "open active" : "")">
                    <a href="javascript:void(0);" class="menu-link menu-toggle">
                        <i class="ri-file-chart-line menu-icon"></i>
                        <span class="title">Reports</span>
                    </a>
                    <ul class="menu-sub">
                        <li class="menu-item @IsActive("/admin/reports/earnings-window")">
                            <a href="/admin/reports/earnings-window" class="menu-link @IsActive("/admin/reports/earnings-window")">
                                <span class="title">Earnings 180 Days</span>
                            </a>
                        </li>
                        <li class="menu-item @IsActive("/admin/reports/earnings-lifetime")">
                            <a href="/admin/reports/earnings-lifetime" class="menu-link @IsActive("/admin/reports/earnings-lifetime")">
                                <span class="title">Earnings Lifetime</span>
                            </a>
                        </li>
                    </ul>
                </li>
            </Authorized>
        </AuthorizeView>
```

Los planes B y C añaden aquí Top Countries, Lifetime Rank y Current Rank, y el grupo Club Recognition.

- [ ] **Step 2: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor
git commit -m "Admin: grupo Reports en el menu con Earnings 180 Days y Earnings Lifetime"
```

---

### Task 10: Verificación final del plan A

- [ ] **Step 1: Suites completas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests`
Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests`
Expected: 0 fallos en las dos.

- [ ] **Step 2: Probar en el navegador (AdminWeb + AdminAPI levantados contra la base local)**

1. Entrar como SuperAdmin, abrir un perfil, activar «Corporate account» y recargar: sigue activado.
2. Abrir Reports → Earnings Lifetime: la cuenta marcada no aparece; buscar un nombre conserva su Rank.
3. Earnings 180 Days: cambiar la ventana a 30 y la fecha de corte; la rejilla se recarga.
4. Exportar: se descarga un CSV con las mismas filas filtradas.
5. Entrar con un usuario IT: ve los reportes solo si su rol está en el menú (no lo está: el grupo es SuperAdmin/Admin) y nunca el botón Export.

- [ ] **Step 3: Revisión de código de la rama** con `superpowers:requesting-code-review` y corregir lo que encuentre.

## Medidas

_(Task 5, Step 7: tiempos de la consulta del ranking contra la base de escala.)_

Medido el 14-nov-2026 con sqlcmd contra `MLMConquerorGlobalEditionDb_Escala` en `Server=.`, solo lectura. La consulta es la
que genera EF (sacada con `ToQueryString()`), ejecutada con `sp_executesql` como la manda EF, corte 2026-11-14.

Base de escala:
- `CommissionEarnings`: 1.939.875 filas, todas sin borrar, todas `Status = 1` (Pending); `EarnedDate` de 2024-10-21 a
  2026-10-22; 293.534 beneficiarios distintos. `MemberProfiles`: 802.003 filas.
- No tiene la migración `CuentaCorporativa` (no existe `IsCorporateAccount`): se midió sin ese predicado. Tampoco tiene el
  índice nuevo `IX_CommissionEarnings_EarnedDate_Beneficiary` (no se creó nada en esa base): los tiempos son SIN el índice.
  El plan lee `CommissionEarnings` entero (~185k lecturas lógicas) y `MemberProfiles` entero (~85k), en paralelo.

| Consulta | Filas | Servidor (resultado consumido en el servidor, 3 corridas) | Con envío de filas al cliente (sqlcmd, 2 corridas) |
|---|---|---|---|
| De por vida | 293.534 | 17,5 s / 5,8 s / 1,6 s (CPU 26 / 22 / 9 s) | 13,2 s / 8,3 s |
| 180 días (desde 2026-05-19) | 128.368 | 8,1 s / 2,5 s / 1,2 s (CPU 19 / 11 / 6 s) | 6,8 s / 18,4 s |
| Solo el agrupado de por vida (sin unir perfiles) | 293.534 | 7,7 s / 1,7 s / 1,6 s | — |

Lectura: con la caché caliente el servidor queda en 1,2–1,6 s, pero la variación entre corridas es grande (la máquina
estaba cargada con compilaciones a la vez) y el uso de CPU es alto (6–26 s de CPU en paralelo por consulta). Además, el
servicio trae al proceso las ~294k filas para ordenarlas y cachearlas, lo que suma el envío. En el peor caso medido no se
cumple «< 2 s»: hay que avisar a Nick antes de seguir. Alternativa si no basta: foto diaria del acumulado por miembro.

Con el índice nuevo, en la base local `MLMConquerorGlobalEditionDb` (972.578 comisiones de 2026-04-19 a 2026-11-11, 65.031
beneficiarios; ahí sí está aplicado), misma consulta de EF con `IsCorporateAccount`:
- Ojo: sqlcmd arranca con `QUOTED_IDENTIFIER OFF` y así SQL Server no puede usar índices filtrados; hay que medir con
  `sqlcmd -I` (EF/SqlClient lo pone en ON).
- El agrupado usa el índice nuevo: 16.711 lecturas lógicas contra 86.544 recorriendo la tabla (5 veces menos). Una ventana
  corta (desde 2026-09-01) baja a 2.122 lecturas.
- Tiempos (3 corridas, resultado consumido en el servidor): de por vida 1,0 / 4,8 / 1,5 s; 180 días 1,5 / 4,2 / 4,7 s.
  El coste que queda es CPU del agrupado hash por `BeneficiaryMemberId` nvarchar(450) (3–7 s de CPU en paralelo) y la
  máquina estaba ocupada (el CommissionEngine corriendo): varias corridas tienen más tiempo transcurrido que CPU.
