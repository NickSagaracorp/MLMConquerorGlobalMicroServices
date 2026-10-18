using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupAmbassador;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupMember;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// Plan M (revisión M-d): las dos formas en que el árbol frena un alta llegan al cliente como una
/// respuesta clara, no como un INTERNAL_ERROR: candado del patrocinador ocupado → 409 «reintenta»;
/// patrocinador sin Node (relleno pendiente) → 503 «no se pudo completar el alta».
/// </summary>
public class AltaSinNodoOCandadoOcupadoTests
{
    private static SignupsController Controlador(Result<SignupResponse> respuesta)
    {
        var mediador = new Mock<IMediator>();
        mediador.Setup(m => m.Send(It.IsAny<SignupMemberCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(respuesta);
        mediador.Setup(m => m.Send(It.IsAny<SignupAmbassadorCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(respuesta);
        var fraude = new Mock<IFraudFingerprintService>();
        fraude.Setup(f => f.RecordAsync(It.IsAny<string?>(), It.IsAny<MLMConquerorGlobalEdition.Domain.Enums.SignupRiskFlow>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
              .ReturnsAsync(new FingerprintCaptureResult(false, null, 1));
        return new SignupsController(mediador.Object, fraude.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Theory]
    [InlineData(SignupGenealogyErrors.SponsorBusy, StatusCodes.Status409Conflict)]
    [InlineData(SignupGenealogyErrors.SponsorNotReady, StatusCodes.Status503ServiceUnavailable)]
    public async Task El_alta_de_miembro_y_la_de_embajador_responden_con_su_codigo(string codigo, int estado)
    {
        var fallo = Result<SignupResponse>.Failure(codigo, "x");

        var miembro = (ObjectResult)await Controlador(fallo).SignupMember(new MemberSignupRequest(), default);
        var embajador = (ObjectResult)await Controlador(fallo).SignupAmbassador(new AmbassadorSignupRequest(), default);

        miembro.StatusCode.Should().Be(estado);
        embajador.StatusCode.Should().Be(estado);
    }

    [Fact]
    public void Las_excepciones_del_arbol_se_traducen_a_esos_codigos()
    {
        SignupGenealogyErrors.From(new GenealogyChildrenBusyException("AMB-1"))!.Value.Code.Should().Be(SignupGenealogyErrors.SponsorBusy);
        SignupGenealogyErrors.From(new GenealogyNodeMissingException("AMB-1"))!.Value.Code.Should().Be(SignupGenealogyErrors.SponsorNotReady);
        SignupGenealogyErrors.From(new InvalidOperationException("otra cosa")).Should().BeNull();
    }

    [Fact]
    public async Task Un_patrocinador_sin_Node_da_SponsorNotReady_y_no_guarda_nada()
    {
        await using var db = InMemoryDbHelper.Create();
        var fecha = new DateTime(2026, 4, 1);
        db.MembershipLevels.Add(new MLMConquerorGlobalEdition.Domain.Entities.Membership.MembershipLevel
        {
            Id = 1, Name = "Elite", IsActive = true, SortOrder = 1, CreatedBy = "seed", CreationDate = fecha
        });
        db.MemberProfiles.Add(new MLMConquerorGlobalEdition.Domain.Entities.Member.MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = "AMB-000001", Email = "s@x.test", FirstName = "S", LastName = "S",
            Country = "US", ReplicateSiteSlug = "sin-nodo", Status = MLMConquerorGlobalEdition.Domain.Entities.Member.MemberAccountStatus.Active,
            MemberType = MLMConquerorGlobalEdition.Domain.Enums.MemberType.Ambassador, EnrollDate = fecha, CreatedBy = "seed"
        });
        db.GenealogyTree.Add(new MLMConquerorGlobalEdition.Domain.Entities.Tree.GenealogyEntity
        {
            MemberId = "AMB-000001", HierarchyPath = "/AMB-000001/", Level = 1, Node = null, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        var reloj = new Mock<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(fecha);
        var usuarios = UserManagerHelper.Create();
        var handler = new SignupMemberHandler(db, reloj.Object, usuarios.Object,
            Mock.Of<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService>(), Mock.Of<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEncryptionService>());

        var r = await handler.Handle(new SignupMemberCommand(new MemberSignupRequest
        {
            SponsorReplicateSite = "sin-nodo", Email = "nuevo@x.test", FirstName = "N", LastName = "N",
            Password = "SecurePass!1", Country = "US", MembershipLevelId = 1
        }), CancellationToken.None);

        r.ErrorCode.Should().Be(SignupGenealogyErrors.SponsorNotReady);
        db.ChangeTracker.Clear();
        db.MemberProfiles.Count().Should().Be(1, "solo el patrocinador");
    }
}
