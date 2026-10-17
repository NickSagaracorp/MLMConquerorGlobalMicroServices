using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Pagar la cuota con saldo o con un token exige step-up (Plan K, K14) con su propia operación: un
/// código confirmado para comprar tokens no vale para la cuota, y al revés.
/// </summary>
public class StepUpDeLaCuotaTests
{
    [Fact]
    public void La_operacion_de_la_cuota_esta_en_el_catalogo_cerrado()
    {
        StepUpOperations.AnnualFeeFromBalance.Should().Be("member.annual-fee.pay-from-balance");
        StepUpOperations.IsKnown(StepUpOperations.AnnualFeeFromBalance).Should().BeTrue();
    }

    [Fact]
    public void No_es_la_de_comprar_tokens()
        => StepUpOperations.AnnualFeeFromBalance.Should().NotBe(StepUpOperations.TokenPurchaseFromBalance);

    [Fact]
    public void El_dialogo_explica_por_que_pide_el_codigo()
        => StepUpMessages.WhyKeyFor(StepUpOperations.AnnualFeeFromBalance).Should().Be("StepUp.WhyAnnualFee");
}
