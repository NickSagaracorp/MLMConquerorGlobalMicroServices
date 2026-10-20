using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan Q (DQ8): busqueda de miembros por el comienzo del nombre completo («nombre apellido» o «apellido nombre») o del
/// id, sin tildes ni mayusculas, con indice. No es prefijo de cada palabra: «luci» no encuentra «Ana Lucia Gomez». Full-Text
/// no esta instalado (FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 0).
/// <para>
/// Revision del bloque 5 (C1, I2): es el componente unico de los dos arboles. <see cref="MatchingIdsSql"/> da los MemberId
/// que casan como UNION de una rama por indice, cada una con TOP: el coste depende del tope, no de cuantos miembros haya ni
/// de cuantos casen («a» casaba con los 119.000 perfiles locales y la union buscaba el nodo de cada uno: 973 ms y 3,9 s de
/// CPU). La rama del id solo corre cuando el termino pasa del prefijo comun de los ids (<see cref="SearchesId"/>): «a»,
/// «amb» o «amb-» casan con todos los ids. DualSubtree.SearchUnder y GenealogySubtree.SearchUnder la unen con su arbol.
/// </para>
/// </summary>
public static class MemberNameSearch
{
    /// <summary>
    /// Largo maximo del termino, el de SearchName (201). Se corta ANTES de escapar y de anadir el '%': un parametro mas
    /// corto que el patron truncaria el comodin final y la busqueda pasaria a ser exacta.
    /// </summary>
    public const int MaxTermLength = 201;

    /// <summary>
    /// Hasta este tamano de equipo, la busqueda parte del rango del equipo (exacta, sin topes): su coste crece con el equipo
    /// (unas 7 lecturas por miembro). Medido en local con «a»: 5.000 miembros, 38.000 lecturas, como la forma por indice con
    /// sus topes; 19.300 miembros, 145.000 lecturas y de 2 a 3 s con la maquina cargada. Por eso no es
    /// TeamGridLimits.FreeMaxRows (20.000): por encima de 5.000 va por los indices de nombre, con tope.
    /// </summary>
    public const int RangeSearchMaxTeam = 5_000;

    /// <summary>
    /// Tope de candidatos por rama del visualizador del binario, que devuelve 25 (100 como mucho) ordenados por cercania:
    /// 40 veces lo que muestra. Con un termino muy comun el resultado es un subconjunto (los primeros por nombre).
    /// </summary>
    public const int ViewerCandidatesPerBranch = 1_000;

    /// <summary>
    /// Tope de candidatos por rama de Mi equipo limitado (equipos de mas de TeamGridLimits.FreeMaxRows): la rejilla cuenta y
    /// pagina lo que casa. Por encima del tope, el total es el de los primeros por nombre (lo explica el aviso de equipo
    /// grande: la busqueda es por el comienzo).
    /// </summary>
    public const int GridCandidatesPerBranch = 2_000;

    /// <summary>
    /// Los ids son letras, un guion opcional y digitos (AMB-DDDDDD, MBR-DDDDDD, ROOTDDD; ValidationPatterns de Signups).
    /// Hasta el primer digito, el termino es el prefijo comun de miles de ids: no dice nada del miembro.
    /// </summary>
    private static readonly Regex IdPastCommonPrefix = new(@"^[A-Za-z]{3,4}-?[0-9]", RegexOptions.CultureInvariant);

    /// <summary>El termino sin espacios alrededor y cortado a <see cref="MaxTermLength"/>.</summary>
    public static string Normalize(string term)
    {
        var t = term.Trim();
        return t.Length > MaxTermLength ? t[..MaxTermLength] : t;
    }

    /// <summary>El patron LIKE de un termino: cortado, con los comodines escapados y '%' al final.</summary>
    public static string Pattern(string term) =>
        Normalize(term).Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";

    /// <summary>True si el termino llega a los digitos de un id (p. ej. «amb-1», «AMB-70»): solo entonces se busca por id.</summary>
    public static bool SearchesId(string term) => IdPastCommonPrefix.IsMatch(Normalize(term));

    /// <summary>
    /// El SQL de los MemberId de los perfiles vivos que casan con <paramref name="term"/>: una rama por indice
    /// (IX_MemberProfiles_SearchName, _SearchNameReversed, filtrados por IsDeleted = 0, y el unico de MemberId), cada una
    /// con TOP (<paramref name="candidatesPerBranch"/>) en el orden de su indice, unidas con UNION. Con los tres LIKE en un
    /// OR el optimizador recorria MemberProfiles entero. Los parametros llevan el prefijo <paramref name="prefix"/>.
    /// </summary>
    public static (string Sql, SqlParameter[] Parameters) MatchingIdsSql(string term, int candidatesPerBranch, string prefix = "@nb")
    {
        var t = $"{prefix}_t";
        var cap = $"{prefix}_top";
        var sql = $@"SELECT MemberId FROM (SELECT TOP ({cap}) MemberId FROM MemberProfiles
        WHERE SearchName LIKE {t} AND IsDeleted = 0 ORDER BY SearchName) n1
    UNION SELECT MemberId FROM (SELECT TOP ({cap}) MemberId FROM MemberProfiles
        WHERE SearchNameReversed LIKE {t} AND IsDeleted = 0 ORDER BY SearchNameReversed) n2";
        if (SearchesId(term))
            sql += $@"
    UNION SELECT MemberId FROM (SELECT TOP ({cap}) MemberId FROM MemberProfiles
        WHERE MemberId LIKE {t} AND IsDeleted = 0 ORDER BY MemberId) n3";
        return (sql, new[] { PatternParameter(t, term), new SqlParameter(cap, SqlDbType.Int) { Value = candidatesPerBranch } });
    }

    /// <summary>El patron como parametro nvarchar(4000): cabe el termino mas largo con todos sus caracteres escapados.</summary>
    public static SqlParameter PatternParameter(string name, string term) =>
        new(name, SqlDbType.NVarChar, 4000) { Value = Pattern(term) };

    /// <summary>
    /// Fuera de SQL Server (InMemory, pruebas de logica): StartsWith sin ignorar tildes, con la misma regla para el id
    /// (<see cref="SearchesId"/>). Sin tope.
    /// </summary>
    public static IQueryable<MemberProfile> ApplyInMemory(IQueryable<MemberProfile> perfiles, string term)
    {
        var s = Normalize(term).ToLowerInvariant();
        var porId = SearchesId(term);
        return perfiles.Where(m => (m.FirstName + " " + m.LastName).ToLower().StartsWith(s)
                                || (m.LastName + " " + m.FirstName).ToLower().StartsWith(s)
                                || (porId && m.MemberId.ToLower().StartsWith(s)));
    }
}
