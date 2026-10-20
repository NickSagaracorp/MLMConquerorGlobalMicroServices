using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Services;

namespace MLMConquerorGlobalEdition.Domain.Tests;

/// <summary>Plan Q (DQ5): la cota de la linea de inscripcion vive en un solo sitio.</summary>
public class GenealogyUplineTests
{
    [Fact]
    public void Devuelve_los_mas_cercanos_primero_sin_el_propio_y_con_tope()
    {
        var ruta = "/" + string.Join("/", Enumerable.Range(0, 15).Select(i => $"M{i}")) + "/";
        GenealogyUpline.Closest(ruta, "M14").Should().Equal("M13", "M12", "M11", "M10", "M9", "M8", "M7", "M6", "M5", "M4");
        GenealogyUpline.Closest(ruta, null, 3).Should().Equal("M14", "M13", "M12");
        GenealogyUpline.Closest("/A/", "A").Should().BeEmpty();
        GenealogyUpline.Closest(null, "A").Should().BeEmpty();
    }
}
