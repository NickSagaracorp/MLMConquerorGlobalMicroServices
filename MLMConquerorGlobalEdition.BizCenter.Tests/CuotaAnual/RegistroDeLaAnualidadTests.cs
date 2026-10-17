using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Plan K (K16): los dos anfitriones que calculan «Calificado» (BizCenter y AdminAPI) registran la
/// anualidad real, y la implementación que siempre decía «nadie pagó» ya no existe para que nadie la
/// vuelva a registrar por error.
/// </summary>
public class RegistroDeLaAnualidadTests
{
    [Theory]
    [InlineData("MLMConquerorGlobalEdition.BizCenter")]
    [InlineData("MLMConquerorGlobalEdition.AdminAPI")]
    public void El_anfitrion_registra_la_anualidad_real(string proyecto)
    {
        var programa = File.ReadAllText(Path.Combine(ReportesDb.RaizDelRepositorio(), proyecto, "Program.cs"));
        programa.Should().Contain("Repository.Services.AnnualFee.BillingStateAnnualFeeStatus>")
                .And.NotContain("AnnualFeeNotModeled");
    }

    [Fact]
    public void AnnualFeeNotModeled_ya_no_existe()
        => typeof(MLMConquerorGlobalEdition.Repository.Services.Teams.IAnnualFeeStatus).Assembly
            .GetTypes().Should().NotContain(t => t.Name == "AnnualFeeNotModeled");
}
