using System.Xml.Linq;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>Las claves del Plan K existen y no están vacías en los nueve idiomas.</summary>
public class TextosDeCuotaAnualTests
{
    private static readonly string[] Culturas = { "", ".es", ".pt", ".fr", ".it", ".de", ".ko", ".zh", ".ka" };

    public static readonly string[] Claves =
    {
        "StepUp.WhyAnnualFee",
        "AnnualFee.Title", "AnnualFee.LoadFailed", "AnnualFee.NotApplicable",
        "AnnualFee.Status.UpToDate", "AnnualFee.Status.DueSoon", "AnnualFee.Status.Due",
        "AnnualFee.Status.UnderReview", "AnnualFee.Status.Waived", "AnnualFee.Status.NotApplicable",
        "AnnualFee.NextDue", "AnnualFee.LastPaid", "AnnualFee.TokensAvailable",
        "AnnualFee.Preference", "AnnualFee.PreferenceHelp", "AnnualFee.PreferenceSaved",
        "AnnualFee.PayWith", "AnnualFee.Method.Token", "AnnualFee.Method.CommissionBalance", "AnnualFee.Method.EWallet",
        "AnnualFee.CardSoon", "AnnualFee.PayNow", "AnnualFee.Retry", "AnnualFee.Paid", "AnnualFee.ReadOnly",
        "AnnualFee.Error.NotDue", "AnnualFee.Error.AlreadyPaid", "AnnualFee.Error.InProgress",
        "AnnualFee.Error.NeedsReview", "AnnualFee.Error.NoToken", "AnnualFee.Error.InsufficientCommissions",
        "AnnualFee.Error.EWallet", "AnnualFee.Error.Waived", "AnnualFee.Error.AccountNotActive",
        "AnnualFee.Error.Busy", "AnnualFee.Error.MembershipOnHold", "AnnualFee.Error.Impersonation", "AnnualFee.Error.StepUp",
        "AnnualFee.Error.Unavailable", "AnnualFee.Error.Generic",
    };

    public static IEnumerable<object[]> Archivos() => Culturas.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Archivos))]
    public void Cada_idioma_tiene_las_claves_del_plan_K_y_no_estan_vacias(string cultura)
    {
        var ruta = Path.Combine(ReportesDb.RaizDelRepositorio(), "MLMConquerorGlobalEdition.SharedComponents",
            "Resources", $"SharedResources{cultura}.resx");
        using var archivo = File.OpenRead(ruta);
        var datos = XDocument.Load(archivo).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

        Claves.Where(k => !datos.ContainsKey(k)).Should().BeEmpty($"SharedResources{cultura}.resx tiene que traerlas");
        Claves.Where(k => datos.TryGetValue(k, out var v) && string.IsNullOrWhiteSpace(v)).Should().BeEmpty();
        datos["AnnualFee.Paid"].Should().Contain("{0}");
    }

    [Fact]
    public void Cada_codigo_de_error_de_la_cuota_tiene_un_texto_con_clave()
    {
        foreach (var codigo in typeof(MLMConquerorGlobalEdition.SharedKernel.Billing.AnnualFeeErrors).GetFields()
                     .Select(f => (string)f.GetValue(null)!))
            Claves.Should().Contain(MLMConquerorGlobalEdition.SharedComponents.Components.Profile.AnnualFeeMessages.ErrorKeyFor(codigo));
    }

    // ── Revisión final (M-c): la misma regla de conservar la clave que en tokens ─────────

    [Theory]
    [InlineData("ANNUAL_FEE_IN_PROGRESS", 409, true)]
    [InlineData("ANNUAL_FEE_BUSY", 409, true)]
    [InlineData("BILLING_UNAVAILABLE", 502, true)]
    [InlineData("STEP_UP_REQUIRED", 403, true)]
    [InlineData("STEP_UP_INVALID", 403, true)]
    [InlineData(null, 500, true)]
    [InlineData("INTERNAL_ERROR", 500, true)]
    [InlineData("BILLING_CONTRACT_ERROR", 502, false)]
    [InlineData("ANNUAL_FEE_NEEDS_REVIEW", 400, false)] // M-b: en revisión no se repite el intento
    [InlineData("ANNUAL_FEE_NOT_DUE", 400, false)]
    [InlineData("IDEMPOTENCY_KEY_REUSED", 409, false)]
    public void Conserva_la_clave_cuando_no_se_sabe_si_entro(string? codigo, int estado, bool conserva)
        => MLMConquerorGlobalEdition.SharedComponents.Components.Profile.AnnualFeeMessages
            .KeepsKey(codigo, (System.Net.HttpStatusCode)estado).Should().Be(conserva);

    [Theory]
    [InlineData("STEP_UP_REQUIRED", 403)]
    [InlineData(null, 500)]
    [InlineData("INTERNAL_ERROR", 503)]
    [InlineData("BILLING_CONTRACT_ERROR", 502)]
    public void La_parte_comun_coincide_con_la_de_tokens(string? codigo, int estado)
    {
        var s = (System.Net.HttpStatusCode)estado;
        MLMConquerorGlobalEdition.SharedComponents.Components.Profile.AnnualFeeMessages.KeepsKey(codigo, s)
            .Should().Be(MLMConquerorGlobalEdition.SharedComponents.Components.Tokens.TokenPurchaseMessages.KeepsKey(codigo, s));
    }
}
