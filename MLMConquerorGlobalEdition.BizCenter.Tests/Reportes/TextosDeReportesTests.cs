using System.Xml.Linq;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Las claves del Plan J existen y no están vacías en los nueve idiomas. Cada bloque añade las
/// suyas a <see cref="Claves"/>.
/// </summary>
public class TextosDeReportesTests
{
    private static readonly string[] Culturas = { "", ".es", ".pt", ".fr", ".it", ".de", ".ko", ".zh", ".ka" };

    public static readonly string[] Claves =
    {
        // Bloque 1 — Mis enlaces
        "Sidebar.MyLinks", "PageTitle.MyLinks", "MyLinks.Personal", "MyLinks.Corporate",
        "MyLinks.Copy", "MyLinks.Copied", "MyLinks.NoSlug", "MyLinks.GoToProfile", "MyLinks.LoadFailed",
        // Bloque 2 — Residuales diarios
        "Reports.PeriodTotal", "Reports.Comment", "Reports.Consolidated", "Reports.Pending",
        "Reports.EarnedDate", "Reports.PaymentDate", "Reports.EligiblePoints", "Reports.Showing", "Reports.Results",
        "Sidebar.Residuals", "Team.LoadFailedResiduals", "Team.NoResidualEarnings",
        "Common.Year", "Common.Month", "Common.AllMonths", "Common.Of", "Common.Amount", "Common.Status",
        "Common.Previous", "Common.Next",
        // Bloque 3 — Clientes del equipo
        "Team.CustomersScopePersonal", "Team.CustomersScopeTeam", "Team.ContactOnlyPersonal",
        // Bloque 4 — Actividad por fechas
        "Sidebar.Activity", "PageTitle.EnrollmentActivity", "Activity.OtherLevel",
        "Reports.RangeInvalid", "Reports.RangeTooLong", "Reports.RangeTooOld", "Reports.RangeInFuture",
        "Reports.Apply", "Common.Error", "Common.From", "Common.To", "Reports.Name", "Reports.Id",
        "Reports.Level", "Reports.Ambassadors", "Common.Customers", "Reports.EnrollTeamPoints",
        // Bloque 5 — Cambios de estado
        "Sidebar.StatusChanges", "Sidebar.CustomerChanges", "PageTitle.EnrollmentChanges",
        "PageTitle.DualTeamChanges", "PageTitle.CustomerChanges", "Changes.FirstName",
        "Changes.LastName", "Changes.ChangedOn", "Changes.NoHistory",
        "Reports.BizCenterStatus", "Reports.MembershipStatus", "Reports.Qualified", "Common.Yes", "Common.No",
        "Reports.Sponsor", "Reports.DualTeamUpline", "Reports.Leg", "Reports.Left", "Reports.Right", "Sidebar.DualTeam",
        // Bloque 6 — Eventos del equipo
        "Sidebar.Events", "PageTitle.TeamEvents", "Events.Select", "Events.None",
        "Events.RegisteredOnly", "Events.Registered", "Events.Tickets", "Events.RegistrationDate",
        "Events.ImportPending", "Reports.EnrollmentDate", "Reports.Rank",
        // Plan Q, Tarea 22 — equipo grande (DQ7)
        "Team.LargeTeamLimited",
    };

    public static IEnumerable<object[]> Archivos() => Culturas.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Archivos))]
    public void Cada_idioma_tiene_las_claves_del_plan_J_y_no_estan_vacias(string cultura)
    {
        var ruta = Path.Combine(ReportesDb.RaizDelRepositorio(), "MLMConquerorGlobalEdition.SharedComponents",
            "Resources", $"SharedResources{cultura}.resx");
        // Por flujo y no por ruta: XDocument.Load(string) trata la ruta como URI y una carpeta con
        // caracteres especiales (o un '#') la rompe.
        using var archivo = File.OpenRead(ruta);
        var datos = XDocument.Load(archivo).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

        Claves.Where(k => !datos.ContainsKey(k)).Should().BeEmpty($"SharedResources{cultura}.resx tiene que traerlas");
        Claves.Where(k => datos.TryGetValue(k, out var v) && string.IsNullOrWhiteSpace(v)).Should().BeEmpty();
    }
}
