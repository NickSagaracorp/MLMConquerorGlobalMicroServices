using FluentAssertions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Domain.Tests;

/// <summary>Plan M (M3): el número de pedido cabe siempre en Orders.OrderNo, nvarchar(40).</summary>
public class OrderNumberHelperTests
{
    [Fact]
    public void El_numero_conserva_su_forma()
        => OrderNumberHelper.Generate("Travel Advantage Elite", new DateTime(2026, 10, 17, 6, 46, 56, 681))
            .Should().MatchRegex(@"^TAE1017-064656681-[A-Z]{4}$");

    [Fact]
    public void Una_descripcion_larga_no_pasa_de_la_columna()
    {
        var numero = OrderNumberHelper.Generate(string.Join(' ', Enumerable.Repeat("Palabra", 40)), new DateTime(2026, 10, 17));

        numero.Should().StartWith("PPPPPPPP1017-");
        numero.Length.Should().BeLessThanOrEqualTo(OrderNumberHelper.MaxLength);
    }

    [Fact]
    public void La_columna_mide_40()
        => OrderNumberHelper.MaxLength.Should().Be(40);
}
