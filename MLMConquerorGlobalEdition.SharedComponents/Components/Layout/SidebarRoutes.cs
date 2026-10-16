namespace MLMConquerorGlobalEdition.SharedComponents.Components.Layout;

/// <summary>
/// Qué grupo del menú lateral se abre para una URL. Vive fuera del .razor para poder probarlo:
/// el grupo Dual Team buscaba solo "/team/dual" y la página de residuales (/team/residuals)
/// lo dejaba cerrado.
/// </summary>
public static class SidebarRoutes
{
    public static bool IsEnrollmentTeam(string uri) => Has(uri, "/team/enrollment");

    public static bool IsDualTeam(string uri) => Has(uri, "/team/dual") || Has(uri, "/team/residuals");

    public static bool IsCommissions(string uri) => Has(uri, "/commissions");

    private static bool Has(string uri, string fragment) =>
        uri.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
