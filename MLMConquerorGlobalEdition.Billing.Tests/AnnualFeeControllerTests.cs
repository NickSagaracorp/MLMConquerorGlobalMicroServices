using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Controllers;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Tests;

/// <summary>
/// Pagar la cuota en Billing (Plan K): el miembro sale del token; el personal no paga por nadie y una
/// suplantación tampoco; el permiso de step-up se valida aquí también y se quema con la marca de Billing
/// salvo en una repetición de una clave que ya tiene pago. Los 403 van con "data":null.
/// </summary>
public class AnnualFeeControllerTests
{
    private readonly Mock<IAnnualFeeChargeService> _svc = new();
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

    private AnnualFeeController Controlador(ClaimsPrincipal user, params string[] grants)
    {
        _svc.Setup(s => s.PayNowAsync(It.IsAny<AnnualFeePayNowInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnnualFeePaymentResultDto>.Success(new AnnualFeePaymentResultDto { PaymentId = "p1" }));
        var ctx = new DefaultHttpContext { User = user };
        if (grants.Length > 0) ctx.Request.Headers[StepUpGrant.HeaderName] = grants;
        return new AnnualFeeController(_svc.Object, _stepUp.Object, NullLogger<AnnualFeeController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    private static PayAnnualFeeRequest Pedido(string clave = "clave-1") =>
        new() { Method = AnnualFeeMethods.CommissionBalance, IdempotencyKey = clave };

    private void PermisoBueno(bool sinQuemar = true)
    {
        _stepUp.Setup(s => s.ValidJti("permiso", "user-001", StepUpOperations.AnnualFeeFromBalance)).Returns("jti-1");
        _stepUp.Setup(s => s.TryConsumeAsync("jti-1", It.IsAny<CancellationToken>())).ReturnsAsync(sinQuemar);
    }

    private static void Es403(IActionResult r, string codigo)
    {
        var obj = r.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        obj.Value.Should().BeOfType<ApiResponse<object>>().Which.ErrorCode.Should().Be(codigo);
    }

    [Fact]
    public async Task Con_permiso_valido_paga_el_miembro_del_token_y_quema_el_permiso()
    {
        PermisoBueno();

        var r = await Controlador(Miembro(), "permiso").Pay(Pedido(), default);

        r.Should().BeOfType<OkObjectResult>();
        _svc.Verify(s => s.PayNowAsync(It.Is<AnnualFeePayNowInput>(i => i.MemberId == "AMB-100001" && i.Actor == "user-001"),
            It.IsAny<CancellationToken>()), Times.Once);
        _stepUp.Verify(s => s.TryConsumeAsync("jti-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_permiso_es_403_STEP_UP_REQUIRED()
    {
        var r = await Controlador(Miembro()).Pay(Pedido(), default);

        Es403(r, StepUpGrant.RequiredErrorCode);
        _svc.Verify(s => s.PayNowAsync(It.IsAny<AnnualFeePayNowInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_permiso_de_comprar_tokens_no_vale_para_la_cuota()
    {
        _stepUp.Setup(s => s.ValidJti("permiso", "user-001", StepUpOperations.TokenPurchaseFromBalance)).Returns("jti-t");

        Es403(await Controlador(Miembro(), "permiso").Pay(Pedido(), default), StepUpGrant.RequiredErrorCode);
    }

    [Fact]
    public async Task Con_el_permiso_ya_quemado_en_Billing_es_403_STEP_UP_INVALID()
    {
        PermisoBueno(sinQuemar: false);

        Es403(await Controlador(Miembro(), "permiso").Pay(Pedido(), default), StepUpGrant.InvalidErrorCode);
        _svc.Verify(s => s.PayNowAsync(It.IsAny<AnnualFeePayNowInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_la_cache_no_responde_falla_cerrado()
    {
        _stepUp.Setup(s => s.ValidJti("permiso", "user-001", StepUpOperations.AnnualFeeFromBalance)).Returns("jti-1");
        _stepUp.Setup(s => s.TryConsumeAsync("jti-1", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("redis"));

        Es403(await Controlador(Miembro(), "permiso").Pay(Pedido(), default), StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Repetir_una_clave_con_pago_no_quema_el_permiso()
    {
        _stepUp.Setup(s => s.ValidJti("permiso", "user-001", StepUpOperations.AnnualFeeFromBalance)).Returns("jti-1");
        _svc.Setup(s => s.HasPaymentAsync("AMB-100001", "clave-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var r = await Controlador(Miembro(), "permiso").Pay(Pedido(), default);

        r.Should().BeOfType<OkObjectResult>();
        _stepUp.Verify(s => s.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Suplantando_no_se_paga_ni_se_mira_el_permiso()
    {
        var r = await Controlador(Miembro(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9")), "permiso")
            .Pay(Pedido(), default);

        Es403(r, AnnualFeeErrors.Impersonation);
        _stepUp.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task El_personal_sin_miembro_no_paga_por_nadie()
    {
        var r = await Controlador(Usuario(new Claim(ClaimTypes.NameIdentifier, "staff-1"), new Claim(ClaimTypes.Role, AppRoles.Admin)))
            .Pay(Pedido(), default);

        Es403(r, AnnualFeeErrors.NotAMember);
    }

    [Theory]
    [InlineData(AnnualFeeErrors.IdempotencyKeyReused, StatusCodes.Status409Conflict)]
    [InlineData(AnnualFeeErrors.Busy, StatusCodes.Status409Conflict)]
    [InlineData(AnnualFeeErrors.InProgress, StatusCodes.Status409Conflict)]
    [InlineData(AnnualFeeErrors.NotDue, StatusCodes.Status400BadRequest)]
    [InlineData(AnnualFeeErrors.MethodNotSupported, StatusCodes.Status400BadRequest)]
    public async Task Los_fallos_salen_con_su_estado(string codigo, int estado)
    {
        PermisoBueno();
        var c = Controlador(Miembro(), "permiso");
        _svc.Setup(s => s.PayNowAsync(It.IsAny<AnnualFeePayNowInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnnualFeePaymentResultDto>.Failure(codigo, "x"));

        var r = await c.Pay(Pedido(), default);

        r.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(estado);
    }

    [Fact]
    public void Conciliar_es_solo_para_tesoreria()
    {
        var roles = typeof(AnnualFeeController).GetMethod(nameof(AnnualFeeController.Reconcile))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');
        roles.Should().BeEquivalentTo(AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.BillingManager);
    }

    [Fact]
    public void Pagar_no_recibe_un_memberId()
    {
        foreach (var p in typeof(AnnualFeeController).GetMethod(nameof(AnnualFeeController.Pay))!.GetParameters())
        {
            p.Name!.Should().NotContainEquivalentOf("memberId");
            p.ParameterType.GetProperty("MemberId").Should().BeNull();
        }
    }
}
