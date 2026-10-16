using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.UpgradeMembership;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

/// <summary>
/// <c>POST api/v1/members/{memberId}/membership/upgrade</c> subía de nivel sin cobrar nada. Va detrás de
/// <c>Membership:UpgradeEnabled</c>, apagado por defecto y si falta: apagado es <c>400 UPGRADE_DISABLED</c>
/// y no cambia nada. Lo encenderá el Plan G junto con el cobro.
/// </summary>
public class SubidaDeNivelApagadaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 16, 10, 0, 0);

    private static async Task<AppDbContext> SembrarAsync()
    {
        var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = "AMB-1", FirstName = "A", LastName = "B", MemberType = MemberType.Ambassador,
            EnrollDate = Ahora, Country = "US", CreatedBy = "seed", LastUpdateDate = Ahora
        });
        db.MembershipLevels.Add(new MembershipLevel { Id = 1, Name = "Basic", SortOrder = 10, IsActive = true, CreatedBy = "seed" });
        db.MembershipLevels.Add(new MembershipLevel { Id = 3, Name = "Elite", SortOrder = 30, IsActive = true, CreatedBy = "seed" });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = "AMB-1", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New, StartDate = Ahora, CreatedBy = "seed", LastUpdateDate = Ahora
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static UpgradeMembershipHandler Handler(AppDbContext db, IConfiguration? config)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        return new UpgradeMembershipHandler(db, reloj.Object, config);
    }

    public static TheoryData<string, IConfiguration?> Apagada => new()
    {
        { "sin configuración", null },
        { "sin la clave", new ConfigurationBuilder().Build() },
        { "false", SubidaDeNivel.Apagada() },
        { "ilegible", SubidaDeNivel.Con("sí") }
    };

    [Theory]
    [MemberData(nameof(Apagada))]
    public async Task Apagada_es_UPGRADE_DISABLED_y_no_cambia_nada(string caso, IConfiguration? config)
    {
        await using var db = await SembrarAsync();

        var r = await Handler(db, config).Handle(new UpgradeMembershipCommand("AMB-1", 3, null), default);

        r.IsSuccess.Should().BeFalse(caso);
        r.ErrorCode.Should().Be("UPGRADE_DISABLED");
        r.ErrorCode.Should().Be(UpgradeMembershipHandler.UpgradeDisabledCode);
        db.ChangeTracker.Clear();
        var sub = await db.MembershipSubscriptions.SingleAsync();
        sub.MembershipLevelId.Should().Be(1);
        sub.SubscriptionStatus.Should().Be(MembershipStatus.Active);
        sub.EndDate.Should().BeNull();
    }

    [Fact]
    public async Task Encendida_sube_de_nivel_como_hasta_ahora()
    {
        await using var db = await SembrarAsync();

        var r = await Handler(db, SubidaDeNivel.Encendida()).Handle(new UpgradeMembershipCommand("AMB-1", 3, null), default);

        r.IsSuccess.Should().BeTrue();
        (await db.MembershipSubscriptions.CountAsync(s => s.MembershipLevelId == 3 && s.SubscriptionStatus == MembershipStatus.Active))
            .Should().Be(1);
    }

    [Fact]
    public async Task El_controlador_responde_400_con_el_codigo()
    {
        var mediador = new Mock<IMediator>();
        mediador.Setup(m => m.Send(It.IsAny<UpgradeMembershipCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Failure(UpgradeMembershipHandler.UpgradeDisabledCode, "x"));
        var usuario = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-001"), new Claim(CallerIdentity.MemberIdClaim, "AMB-1"),
                new Claim(ClaimTypes.Role, AppRoles.Ambassador)
            },
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));
        var controlador = new MembershipController(mediador.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = usuario } }
        };

        var r = await controlador.Upgrade("AMB-1", new MembershipChangeRequest { NewMembershipLevelId = 3 }, default);

        var objeto = r.Should().BeAssignableTo<ObjectResult>().Subject;
        objeto.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        objeto.Value.Should().BeOfType<ApiResponse<bool>>().Which.ErrorCode.Should().Be("UPGRADE_DISABLED");
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Solo_true_la_enciende(string? valor, bool encendida)
        => MembershipUpgrades.IsEnabled(SubidaDeNivel.Con(valor)).Should().Be(encendida);

    [Fact]
    public void Sin_configuracion_va_apagada()
        => MembershipUpgrades.IsEnabled(null).Should().BeFalse();

    [Fact]
    public void El_appsettings_base_la_deja_apagada()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        var c = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.SignupAPI", "appsettings.json"))
            .Build();

        c[MembershipUpgrades.EnabledConfigKey].Should().BeOneOf(new[] { "False", "false" }, "tiene que estar escrita y apagada");
    }
}
