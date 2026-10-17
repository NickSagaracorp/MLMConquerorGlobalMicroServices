using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Domain;

/// <summary>
/// El historial de estados lo escribe StatusHistoryInterceptor al guardar. ExecuteUpdate,
/// ExecuteDelete y el SQL crudo no pasan por el seguimiento de cambios y se lo saltan (Plan J, R3).
/// Esta prueba falla si alguien cambia estados de perfiles o suscripciones así.
/// </summary>
public class EstadosSinInterceptorTests
{
    private static readonly Regex UpdateSql = new(
        @"UPDATE\s+(\[?dbo\]?\.)?\[?(MemberProfiles|MembershipSubscriptions)\]?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void Nadie_cambia_estados_por_fuera_del_seguimiento_de_cambios()
    {
        var raiz      = RaizDelRepositorio();
        var sep       = Path.DirectorySeparatorChar;
        var ofensores = new List<string>();

        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}")
                || archivo.Contains($"{sep}Migrations{sep}"))
                continue;

            var texto = File.ReadAllText(archivo);
            foreach (var sentencia in texto.Split(';'))
            {
                var masiva = sentencia.Contains("ExecuteUpdate") || sentencia.Contains("ExecuteDelete")
                             || sentencia.Contains("ExecuteSql");
                var tabla  = sentencia.Contains("MemberProfiles") || sentencia.Contains("MembershipSubscriptions");
                if (masiva && tabla && sentencia.Contains("Status"))
                    ofensores.Add(Path.GetRelativePath(raiz, archivo));
            }

            if (UpdateSql.IsMatch(texto))
                ofensores.Add(Path.GetRelativePath(raiz, archivo) + " (UPDATE en SQL)");

            // Update() de una entidad que no viene del contexto: el valor original sería el nuevo y
            // el interceptor no vería el cambio (IUnitOfWork.Members / Subscriptions).
            if (texto.Contains("Members.Update(") || texto.Contains("Subscriptions.Update("))
                ofensores.Add(Path.GetRelativePath(raiz, archivo) + " (Update de entidad sin seguir)");
        }

        ofensores.Distinct().Should().BeEmpty(
            "un cambio de estado hecho así no deja historial. Si de verdad hace falta, escribir la fila " +
            "de historial a mano en el mismo sitio y documentar aquí la excepción");
    }

    private static string RaizDelRepositorio()
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
