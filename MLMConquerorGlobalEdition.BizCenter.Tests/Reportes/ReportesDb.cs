using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Base y siembra compartidas por las pruebas de reportes del Plan J. Los métodos de siembra solo
/// añaden al contexto (no guardan): valen para EF InMemory y, dentro de una transacción que se
/// deshace, para SQL Server.
/// </summary>
public static class ReportesDb
{
    /// <summary>Instante de referencia de las pruebas (hora de servidor, sin Kind).</summary>
    public static readonly DateTime Base = new(2026, 3, 15, 10, 0, 0);

    public static AppDbContext Nueva() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    /// <summary>"/A/B/C/": la forma de las rutas de los dos árboles (barra inicial y final).</summary>
    public static string Ruta(params string[] ids) => "/" + string.Join("/", ids) + "/";

    /// <summary>Los cuatro niveles reales (ids 1 a 4). Solo para InMemory: en SQL Server ya existen.</summary>
    public static void Niveles(AppDbContext db) => db.MembershipLevels.AddRange(
        new MembershipLevel { Id = 1, Name = "Lifestyle Ambassador",     SortOrder = 1, CreatedBy = "prueba" },
        new MembershipLevel { Id = 2, Name = "Travel Advantage - VIP",   SortOrder = 2, CreatedBy = "prueba" },
        new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" },
        new MembershipLevel { Id = 4, Name = "Travel Advantage - Turbo", SortOrder = 4, CreatedBy = "prueba" });

    public static MemberProfile Miembro(
        AppDbContext db, string memberId, string? patrocinador = null,
        MemberType tipo = MemberType.Ambassador, DateTime? alta = null,
        MemberAccountStatus estado = MemberAccountStatus.Active, string? slug = null,
        string? email = null, string? telefono = null)
    {
        var perfil = new MemberProfile
        {
            UserId            = Guid.NewGuid(),
            MemberId          = memberId,
            Email             = email ?? $"{memberId.ToLowerInvariant()}@prueba.test",
            FirstName         = "Nombre",
            LastName          = memberId,
            Phone             = telefono,
            Country           = "US",
            MemberType        = tipo,
            Status            = estado,
            EnrollDate        = alta ?? Base,
            SponsorMemberId   = patrocinador,
            ReplicateSiteSlug = slug,
            CreatedBy         = "prueba"
        };
        db.MemberProfiles.Add(perfil);
        return perfil;
    }

    public static GenealogyEntity Nodo(AppDbContext db, string memberId, string? padre, string ruta, int nivel)
    {
        var nodo = new GenealogyEntity
        {
            MemberId       = memberId,
            ParentMemberId = padre,
            HierarchyPath  = ruta,
            Level          = nivel,
            CreatedBy      = "prueba"
        };
        db.GenealogyTree.Add(nodo);
        return nodo;
    }

    public static DualTeamEntity NodoBinario(AppDbContext db, string memberId, string? padre, string ruta, TreeSide lado)
    {
        var nodo = new DualTeamEntity
        {
            MemberId       = memberId,
            ParentMemberId = padre,
            HierarchyPath  = ruta,
            Side           = lado,
            CreatedBy      = "prueba"
        };
        db.DualTeamTree.Add(nodo);
        return nodo;
    }

    public static MembershipSubscription Suscripcion(
        AppDbContext db, string memberId, int nivel, MembershipStatus estado, DateTime inicio)
    {
        var suscripcion = new MembershipSubscription
        {
            MemberId           = memberId,
            MembershipLevelId  = nivel,
            SubscriptionStatus = estado,
            StartDate          = inicio,
            CreatedBy          = "prueba"
        };
        db.MembershipSubscriptions.Add(suscripcion);
        return suscripcion;
    }

    public static MemberStatisticEntity Puntos(AppDbContext db, string memberId, int inscripcion)
    {
        var estadistica = new MemberStatisticEntity
        {
            MemberId         = memberId,
            EnrollmentPoints = inscripcion,
            CreatedBy        = "prueba"
        };
        db.MemberStatistics.Add(estadistica);
        return estadistica;
    }

    /// <summary>
    /// Sube desde el directorio de salida hasta la carpeta del .slnx, para leer archivos del
    /// repositorio que no se copian a la salida (appsettings, .resx, código fuente).
    /// </summary>
    public static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null &&
               !File.Exists(Path.Combine(directorio.FullName, "MLMConquerorGlobalEdition.slnx")))
        {
            directorio = directorio.Parent;
        }

        directorio.Should().NotBeNull("la prueba tiene que poder localizar la raíz del repositorio");
        return directorio!.FullName;
    }
}
