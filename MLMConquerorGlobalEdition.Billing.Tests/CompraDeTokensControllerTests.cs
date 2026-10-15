using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MLMConquerorGlobalEdition.Billing.Controllers;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Tests;

/// <summary>
/// EL COMPRADOR SALE DEL TOKEN. El personal no compra por nadie, una suplantación tampoco, y pagar
/// con saldo exige el permiso de step-up también aquí, para que no se salte llamando a Billing.
/// </summary>
public class CompraDeTokensControllerTests
{
    private readonly Mock<ITokenPurchaseService> _svc = new();
    private readonly Mock<IStepUpGrantCheck> _stepUp = new();

    private static ClaimsPrincipal Usuario(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    private static ClaimsPrincipal Miembro(params Claim[] extra) =>
        Usuario(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-001"),
            new Claim(CallerIdentity.MemberIdClaim, "AMB-100001"),
            new Claim(ClaimTypes.Role, AppRoles.Ambassador)
        }.Concat(extra).ToArray());

    private static ClaimsPrincipal Staff() =>
        Usuario(new Claim(ClaimTypes.NameIdentifier, "staff-1"), new Claim(ClaimTypes.Role, AppRoles.Admin));

    private TokenPurchasesController Controlador(ClaimsPrincipal user, params string[] grants)
    {
        _svc.Setup(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto { PurchaseId = "p1" }));
        var ctx = new DefaultHttpContext { User = user };
        if (grants.Length > 0) ctx.Request.Headers[StepUpGrant.HeaderName] = grants;
        return new TokenPurchasesController(_svc.Object, _stepUp.Object) { ControllerContext = new ControllerContext { HttpContext = ctx } };
    }

    private static PurchaseTokensRequest Pedido(string method) => new() { PaymentMethod = method };

    [Fact]
    public async Task Con_tarjeta_compra_con_el_miembro_y_el_usuario_del_token_sin_pedir_step_up()
    {
        var r = await Controlador(Miembro()).Purchase(Pedido(TokenPaymentMethods.CreditCard), default);

        r.Should().BeOfType<OkObjectResult>();
        _svc.Verify(s => s.PurchaseAsync(It.Is<TokenPurchaseInput>(i => i.MemberId == "AMB-100001" && i.Actor == "user-001"), It.IsAny<CancellationToken>()));
        _stepUp.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(TokenPaymentMethods.CommissionBalance)]
    [InlineData(TokenPaymentMethods.EWallet)]
    [InlineData("ewallet")]
    [InlineData(" EWallet ")]
    [InlineData("CommissionBalance\t")]
    public async Task Con_saldo_y_sin_permiso_es_403_STEP_UP_REQUIRED(string metodo)
    {
        var r = await Controlador(Miembro()).Purchase(Pedido(metodo), default);

        var obj = r.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        obj.Value.Should().BeOfType<ApiResponse<bool>>().Which.ErrorCode.Should().Be(StepUpGrant.RequiredErrorCode);
        _svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Con_saldo_y_permiso_valido_compra()
    {
        _stepUp.Setup(s => s.IsValid("permiso", "user-001", StepUpOperations.TokenPurchaseFromBalance)).Returns(true);

        var r = await Controlador(Miembro(), "permiso").Purchase(Pedido(TokenPaymentMethods.EWallet), default);

        r.Should().BeOfType<OkObjectResult>();
        _svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Con_saldo_y_permiso_invalido_es_403_y_no_compra()
    {
        _stepUp.Setup(s => s.IsValid(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var r = await Controlador(Miembro(), "caducado").Purchase(Pedido(TokenPaymentMethods.CommissionBalance), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dos_cabeceras_de_permiso_no_valen_aunque_una_sea_buena()
    {
        _stepUp.Setup(s => s.IsValid("permiso", "user-001", StepUpOperations.TokenPurchaseFromBalance)).Returns(true);

        var r = await Controlador(Miembro(), "permiso", "otro").Purchase(Pedido(TokenPaymentMethods.EWallet), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task El_personal_sin_memberId_no_compra()
    {
        var r = await Controlador(Staff()).Purchase(Pedido(TokenPaymentMethods.CreditCard), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Una_suplantacion_no_compra_aunque_no_sea_de_solo_lectura()
    {
        var suplantado = Miembro(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));

        var r = await Controlador(suplantado).Purchase(Pedido(TokenPaymentMethods.CreditCard), default);

        r.Should().BeOfType<ObjectResult>().Which.Value.Should().BeOfType<ApiResponse<bool>>()
         .Which.ErrorCode.Should().Be(TokenPurchaseErrors.Impersonation);
        _svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_fallo_de_la_compra_es_400_con_su_codigo()
    {
        var ctl = Controlador(Miembro());
        _svc.Setup(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Failure(TokenPurchaseErrors.MonthlyLimitReached, "tope"));

        var r = await ctl.Purchase(Pedido(TokenPaymentMethods.CreditCard), default);

        r.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().BeOfType<ApiResponse<TokenPurchaseResultDto>>()
         .Which.ErrorCode.Should().Be(TokenPurchaseErrors.MonthlyLimitReached);
    }

    [Fact]
    public async Task Reusar_la_clave_para_otra_compra_es_409()
    {
        var ctl = Controlador(Miembro());
        _svc.Setup(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Failure(TokenPurchaseErrors.IdempotencyKeyReused, "otra compra"));

        var r = await ctl.Purchase(Pedido(TokenPaymentMethods.Crypto), default);

        var obj = r.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        obj.Value.Should().BeOfType<ApiResponse<TokenPurchaseResultDto>>().Which.ErrorCode.Should().Be(TokenPurchaseErrors.IdempotencyKeyReused);
    }

    [Fact]
    public async Task Confirmar_sin_poder_emitir_es_409_para_reintentar()
    {
        _svc.Setup(s => s.ConfirmCryptoAsync("ord-1", It.IsAny<ConfirmCryptoTokenPurchaseRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Failure(TokenPurchaseErrors.IssueFailed, "choque"));
        var admin = Usuario(new Claim(ClaimTypes.NameIdentifier, "admin-1"), new Claim(ClaimTypes.Role, AppRoles.BillingManager));

        var r = await Controlador(admin).ConfirmCrypto("ord-1", new ConfirmCryptoTokenPurchaseRequest(), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Catalogo_y_fondos_son_del_miembro_del_token_y_el_personal_no_los_ve()
    {
        _svc.Setup(s => s.GetCatalogAsync("AMB-100001", It.IsAny<CancellationToken>())).ReturnsAsync(new TokenCatalogDto());
        _svc.Setup(s => s.GetFundsAsync("AMB-100001", It.IsAny<CancellationToken>())).ReturnsAsync(new TokenPurchaseFundsDto());

        (await Controlador(Miembro()).Catalog(default)).Should().BeOfType<OkObjectResult>();
        (await Controlador(Miembro()).Funds(default)).Should().BeOfType<OkObjectResult>();
        (await Controlador(Staff()).Catalog(default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await Controlador(Staff()).Funds(default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void Confirmar_cripto_y_conciliar_llevan_sus_roles()
    {
        typeof(TokenPurchasesController).GetMethod(nameof(TokenPurchasesController.ConfirmCrypto))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(AppRoles.CryptoPaymentApprovers);
        typeof(TokenPurchasesController).GetMethod(nameof(TokenPurchasesController.Reconcile))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin,BillingManager");
    }

    [Fact]
    public async Task Confirmar_dos_veces_es_409()
    {
        _svc.Setup(s => s.ConfirmCryptoAsync("ord-1", It.IsAny<ConfirmCryptoTokenPurchaseRequest>(), "admin-1", "admin@x.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Failure(TokenPurchaseErrors.CryptoAlreadyConfirmed, "ya"));
        var admin = Usuario(new Claim(ClaimTypes.NameIdentifier, "admin-1"), new Claim(ClaimTypes.Email, "admin@x.com"),
                            new Claim(ClaimTypes.Role, AppRoles.BillingManager));

        var r = await Controlador(admin).ConfirmCrypto("ord-1", new ConfirmCryptoTokenPurchaseRequest(), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Conciliar_una_compra_que_no_existe_es_404_y_firma_con_el_correo()
    {
        _svc.Setup(s => s.ReconcileAsync("p9", It.IsAny<ReconcileTokenPurchaseRequest>(), "admin@x.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Failure(TokenPurchaseErrors.PurchaseNotFound, "no"));
        var admin = Usuario(new Claim(ClaimTypes.NameIdentifier, "admin-1"), new Claim(ClaimTypes.Email, "admin@x.com"),
                            new Claim(ClaimTypes.Role, AppRoles.Admin));

        var r = await Controlador(admin).Reconcile("p9", new ReconcileTokenPurchaseRequest { Charged = true }, default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Cancelar_una_cripto_usa_el_miembro_del_token()
    {
        _svc.Setup(s => s.CancelCryptoAsync("AMB-100001", "p1", "user-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto { PurchaseId = "p1", Status = TokenPurchaseStatuses.Cancelled }));

        (await Controlador(Miembro()).Cancel("p1", default)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Cancelar_una_que_ya_no_espera_es_409()
    {
        _svc.Setup(s => s.CancelCryptoAsync("AMB-100001", "p1", "user-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Failure(TokenPurchaseErrors.NotAwaitingCrypto, "no"));

        (await Controlador(Miembro()).Cancel("p1", default))
            .Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Ni_el_personal_ni_una_suplantacion_cancelan()
    {
        var suplantado = Miembro(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));

        (await Controlador(Staff()).Cancel("p1", default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await Controlador(suplantado).Cancel("p1", default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _svc.Verify(s => s.CancelCryptoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Rechazar_cripto_lleva_el_rol_de_quien_confirma()
        => typeof(TokenPurchasesController).GetMethod(nameof(TokenPurchasesController.RejectCrypto))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(AppRoles.CryptoPaymentApprovers);

    [Fact]
    public async Task Rechazar_pasa_quien_rechaza_y_el_motivo()
    {
        var pedido = new RejectCryptoTokenPurchaseRequest { Reason = "no llegó" };
        _svc.Setup(s => s.RejectCryptoAsync("ord-1", pedido, "admin-1", "admin@x.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto { Status = TokenPurchaseStatuses.Rejected }));
        var admin = Usuario(new Claim(ClaimTypes.NameIdentifier, "admin-1"), new Claim(ClaimTypes.Email, "admin@x.com"),
                            new Claim(ClaimTypes.Role, AppRoles.SupportLevel3));

        (await Controlador(admin).RejectCrypto("ord-1", pedido, default)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void El_controlador_exige_sesion()
        => typeof(TokenPurchasesController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
}
