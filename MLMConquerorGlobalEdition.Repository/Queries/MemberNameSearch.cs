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
