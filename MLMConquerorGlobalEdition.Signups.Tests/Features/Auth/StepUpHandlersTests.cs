using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Reto y canje del step-up. Lo que importa: no se emite para operaciones inventadas, y el
/// permiso solo sale para el mismo usuario y la misma operación del reto.
/// </summary>
public class StepUpHandlersTests : IDisposable
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";
    private const string Op     = StepUpOperations.PayoutMethodChange;

    private static readonly DateTime FixedNow = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<ITwoFactorService>   _twoFactor = new();
    private readonly Mock<IStepUpGrantService> _grants    = new();
    private readonly ApplicationUser _user = new() { Id = UserId, Email = Email, IsActive = true };

    public void Dispose() => _db.Dispose();

    private Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> Users()
    {
        var um = UserManagerHelper.Create();
        um.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(_user);
        return um;
    }

    private static ChallengeClaims Claims(string userId = UserId, string? op = Op) => new(
        Jti: "jti", UserId: userId, Email: Email, Purpose: TwoFactorPurpose.StepUp,
        OperationKey: op, Channel: TwoFactorChannel.Email, CodeHash: "h",
        IssuedAt: FixedNow, ExpiresAt: FixedNow.AddMinutes(5));

    [Fact]
    public async Task Reto_para_operacion_desconocida_no_emite_nada()
    {
        var handler = new IssueStepUpHandler(Users().Object, _twoFactor.Object, _db);

        var result = await handler.Handle(
            new IssueStepUpCommand(UserId, new StepUpChallengeRequest { OperationKey = "inventada" }), default);

        result.ErrorCode.Should().Be("UNKNOWN_OPERATION");
        _twoFactor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reto_se_emite_con_proposito_step_up_y_la_operacion()
    {
        _twoFactor.Setup(t => t.IssueAsync(_user, TwoFactorPurpose.StepUp, Op, null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeIssued>.Success(
                      new ChallengeIssued("reto", TwoFactorChannel.Email, "u***@dominio.com", FixedNow.AddMinutes(5))));
        var handler = new IssueStepUpHandler(Users().Object, _twoFactor.Object, _db);

        var result = await handler.Handle(
            new IssueStepUpCommand(UserId, new StepUpChallengeRequest { OperationKey = Op }), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ChallengeToken.Should().Be("reto");
        result.Value.Channel.Should().Be("Email");
        result.Value.MaskedTarget.Should().Be("u***@dominio.com");
    }

    [Fact]
    public async Task Codigo_bueno_devuelve_permiso_para_el_usuario_y_la_operacion()
    {
        _twoFactor.Setup(t => t.VerifyAsync("reto", "123456", TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Success(Claims()));
        _grants.Setup(g => g.Issue(UserId, Op)).Returns("permiso");
        var handler = new VerifyStepUpHandler(_twoFactor.Object, _grants.Object);

        var result = await handler.Handle(new VerifyStepUpCommand(UserId,
            new StepUpVerifyRequest { OperationKey = Op, ChallengeToken = "reto", Code = "123456" }), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.StepUpToken.Should().Be("permiso");
    }

    [Fact]
    public async Task Reto_de_otro_usuario_no_da_permiso()
    {
        _twoFactor.Setup(t => t.VerifyAsync("reto", "123456", TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Success(Claims(userId: "otro")));
        var handler = new VerifyStepUpHandler(_twoFactor.Object, _grants.Object);

        var result = await handler.Handle(new VerifyStepUpCommand(UserId,
            new StepUpVerifyRequest { OperationKey = Op, ChallengeToken = "reto", Code = "123456" }), default);

        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
        _grants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Codigo_malo_propaga_el_error_y_no_da_permiso()
    {
        _twoFactor.Setup(t => t.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Failure("CODE_INVALID", "x"));
        var handler = new VerifyStepUpHandler(_twoFactor.Object, _grants.Object);

        var result = await handler.Handle(new VerifyStepUpCommand(UserId,
            new StepUpVerifyRequest { OperationKey = Op, ChallengeToken = "reto", Code = "000000" }), default);

        result.ErrorCode.Should().Be("CODE_INVALID");
        _grants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Canje_para_operacion_desconocida_no_verifica_nada()
    {
        var handler = new VerifyStepUpHandler(_twoFactor.Object, _grants.Object);

        var result = await handler.Handle(new VerifyStepUpCommand(UserId,
            new StepUpVerifyRequest { OperationKey = "inventada", ChallengeToken = "reto", Code = "1" }), default);

        result.ErrorCode.Should().Be("UNKNOWN_OPERATION");
        _twoFactor.VerifyNoOtherCalls();
    }
}
