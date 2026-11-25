using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccessAuditGrid;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Security;

/// <summary>
/// El grid del registro de auditoría de acceso filtra, ordena y pagina en el servidor sobre TODO el registro, no solo
/// sobre la página que antes cargaba la pantalla.
/// </summary>
public class GridDeAuditoriaDeAccesoTests
{
    private static readonly DateTime Base = new(2026, 1, 1);

    /// <summary>
    /// 120 cambios Active→Inactive de AMB-001 con fechas crecientes y, el MÁS ANTIGUO, un Active→Suspended de
    /// AMB-XYZ hecho por "admin-7": con lo más reciente primero, queda en la última página.
    /// </summary>
    private static async Task<AppDbContext> ConRegistro()
    {
        var db = InMemoryDbHelper.Create();
        for (var i = 0; i < 120; i++)
            db.MemberStatusHistories.Add(Cambio("AMB-001", MemberAccountStatus.Inactive, Base.AddHours(i), "system"));
        db.MemberStatusHistories.Add(Cambio("AMB-XYZ", MemberAccountStatus.Suspended, Base.AddYears(-1), "admin-7"));
        await db.SaveChangesAsync();
        return db;
    }

    private static MemberStatusHistory Cambio(string miembro, MemberAccountStatus nuevo, DateTime cuando, string quien) => new()
    {
        MemberId = miembro, OldStatus = MemberAccountStatus.Active, NewStatus = nuevo, Reason = "prueba",
        ChangedAt = cuando, CreationDate = cuando, CreatedBy = quien
    };

    private static GridDataRequest Pagina(int tamano = 50, params GridFilter[] filtros) => new()
    {
        Page = 1, PageSize = tamano, Filters = filtros.ToList()
    };

    [Fact]
    public async Task El_filtro_de_columna_busca_en_todo_el_registro()
    {
        await using var db = await ConRegistro();
        var handler = new GetAccessAuditGridHandler(db);

        var r = await handler.Handle(new GetAccessAuditGridQuery(
            Pagina(50, new GridFilter { Field = "ChangedBy", Operator = "equal", Value = "admin-7" })),
            CancellationToken.None);

        r.Value!.TotalCount.Should().Be(1);
        var fila = r.Value.Items.Single();
        fila.MemberId.Should().Be("AMB-XYZ");
        fila.NewStatus.Should().Be("Suspended");
    }

    [Fact]
    public async Task Sin_orden_lo_mas_reciente_va_primero()
    {
        await using var db = await ConRegistro();
        var handler = new GetAccessAuditGridHandler(db);

        var r = await handler.Handle(new GetAccessAuditGridQuery(Pagina()), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(121);
        r.Value.Items.First().ChangedAt.Should().Be(Base.AddHours(119));
        r.Value.Items.Select(x => x.ChangedAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task El_filtro_igual_de_estado_se_aplica_sobre_la_entidad()
    {
        await using var db = await ConRegistro();
        var handler = new GetAccessAuditGridHandler(db);

        var suspendidos = await handler.Handle(new GetAccessAuditGridQuery(
            Pagina(50, new GridFilter { Field = "NewStatus", Operator = "equal", Value = "suspended" })),
            CancellationToken.None);
        var desdeActivo = await handler.Handle(new GetAccessAuditGridQuery(
            Pagina(50, new GridFilter { Field = "OldStatus", Operator = "equal", Value = "Active" })),
            CancellationToken.None);
        var nada = await handler.Handle(new GetAccessAuditGridQuery(
            Pagina(50, new GridFilter { Field = "NewStatus", Operator = "equal", Value = "Susp" })),
            CancellationToken.None);

        suspendidos.Value!.Items.Single().MemberId.Should().Be("AMB-XYZ");
        desdeActivo.Value!.TotalCount.Should().Be(121);
        nada.Value!.TotalCount.Should().Be(0);
    }

    [Theory]
    [InlineData(500, 100)]
    [InlineData(25, 25)]
    public async Task El_tamano_de_pagina_no_pasa_de_100(int pedido, int esperado)
    {
        await using var db = await ConRegistro();
        var handler = new GetAccessAuditGridHandler(db);

        var r = await handler.Handle(new GetAccessAuditGridQuery(Pagina(pedido)), CancellationToken.None);

        r.Value!.PageSize.Should().Be(esperado);
        r.Value.Items.Should().HaveCount(esperado);
    }
}
