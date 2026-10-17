namespace MLMConquerorGlobalEdition.Billing.Tests.Helpers;

/// <summary>
/// La raíz del repositorio (la carpeta del .slnx), para que las guardas lean archivos fuente como
/// Program.cs. Sube desde el directorio de salida de las pruebas.
/// </summary>
public static class Repositorio
{
    public static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx")))
            dir = dir.Parent;

        return dir?.FullName
               ?? throw new InvalidOperationException("No se encontró MLMConquerorGlobalEdition.slnx subiendo desde las pruebas.");
    }

    public static string Leer(params string[] partes) =>
        File.ReadAllText(Path.Combine(new[] { Raiz() }.Concat(partes).ToArray()));
}
