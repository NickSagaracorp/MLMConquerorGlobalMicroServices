using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Lo que el modelo tiene que garantizar por sí mismo: un pedido externo y un pago externo no se
/// repiten, y la fila del pago lleva token de concurrencia (es lo que decide la carrera al activar).
/// EF InMemory no aplica los índices; aquí se comprueba que están declarados.
/// </summary>
public class EntraxPaymentModelTests
{
    [Fact]
    public void Los_estados_del_dominio_y_los_de_la_pantalla_se_llaman_igual()
        => Enum.GetNames<EntraxPaymentStatus>().Should().BeEquivalentTo(EntraxSignupStatuses.All);

    [Fact]
    public void El_pedido_externo_y_el_pago_externo_son_unicos()
    {
        using var db = InMemoryDbHelper.Create();
        var tipo = db.Model.FindEntityType(typeof(EntraxPayment))!;

        tipo.GetIndexes().Should().Contain(i => i.IsUnique
            && i.Properties.Count == 1 && i.Properties[0].Name == nameof(EntraxPayment.ExternalOrderId));
        tipo.GetIndexes().Should().Contain(i => i.IsUnique
            && i.Properties.Count == 1 && i.Properties[0].Name == nameof(EntraxPayment.ExternalPaymentId));
    }

    [Fact]
    public void Un_pedido_lo_consume_como_mucho_un_pago()
    {
        // Dos pagos distintos del mismo pedido (un QR pagado tarde y el siguiente) no pueden quedar
        // los dos Consumed: el segundo guardado choca con este índice y el pago va a revisión.
        using var db = InMemoryDbHelper.Create();
        var indice = db.Model.FindEntityType(typeof(EntraxPayment))!.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "UX_EntraxPayments_OrderId_Consumed");

        indice.Should().NotBeNull();
        indice!.IsUnique.Should().BeTrue();
        indice.Properties.Select(p => p.Name).Should().Equal(nameof(EntraxPayment.OrderId));
        indice.GetFilter().Should().Be("[Consumed] = 1");
    }

    [Fact]
    public void La_fila_del_pago_lleva_token_de_concurrencia()
    {
        using var db = InMemoryDbHelper.Create();
        db.Model.FindEntityType(typeof(EntraxPayment))!
            .FindProperty(nameof(EntraxPayment.RowVersion))!
            .IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void El_pedido_externo_lleva_el_pedido_y_el_intento()
        => EntraxPayment.BuildExternalOrderId("8f0c", 2).Should().Be("SIGNUP-8f0c-2");
}
