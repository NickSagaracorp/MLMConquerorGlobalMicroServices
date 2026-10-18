using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P: siembra de binarios pequenos para las pruebas contra SQL Server. Cada prueba usa un prefijo
/// propio; DualTeamTree no tiene FK hacia MemberProfiles, asi que no hacen falta perfiles.
/// </summary>
public static class BinarioDb
{
    /// <summary>Prefijo de 12 caracteres: deja sitio para el sufijo del nodo (P, L, LL...).</summary>
    public static string Prefijo(string etiqueta) => $"{etiqueta}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    /// <summary>Arbol pequeno por (id, padre, lado), en orden de padres a hijos. La raiz va con padre null.</summary>
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
        db.DualTeamTree.AsNoTracking().CountAsync(d => d.ParentMemberId == padre && d.Side == lado);

    /// <summary>Huecos (padre, lado) con mas de un hijo vivo entre los nodos del prefijo.</summary>
    public static async Task<int> HuecosRepetidosAsync(AppDbContext db, string prefijo) =>
        (await db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId != null && d.MemberId.StartsWith(prefijo))
            .GroupBy(d => new { d.ParentMemberId, d.Side })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync()).Count;

    /// <summary>Perfiles minimos para los manejadores que comprueban que el miembro existe.</summary>
    public static async Task SembrarPerfilesAsync(AppDbContext db, params string[] ids)
    {
        foreach (var id in ids)
            db.MemberProfiles.Add(new Domain.Entities.Member.MemberProfile
            {
                UserId = Guid.NewGuid(), MemberId = id, Email = $"{id.ToLowerInvariant()}@prueba.test",
                FirstName = "Plan", LastName = "P", Country = "US", MemberType = MemberType.Ambassador,
                Status = Domain.Entities.Member.MemberAccountStatus.Active, EnrollDate = DateTime.Now, CreationDate = DateTime.Now,
                LastUpdateDate = DateTime.Now, CreatedBy = "plan-p-prueba"
            });
        await db.SaveChangesAsync();
    }

    /// <summary>Borra los nodos, logs, colas y perfiles del prefijo (pruebas de concurrencia, sin transaccion envolvente).</summary>
    public static async Task LimpiarAsync(string prefijo)
    {
        await using var db = Reportes.SqlServerReportes.Db();
        var patron = prefijo + "%";
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualTeamLegFrontiers WHERE SponsorMemberId LIKE {patron} OR DeepestMemberId LIKE {patron}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PlacementLogs WHERE MemberId LIKE {patron}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DualTeamTree WHERE MemberId LIKE {patron}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM MemberProfiles WHERE MemberId LIKE {patron} AND CreatedBy = 'plan-p-prueba'");
    }
}
