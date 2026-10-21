using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Xunit;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Ranks;

/// <summary>El cambio manual de rango guarda la hora del servidor (IDateTimeProvider.Now), no UTC.</summary>
public class AdminMemberRanksControllerTests
{
    private static readonly DateTime Ahora = new(2026, 10, 21, 9, 30, 0, DateTimeKind.Local);

    [Fact]
    public async Task El_rango_de_por_vida_se_guarda_con_la_hora_del_servidor()
    {
        await using var db = InMemoryDbHelper.Create();
        db.RankDefinitions.Add(new RankDefinition { Id = 1, Name = "Silver", SortOrder = 1, Status = RankDefinitionStatus.Active, CreationDate = Ahora, CreatedBy = "s" });
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = "AMB-1", Email = "amb-1@example.com", FirstName = "Ana", LastName = "Ruiz",
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active, EnrollDate = Ahora, Country = "US",
            CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        await db.SaveChangesAsync();
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        var controller = new AdminMemberRanksController(db, new Mock<IRankComputationService>().Object, reloj.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin@x") }, "test"))
                }
            }
        };

        var r = await controller.SetLifetimeRank("AMB-1", new AdminMemberRanksController.SetLifetimeRankRequest { RankDefinitionId = 1 });

        r.Should().BeOfType<OkObjectResult>();
        var h = await db.MemberRankHistories.AsNoTracking().SingleAsync();
        // CreationDate y LastUpdateDate las pone el AuditInterceptor (hora del servidor); la del controlador es AchievedAt.
        h.AchievedAt.Should().Be(Ahora);
    }
}
