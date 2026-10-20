using MLMConquerorGlobalEdition.Domain.Services;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

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
            Node           = NodoDesdeRuta(db, ruta),
            CreatedBy      = "prueba"
        };
        db.GenealogyTree.Add(nodo);
        return nodo;
    }

    /// <summary>
    /// Plan M: rellena Node en los nodos seguidos que no lo tengan, a partir de su ruta, como hace el
    /// relleno (02-relleno-nodo-genealogia.sql). Para las pruebas que crean GenealogyEntity a mano.
    /// </summary>
    public static void RellenarNodos(AppDbContext db)
    {
        foreach (var g in db.GenealogyTree.Local.Where(g => g.Node is null).OrderBy(g => g.HierarchyPath.Length).ToList())
            g.Node = NodoDesdeRuta(db, g.HierarchyPath);
    }

    /// <summary>
    /// Nodos reservados para rutas cuyo nodo aún no existe: hay pruebas que crean un hijo antes que su
    /// padre, o cuelgan nodos de un patrocinador que no está en el árbol. El padre recibe aquí su Node
    /// y, si después se crea, usa ese mismo; así el hijo queda en su equipo.
    /// </summary>
    private static readonly ConditionalWeakTable<AppDbContext, Dictionary<string, HierarchyId>> Reservados = new();

    /// <summary>
    /// El Node que corresponde a una ruta. Las raíces reciben un ordinal alto al azar, para no chocar con
    /// las de la base local en las pruebas [SqlServerFact]; los hijos, el siguiente detrás del último
    /// hermano (seguido, guardado o reservado), como GenealogyNodes.NextChildAsync.
    /// </summary>
    private static HierarchyId NodoDesdeRuta(AppDbContext db, string ruta)
    {
        var reservados = Reservados.GetOrCreateValue(db);
        if (reservados.Remove(ruta, out var reservado))
            return reservado;
        return NuevoNodo(db, ruta, reservados);
    }

    private static HierarchyId NuevoNodo(AppDbContext db, string ruta, Dictionary<string, HierarchyId> reservados)
    {
        var segmentos = ruta.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segmentos.Length <= 1)
            return HierarchyId.Parse($"/{Random.Shared.Next(1_000_000, int.MaxValue)}/");

        var rutaPadre = "/" + string.Join('/', segmentos[..^1]) + "/";
        var rutaSinBarra = rutaPadre.TrimEnd('/'); // hay rutas antiguas guardadas sin la barra final
        var padre = db.GenealogyTree.Local
                        .Where(g => (g.HierarchyPath == rutaPadre || g.HierarchyPath == rutaSinBarra) && g.Node is not null)
                        .OrderByDescending(g => g.CreationDate).Select(g => g.Node).FirstOrDefault()
                    ?? db.GenealogyTree.AsNoTracking()
                        .Where(g => (g.HierarchyPath == rutaPadre || g.HierarchyPath == rutaSinBarra) && g.Node != null)
                        .OrderByDescending(g => g.CreationDate).Select(g => g.Node).FirstOrDefault();
        if (padre is null)
        {
            // El padre todavía no existe: se le reserva su Node para cuando se cree.
            if (!reservados.TryGetValue(rutaPadre, out padre))
            {
                padre = NuevoNodo(db, rutaPadre, reservados);
                reservados[rutaPadre] = padre;
            }
        }

        // El último hermano por Node, no un recuento: así no choca aunque falte algún ordinal.
        var nivelHijo = padre.GetLevel() + 1;
        var candidatos = db.GenealogyTree.Local
            .Where(g => g.Node is not null && g.Node.GetAncestor(1) == padre)
            .Select(g => g.Node!)
            .Concat(reservados.Values.Where(r => r.GetAncestor(1) == padre))
            .ToList();
        var guardado = db.GenealogyTree.AsNoTracking().IgnoreQueryFilters()
            .Where(g => g.Node != null && g.Node.IsDescendantOf(padre) && g.Node.GetLevel() == nivelHijo)
            .OrderByDescending(g => g.Node).Select(g => g.Node).FirstOrDefault();
        if (guardado is not null) candidatos.Add(guardado);
        return padre.GetDescendant(candidatos.Count == 0 ? null : candidatos.Max(), null);
    }

    /// <summary>
    /// Un nodo de inscripción OBSOLETO (R8): un miembro puede tener dos nodos y manda el más
    /// reciente. Se guarda y después se le pone una fecha de creación antigua con una segunda
    /// escritura, porque AuditInterceptor sella CreationDate al añadir.
    /// </summary>
    public static async Task<GenealogyEntity> NodoObsoletoAsync(
        AppDbContext db, string memberId, string? padre, string ruta, int nivel)
    {
        var nodo = Nodo(db, memberId, padre, ruta, nivel);
        await db.SaveChangesAsync();
        nodo.CreationDate = new DateTime(2020, 1, 1);
        await db.SaveChangesAsync();
        return nodo;
    }

    /// <summary>
    /// Un nodo del binario. Plan P: con su clave compacta, calculada desde el padre si ya esta sembrado en este
    /// contexto (o con un ordinal de raiz nuevo si es raiz); sin padre sembrado, sin clave.
    /// </summary>
    public static DualTeamEntity NodoBinario(AppDbContext db, string memberId, string? padre, string ruta, TreeSide lado)
    {
        byte[]? clave = null;
        int? prof = null;
        if (padre is null)
        {
            clave = DualBinaryPath.Root(OrdinalDeRaiz(db));
            prof = 0;
        }
        else if (db.DualTeamTree.Local.FirstOrDefault(n => n.MemberId == padre) is { BinaryPath: not null } p)
        {
            clave = DualBinaryPath.Child(p.BinaryPath, lado);
            prof = p.Depth + 1;
        }
        var nodo = new DualTeamEntity
        {
            MemberId       = memberId,
            ParentMemberId = padre,
            Side           = lado,
            BinaryPath     = clave,
            Depth          = prof,
            CreatedBy      = "prueba"
        };
        db.DualTeamTree.Add(nodo);
        return nodo;
    }

    private static int _ordinalEnMemoria = 1_000_000;

    /// <summary>Un ordinal de raiz que no choca con ninguno real: la secuencia en SQL Server, un contador en InMemory.</summary>
    public static int OrdinalDeRaiz(AppDbContext db) =>
        db.Database.IsSqlServer()
            ? db.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR dbo.DualTeamRootOrdinal AS [Value]").ToList()[0]
            : Interlocked.Increment(ref _ordinalEnMemoria);

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
    /// Un pedido de <paramref name="memberId"/> con un producto por cada nivel de
    /// <paramref name="niveles"/> (null = un producto que no es de membresía). Un pedido de alta
    /// lleva Lifestyle Ambassador (1) y, si acaso, VIP, Elite o Turbo: su nivel es el más alto.
    /// </summary>
    public static OrdersEntity Orden(AppDbContext db, string memberId, DateTime fecha, params int?[] niveles)
    {
        var orden = new OrdersEntity
        {
            MemberId  = memberId,
            OrderDate = fecha,
            Status    = OrderStatus.Completed,
            CreatedBy = "prueba"
        };
        db.Orders.Add(orden);

        foreach (var nivel in niveles)
        {
            var producto = new Product
            {
                Name              = nivel is null ? "Otro producto" : $"Membresía {nivel}",
                Description       = "prueba",
                ImageUrl          = "prueba",
                MonthlyFee        = 0,
                SetupFee          = 0,
                MembershipLevelId = nivel,
                CreatedBy         = "prueba"
            };
            db.Products.Add(producto);
            db.OrderDetails.Add(new OrderDetail
            {
                OrderId   = orden.Id,
                ProductId = producto.Id,
                Quantity  = 1,
                CreatedBy = "prueba"
            });
        }

        return orden;
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
