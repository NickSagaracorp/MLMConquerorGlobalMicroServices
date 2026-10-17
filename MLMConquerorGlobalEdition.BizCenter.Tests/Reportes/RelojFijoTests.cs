namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class RelojFijoTests
{
    [Fact]
    public void UtcNow_no_depende_de_la_zona_horaria_de_la_maquina()
    {
        // En las pruebas la hora de servidor fija se toma como UTC: el mismo valor en cualquier máquina.
        var reloj = new RelojFijo(new DateTime(2026, 3, 15, 10, 0, 0));

        reloj.UtcNow.Should().Be(new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc));
        reloj.UtcNow.Kind.Should().Be(DateTimeKind.Utc);
    }
}
