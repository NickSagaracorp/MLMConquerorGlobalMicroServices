using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Al reservar el intento antes de pedir el QR, solo la violación del índice único de
/// ExternalOrderId (otra petición del mismo alta llegó primero) es ENTRAX_IN_PROGRESS. Cualquier otro
/// error al guardar no se disfraza de "espera un momento": se relanza.
/// </summary>
public class ReservaDelQrTests
{
    /// <summary>Hace fallar el primer guardado con la excepción dada.</summary>
    private sealed class FallaAlGuardar(Exception ex) : SaveChangesInterceptor
    {
        private bool _lanzado;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (_lanzado) return ValueTask.FromResult(result);
            _lanzado = true;
            throw ex;
        }
    }

    private static async Task<AppDbContext> AltaAsync(Exception alGuardar,
        MLMConquerorGlobalEdition.Domain.Entities.Orders.OrderStatus pedido = MLMConquerorGlobalEdition.Domain.Entities.Orders.OrderStatus.Pending)
    {
        var nombre = Guid.NewGuid().ToString();
        await using (var semilla = Helpers.InMemoryDbHelper.Create(nombre))
            await EscenarioEntrax.SembrarAltaAsync(semilla, pedido);

        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nombre)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new FallaAlGuardar(alGuardar))
            .Options);
    }

    private static Task<MLMConquerorGlobalEdition.SharedKernel.Result<MLMConquerorGlobalEdition.SharedKernel.Billing.EntraxSignupPaymentDto>>
        EmpezarAsync(AppDbContext db, Mock<IEntraxClient> cliente)
        => new EntraxSignupCheckout(db, cliente.Object, EscenarioEntrax.Opciones(), NullLogger<EntraxSignupCheckout>.Instance)
            .StartAsync(db.Orders.Single(), db.MemberProfiles.Single(), EscenarioEntrax.Ahora, default);

    [Theory]
    [InlineData("Cannot insert duplicate key row in object 'dbo.EntraxPayments' with unique index 'IX_EntraxPayments_ExternalOrderId'.")]
    public async Task El_indice_unico_del_orderId_es_ENTRAX_IN_PROGRESS_sin_llamar_a_Entrax(string mensaje)
    {
        await using var db = await AltaAsync(new DbUpdateException("guardado", new Exception(mensaje)));
        var cliente = new Mock<IEntraxClient>();

        var r = await EmpezarAsync(db, cliente);

        r.ErrorCode.Should().Be(EntraxSignupErrors.InProgress);
        cliente.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Otro_error_al_guardar_se_relanza_y_no_se_llama_a_Entrax()
    {
        await using var db = await AltaAsync(new DbUpdateException("guardado", new Exception("Timeout expired.")));
        var cliente = new Mock<IEntraxClient>();

        var accion = () => EmpezarAsync(db, cliente);

        await accion.Should().ThrowAsync<DbUpdateException>();
        cliente.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_choque_de_concurrencia_con_el_alta_aun_sin_activar_es_ENTRAX_IN_PROGRESS()
    {
        await using var db = await AltaAsync(new DbUpdateConcurrencyException("miembro"));

        (await EmpezarAsync(db, new Mock<IEntraxClient>())).ErrorCode.Should().Be(EntraxSignupErrors.InProgress);
    }

    [Fact]
    public async Task Un_choque_de_concurrencia_porque_el_alta_ya_se_activo_es_SIGNUP_ALREADY_SETTLED()
    {
        // La tarjeta (o la cripto) activó el alta entre la lectura y la reserva del QR: el RowVersion del
        // miembro choca. No es "espera un momento": el alta ya está hecha.
        await using var db = await AltaAsync(new DbUpdateConcurrencyException("miembro"),
            MLMConquerorGlobalEdition.Domain.Entities.Orders.OrderStatus.Completed);
        var cliente = new Mock<IEntraxClient>();

        (await EmpezarAsync(db, cliente)).ErrorCode.Should().Be(MLMConquerorGlobalEdition.SignupAPI.Services.SignupOrderClaim.SignupAlreadySettledCode);
        cliente.VerifyNoOtherCalls();
    }
}
