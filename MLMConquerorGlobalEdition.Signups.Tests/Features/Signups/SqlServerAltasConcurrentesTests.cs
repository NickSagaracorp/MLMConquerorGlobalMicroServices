using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupMember;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// Plan M (revisión I3): diez altas a la vez bajo el mismo patrocinador, cada una con su contexto,
/// contra SQL Server. El candado genealogy-children:{patrocinador} las serializa: todas terminan y
/// cada una recibe un Node distinto, hijo del Node del patrocinador. Las filas se borran al final.
/// </summary>
public class SqlServerAltasConcurrentesTests
{
    private const int Altas = 10;

    /// <summary>Cuenta los guardados que fallan: con el candado no debe chocar ninguno por el Node.</summary>
    private sealed class Choques : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public int PorNode;

        public override Task SaveChangesFailedAsync(Microsoft.EntityFrameworkCore.Diagnostics.DbContextErrorEventData e, CancellationToken ct = default)
        {
            if (e.Exception is DbUpdateException ex && Repository.Queries.GenealogyNodes.IsDuplicateNode(ex))
                Interlocked.Increment(ref PorNode);
            return Task.CompletedTask;
        }
    }

    private static SignupMemberHandler Handler(Repository.Context.AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(DateTime.Now); // extremo de la prueba, no lógica de negocio
        var usuarios = UserManagerHelper.Create();
        usuarios.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        usuarios.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        usuarios.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        var avisos = new Mock<IPushNotificationService>();
        avisos.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        var cifrado = new Mock<IEncryptionService>();
        cifrado.Setup(c => c.Encrypt(It.IsAny<string>())).Returns<string>(s => "ENC:" + s);
        return new SignupMemberHandler(db, reloj.Object, usuarios.Object, avisos.Object, cifrado.Object);
    }

    [SqlServerFact]
    public async Task Diez_altas_bajo_el_mismo_patrocinador_terminan_con_Node_distintos()
    {
        var marca      = Guid.NewGuid().ToString("N")[..10];
        var patrocinio = ("TQ" + marca).ToUpperInvariant()[..12];
        var slug       = "pm-conc-" + marca;
        HierarchyId nodoDelPatrocinador;

        await using (var db = SqlServerTestDb.Db())
        {
            nodoDelPatrocinador = HierarchyId.Parse($"/{Random.Shared.Next(1_000_000, int.MaxValue)}/");
            db.MemberProfiles.Add(new MemberProfile
            {
                UserId = Guid.NewGuid(), MemberId = patrocinio, Email = $"{slug}@prueba.test", FirstName = "Plan",
                LastName = "M", Country = "US", MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active,
                EnrollDate = DateTime.Now, ReplicateSiteSlug = slug, CreatedBy = "plan-m-prueba"
            });
            db.GenealogyTree.Add(new GenealogyEntity
            {
                MemberId = patrocinio, HierarchyPath = $"/{patrocinio}/", Level = 1, Node = nodoDelPatrocinador,
                CreatedBy = "plan-m-prueba"
            });
            await db.SaveChangesAsync();
        }

        var creados = new List<string>();
        var choques = new Choques();
        try
        {
            var resultados = await Task.WhenAll(Enumerable.Range(1, Altas).Select(async i =>
            {
                await using var db = new Repository.Context.AppDbContext(new DbContextOptionsBuilder<Repository.Context.AppDbContext>()
                    .UseSqlServer(SqlServerTestDb.ConnectionString).AddInterceptors(choques).Options);
                return await Handler(db).Handle(new SignupMemberCommand(new MemberSignupRequest
                {
                    SponsorReplicateSite = slug, FirstName = "Alta", LastName = $"Concurrente{i}",
                    Email = $"{slug}-{i}@prueba.test", Password = "SecurePass!1", Phone = "+1-555-0200",
                    Country = "US", State = "TX", City = "Houston", Address = "1 Main St", MembershipLevelId = 2
                }), CancellationToken.None);
            }));
            creados.AddRange(resultados.Where(r => r.IsSuccess).Select(r => r.Value!.MemberId));

            resultados.Should().OnlyContain(r => r.IsSuccess, "el candado serializa las altas: ninguna se agota reintentando");
            choques.PorNode.Should().Be(0, "con el candado nadie calcula un Node ya ocupado; el reintento queda de red");

            await using var lectura = SqlServerTestDb.Db();
            var nodos = await lectura.GenealogyTree.AsNoTracking()
                .Where(g => g.ParentMemberId == patrocinio)
                .Select(g => g.Node)
                .ToListAsync();
            nodos.Should().HaveCount(Altas).And.OnlyHaveUniqueItems();
            nodos.Should().OnlyContain(n => n != null && n.GetAncestor(1) == nodoDelPatrocinador);
        }
        finally
        {
            await using var limpieza = SqlServerTestDb.Db();
            var ids = creados.Append(patrocinio).ToList();
            await limpieza.RankEvaluationQueue.Where(r => ids.Contains(r.TriggerMemberId)).ExecuteDeleteAsync();
            await limpieza.GenealogyTree.IgnoreQueryFilters().Where(g => ids.Contains(g.MemberId)).ExecuteDeleteAsync();
            await limpieza.MembershipSubscriptions.IgnoreQueryFilters().Where(s => ids.Contains(s.MemberId)).ExecuteDeleteAsync();
            await limpieza.Orders.IgnoreQueryFilters().Where(o => ids.Contains(o.MemberId)).ExecuteDeleteAsync();
            await limpieza.MemberProfiles.IgnoreQueryFilters().Where(m => ids.Contains(m.MemberId)).ExecuteDeleteAsync();
        }
    }
}
